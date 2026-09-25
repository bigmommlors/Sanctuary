using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Farming;

public sealed class FarmingService : IFarmingService
{
    private readonly ILogger _logger;
    private readonly IZoneManager _zoneManager;
    private readonly IRewardManager _rewardManager;
    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private PlotRuntime? _farnumPlot;
    private readonly ConcurrentDictionary<int, PlotRuntime> _wildsPlotsByZoneId = new();
    private readonly ConcurrentDictionary<ulong, WildsSession> _wildsSessions = new();
    private readonly ConcurrentDictionary<int, Npc> _debugWeedsByZoneId = new();
    private readonly ConcurrentDictionary<int, Npc> _debugRocksByZoneId = new();
    private readonly ConcurrentDictionary<int, Npc> _debugTreesByZoneId = new();
    /// <summary>
    /// EXPERIMENTAL: in-flight dig→delayed rock clears keyed by private Wilds zone id.
    /// Prevents duplicate clicks; cancelled on leave/remove/reset.
    /// </summary>
    private readonly ConcurrentDictionary<int, PendingRockClear> _pendingRockClearsByZoneId = new();

    public Npc? PlotNpc => _farnumPlot?.Foundation;

    private sealed class PlotRuntime
    {
        public required string PlotKey { get; init; }
        public required Npc Foundation { get; init; }
        public Npc? Crop { get; set; }
        public CropVisualStage LastPushedVisual { get; set; } = CropVisualStage.Empty;
    }

    private readonly record struct WildsSession(
        int InstanceZoneId,
        string ReturnZoneName,
        int ReturnZoneId,
        Vector4 ReturnPosition,
        Quaternion ReturnRotation);

    /// <summary>
    /// EXPERIMENTAL pending dig clear. Commit only if zone/player/rock Guid still match after delay.
    /// </summary>
    private readonly record struct PendingRockClear(
        ulong CharacterId,
        ulong PlayerGuid,
        ulong RockGuid,
        DateTimeOffset DueAtUtc);

    public FarmingService(
        ILogger<FarmingService> logger,
        IZoneManager zoneManager,
        IRewardManager rewardManager,
        IResourceManager resourceManager,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
        _zoneManager = zoneManager;
        _rewardManager = rewardManager;
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;
    }

    public bool TrySpawnPrototypePlot()
    {
        if (_farnumPlot is not null)
            return true;

        var zone = _zoneManager.StartingZone;

        if (zone.DefinitionId != FarmingPrototypeConfig.ZoneDefinitionId)
        {
            _logger.LogError(
                "Farming prototype expected StartingZone definition {Expected} but found {Actual} ({Name}).",
                FarmingPrototypeConfig.ZoneDefinitionId, zone.DefinitionId, zone.Name);
            return false;
        }

        if (!ModelsAvailable())
        {
            _logger.LogError("Farming prototype model ids are missing from Models.txt.");
            return false;
        }

        if (!TryCreatePlotRuntime(zone, FarmingPrototypeConfig.PlotPosition, FarmingPrototypeConfig.Heading,
                FarmingPrototypeConfig.PlotKey, out var runtime))
        {
            _logger.LogError("Failed to create Farnum farming prototype NPC.");
            return false;
        }

        _farnumPlot = runtime;

        _logger.LogInformation(
            "Farming Farnum harness plot {PlotKey} spawned in {Zone} at ({X:0.##}, {Y:0.##}, {Z:0.##}).",
            FarmingPrototypeConfig.PlotKey,
            zone.Name,
            FarmingPrototypeConfig.PositionX,
            FarmingPrototypeConfig.PositionY,
            FarmingPrototypeConfig.PositionZ);

        return true;
    }

    public bool IsInWildsTestInstance(Player player)
    {
        return player.Zone is InstanceZone instance
            && instance.DefinitionId == FarmingPrototypeConfig.WildsZoneDefinitionId
            && string.Equals(instance.Name, FarmingPrototypeConfig.WildsZoneName, StringComparison.OrdinalIgnoreCase);
    }

    public bool TryEnterWildsTestInstance(Player player, out string message)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        if (_wildsSessions.ContainsKey(characterId) || IsInWildsTestInstance(player))
        {
            message = "Already in Wilds Farm test instance. Use !farmtest leave first.";
            return false;
        }

        if (player.Zone is InstanceZone)
        {
            message = "Leave your current instance before entering Wilds Farm test.";
            return false;
        }

        if (!ModelsAvailable())
        {
            message = "Farming models missing; cannot enter Wilds Farm test.";
            return false;
        }

        if (!_zoneManager.TryCreateInstanceZone(FarmingPrototypeConfig.WildsZoneDefinitionId, out var destination))
        {
            message = "Failed to create private Wilds Farm test instance.";
            return false;
        }

        if (!string.Equals(destination.Name, FarmingPrototypeConfig.WildsZoneName, StringComparison.OrdinalIgnoreCase))
        {
            _zoneManager.TryRemoveInstanceZone(destination.Id);
            message = $"Wilds zone name mismatch (got {destination.Name}).";
            return false;
        }

        if (!TryCreatePlotRuntime(destination, FarmingPrototypeConfig.WildsPlotPosition,
                FarmingPrototypeConfig.WildsPlotHeading, FarmingPrototypeConfig.WildsPlotKey, out var runtime))
        {
            _zoneManager.TryRemoveInstanceZone(destination.Id);
            message = "Failed to spawn Wilds test dirt plot.";
            return false;
        }

        _wildsPlotsByZoneId[destination.Id] = runtime;
        RestorePlotVisualFromDb(runtime, characterId);
        SpawnUnclearedObstacles(destination, characterId);

        var returnZoneName = player.Zone?.Name ?? _zoneManager.StartingZone.Name;
        var returnZoneId = player.Zone?.Id ?? _zoneManager.StartingZone.Id;
        var returnPosition = player.Position;
        var returnRotation = player.Rotation;

        _wildsSessions[characterId] = new WildsSession(
            destination.Id,
            returnZoneName,
            returnZoneId,
            returnPosition,
            returnRotation);

        // Fresh private farm session: no prior Factory tool selection.
        player.SelectedFarmToolId = null;

        var spawn = destination.SpawnPosition;
        var rotation = destination.SpawnRotation;

        _logger.LogInformation(
            "Wilds Farm test ENTER character={CharacterId} InstanceZoneId={ZoneId} from {FromZone}/{FromId} ({Pos}) -> {DestName}/{DestId} spawn={Spawn}.",
            characterId, destination.Id, returnZoneName, returnZoneId, returnPosition,
            destination.Name, destination.Id, spawn);

        player.TeleportToZone(destination, spawn, rotation);

        message =
            $"Entered private Wilds Farm test ({FarmingPrototypeConfig.WildsZoneName}). " +
            $"Plot {FarmingPrototypeConfig.WildsPlotKey} nearby. Uncleared obstacles auto-spawned. " +
            "Use !farmtest leave to return.";
        return true;
    }

    public bool TryLeaveWildsTestInstance(Player player, out string message)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        if (!_wildsSessions.TryRemove(characterId, out var session))
        {
            if (!IsInWildsTestInstance(player))
            {
                message = "Not in a Wilds Farm test instance.";
                return false;
            }

            session = new WildsSession(
                player.Zone!.Id,
                _zoneManager.StartingZone.Name,
                _zoneManager.StartingZone.Id,
                player.StartingZonePosition,
                player.StartingZoneRotation);

            _logger.LogWarning(
                "Wilds Farm test LEAVE character={CharacterId} had no session; using StartingZonePosition fallback.",
                characterId);
        }

        var instanceZoneId = session.InstanceZoneId;
        var destination = _zoneManager.StartingZone;

        if (!string.Equals(destination.Name, session.ReturnZoneName, StringComparison.OrdinalIgnoreCase) &&
            destination.Id != session.ReturnZoneId)
        {
            _logger.LogWarning(
                "Wilds Farm test LEAVE stored return {StoredName}/{StoredId} differs from StartingZone {DestName}/{DestId}; returning to StartingZone.",
                session.ReturnZoneName, session.ReturnZoneId, destination.Name, destination.Id);
        }

        _logger.LogInformation(
            "Wilds Farm test LEAVE character={CharacterId} InstanceZoneId={ZoneId} -> {ReturnZone}/{ReturnId} ({Pos}). Crop DB preserved.",
            characterId, instanceZoneId, destination.Name, destination.Id, session.ReturnPosition);

        // Session-only Factory tool selection must not leak across farm exits / zone returns.
        player.SelectedFarmToolId = null;

        player.TeleportToZone(destination, session.ReturnPosition, session.ReturnRotation);

        CancelPendingRockClear(instanceZoneId);

        _wildsPlotsByZoneId.TryRemove(instanceZoneId, out _);
        _debugWeedsByZoneId.TryRemove(instanceZoneId, out _);
        _debugRocksByZoneId.TryRemove(instanceZoneId, out _);
        _debugTreesByZoneId.TryRemove(instanceZoneId, out _);

        if (!_zoneManager.TryRemoveInstanceZone(instanceZoneId))
        {
            _logger.LogWarning(
                "Wilds Farm test instance ZoneId={ZoneId} was not removed (still occupied or missing).",
                instanceZoneId);
        }

        message = "Left Wilds Farm test. Returned to prior overworld position. Crop state preserved.";
        return true;
    }

    public bool TrySpawnDebugWeed(Player player, out string message)
    {
        if (!IsInWildsTestInstance(player) || player.Zone is null)
        {
            message = "Debug weed only spawns in private Wilds Farm (!farmtest enter).";
            return false;
        }

        return TrySpawnWeedInZone(player.Zone, out message);
    }

    public bool TryRemoveDebugWeed(Player player, out string message)
    {
        if (!IsInWildsTestInstance(player) || player.Zone is null)
        {
            message = "Not in private Wilds Farm test instance.";
            return false;
        }

        var zoneId = player.Zone.Id;

        if (!_debugWeedsByZoneId.TryRemove(zoneId, out var weed))
        {
            message = "No debug weed to remove.";
            return false;
        }

        weed.Dispose();

        _logger.LogInformation(
            "Prototype weed removed (debug cmd) zone={ZoneId} guid={Guid}.",
            zoneId, weed.Guid);

        message = "Debug weed removed (session only; cleared DB state unchanged).";
        return true;
    }

    /// <summary>
    /// Click-to-clear for prototype weed. Persists cleared for this character + Wilds farm.
    /// </summary>
    private void HandleDebugWeedInteract(Player player)
    {
        if (!IsInWildsTestInstance(player) || player.Zone is null)
            return;

        var zoneId = player.Zone.Id;
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        _logger.LogInformation(
            "Prototype weed interaction received zone={ZoneId} character={CharacterId}.",
            zoneId, characterId);

        if (!_debugWeedsByZoneId.TryRemove(zoneId, out var weed))
        {
            _logger.LogWarning(
                "Prototype weed interaction but no tracked weed zone={ZoneId} character={CharacterId}.",
                zoneId, characterId);
            return;
        }

        var guid = weed.Guid;
        weed.Dispose();
        PersistObstacleCleared(characterId, FarmingPrototypeConfig.WildsWeedObstacleKey);

        _logger.LogInformation(
            "Prototype weed cleared/despawned zone={ZoneId} guid={Guid} character={CharacterId} key={ObstacleKey}.",
            zoneId, guid, characterId, FarmingPrototypeConfig.WildsWeedObstacleKey);

        ChatHelper.SendSystemMessage(player, "Weed cleared.");
    }

    public bool TrySpawnDebugRock(Player player, out string message)
    {
        if (!IsInWildsTestInstance(player) || player.Zone is null)
        {
            message = "Debug rock only spawns in private Wilds Farm (!farmtest enter).";
            return false;
        }

        return TrySpawnRockInZone(player.Zone, out message);
    }

    public bool TryRemoveDebugRock(Player player, out string message)
    {
        if (!IsInWildsTestInstance(player) || player.Zone is null)
        {
            message = "Not in private Wilds Farm test instance.";
            return false;
        }

        var zoneId = player.Zone.Id;

        CancelPendingRockClear(zoneId);

        if (!_debugRocksByZoneId.TryRemove(zoneId, out var rock))
        {
            message = "No debug rock to remove.";
            return false;
        }

        rock.UpdateEverySecondAction = null;
        rock.Dispose();

        _logger.LogInformation(
            "Prototype rock removed (debug cmd) zone={ZoneId} guid={Guid}.",
            zoneId, rock.Guid);

        message = "Debug rock removed (session only; cleared DB state unchanged).";
        return true;
    }

    /// <summary>
    /// Click-to-clear for prototype rock. EXPERIMENTAL: requires session Shovel (ToolId=4)
    /// selected via EquipTool 188/7. Without Shovel: reject, leave rock, no DB write.
    /// With Shovel: play farm_dig (3900003), wait EXPERIMENTAL ~1.5s via zone second timer,
    /// then persist+despawn. Not retail Factory obstacle protocol.
    /// </summary>
    private void HandleDebugRockInteract(Player player)
    {
        if (!IsInWildsTestInstance(player) || player.Zone is null)
            return;

        var zoneId = player.Zone.Id;
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        _logger.LogInformation(
            "Prototype rock interaction received zone={ZoneId} character={CharacterId} SelectedFarmToolId={SelectedFarmToolId}.",
            zoneId, characterId, player.SelectedFarmToolId);

        var alreadyPending = _pendingRockClearsByZoneId.ContainsKey(zoneId);
        if (!FarmingRockDigClearExperiment.TryBegin(player.SelectedFarmToolId, alreadyPending, out var rejectMessage))
        {
            if (rejectMessage == FarmingToolSelection.RockRequiresShovelMessage)
            {
                _logger.LogInformation(
                    "Prototype rock clear rejected (Shovel required) zone={ZoneId} character={CharacterId} SelectedFarmToolId={SelectedFarmToolId}.",
                    zoneId, characterId, player.SelectedFarmToolId);
            }
            else
            {
                _logger.LogInformation(
                    "Prototype rock clear rejected (already pending) zone={ZoneId} character={CharacterId}.",
                    zoneId, characterId);
            }

            if (!string.IsNullOrEmpty(rejectMessage))
                ChatHelper.SendSystemMessage(player, rejectMessage);
            return;
        }

        if (!_debugRocksByZoneId.TryGetValue(zoneId, out var rock))
        {
            _logger.LogWarning(
                "Prototype rock interaction but no tracked rock zone={ZoneId} character={CharacterId}.",
                zoneId, characterId);
            return;
        }

        var startedAt = DateTimeOffset.UtcNow;
        var pending = new PendingRockClear(
            characterId,
            player.Guid,
            rock.Guid,
            FarmingRockDigClearExperiment.ComputeDueAtUtc(startedAt));

        if (!_pendingRockClearsByZoneId.TryAdd(zoneId, pending))
        {
            // Race: another click won the pending slot.
            ChatHelper.SendSystemMessage(player, FarmingRockDigClearExperiment.AlreadyDiggingMessage);
            return;
        }

        // Same verified packet path as !farmtest diganim (PlayerUpdatePacketSetAnimation farm_dig).
        var animPacket = FarmingDigAnimExperiment.CreatePlayNowPacket(player.Guid);
        player.SendTunneledToVisible(animPacket, sendToSelf: true);

        // Rock stays visible; zone UpdateEverySecondAction commits after EXPERIMENTAL delay.
        rock.UpdateEverySecondAction = () => TryCompletePendingRockClear(zoneId);

        _logger.LogInformation(
            "EXPERIMENTAL prototype rock dig started zone={ZoneId} guid={Guid} character={CharacterId} AnimationId={AnimationId} delayMs={DelayMs} dueAt={DueAt:u}.",
            zoneId, rock.Guid, characterId,
            FarmingDigAnimExperiment.FarmDigAnimationId,
            FarmingRockDigClearExperiment.ExperimentalClearDelayMs,
            pending.DueAtUtc);
    }

    /// <summary>
    /// EXPERIMENTAL: zone-second poll for delayed rock clear. Revalidates player + rock before persist.
    /// </summary>
    private void TryCompletePendingRockClear(int zoneId)
    {
        if (!_pendingRockClearsByZoneId.TryGetValue(zoneId, out var pending))
        {
            ClearRockUpdateAction(zoneId);
            return;
        }

        if (!FarmingRockDigClearExperiment.IsDue(DateTimeOffset.UtcNow, pending.DueAtUtc))
            return;

        // Claim completion so duplicate second-ticks / clicks cannot double-persist.
        if (!_pendingRockClearsByZoneId.TryRemove(zoneId, out pending))
            return;

        ClearRockUpdateAction(zoneId);

        if (!_debugRocksByZoneId.TryGetValue(zoneId, out var rock) ||
            !FarmingRockDigClearExperiment.MatchesTrackedRock(pending.RockGuid, rock.Guid))
        {
            _logger.LogInformation(
                "EXPERIMENTAL prototype rock dig aborted (rock gone or replaced) zone={ZoneId} pendingGuid={PendingGuid}.",
                zoneId, pending.RockGuid);
            return;
        }

        if (!_zoneManager.TryGetPlayer(pending.PlayerGuid, out var player) ||
            !IsInWildsTestInstance(player) ||
            player.Zone is null ||
            player.Zone.Id != zoneId ||
            GuidHelper.GetPlayerId(player.Guid) != pending.CharacterId)
        {
            _logger.LogInformation(
                "EXPERIMENTAL prototype rock dig aborted (player/session invalid) zone={ZoneId} character={CharacterId}.",
                zoneId, pending.CharacterId);
            return;
        }

        // Defensive: shovel still required at commit (leave clears selection + cancels pending).
        if (!FarmingToolSelection.CanClearRock(player.SelectedFarmToolId))
        {
            _logger.LogInformation(
                "EXPERIMENTAL prototype rock dig aborted (Shovel no longer selected) zone={ZoneId} character={CharacterId}.",
                zoneId, pending.CharacterId);
            return;
        }

        if (!_debugRocksByZoneId.TryRemove(zoneId, out rock) ||
            !FarmingRockDigClearExperiment.MatchesTrackedRock(pending.RockGuid, rock.Guid))
        {
            _logger.LogInformation(
                "EXPERIMENTAL prototype rock dig aborted (rock removed during commit) zone={ZoneId} pendingGuid={PendingGuid}.",
                zoneId, pending.RockGuid);
            return;
        }

        var guid = rock.Guid;
        rock.UpdateEverySecondAction = null;
        rock.Dispose();
        PersistObstacleCleared(pending.CharacterId, FarmingPrototypeConfig.WildsRockObstacleKey);

        _logger.LogInformation(
            "Prototype rock cleared/despawned zone={ZoneId} guid={Guid} character={CharacterId} key={ObstacleKey}.",
            zoneId, guid, pending.CharacterId, FarmingPrototypeConfig.WildsRockObstacleKey);

        ChatHelper.SendSystemMessage(player, "Rock cleared.");
    }

    private void CancelPendingRockClear(int zoneId)
    {
        if (_pendingRockClearsByZoneId.TryRemove(zoneId, out var pending))
        {
            _logger.LogInformation(
                "EXPERIMENTAL prototype rock dig cancelled zone={ZoneId} pendingGuid={PendingGuid} character={CharacterId}.",
                zoneId, pending.RockGuid, pending.CharacterId);
        }

        ClearRockUpdateAction(zoneId);
    }

    private void ClearRockUpdateAction(int zoneId)
    {
        if (_debugRocksByZoneId.TryGetValue(zoneId, out var rock))
            rock.UpdateEverySecondAction = null;
    }

    public bool TrySpawnDebugTree(Player player, out string message)
    {
        if (!IsInWildsTestInstance(player) || player.Zone is null)
        {
            message = "Debug tree only spawns in private Wilds Farm (!farmtest enter).";
            return false;
        }

        return TrySpawnTreeInZone(player.Zone, out message);
    }

    public bool TryRemoveDebugTree(Player player, out string message)
    {
        if (!IsInWildsTestInstance(player) || player.Zone is null)
        {
            message = "Not in private Wilds Farm test instance.";
            return false;
        }

        var zoneId = player.Zone.Id;

        if (!_debugTreesByZoneId.TryRemove(zoneId, out var tree))
        {
            message = "No debug tree to remove.";
            return false;
        }

        tree.Dispose();

        _logger.LogInformation(
            "Prototype tree removed (debug cmd) zone={ZoneId} guid={Guid}.",
            zoneId, tree.Guid);

        message = "Debug tree removed (session only; cleared DB state unchanged).";
        return true;
    }

    /// <summary>
    /// Click-to-clear for prototype tree. Persists cleared for this character + Wilds farm.
    /// </summary>
    private void HandleDebugTreeInteract(Player player)
    {
        if (!IsInWildsTestInstance(player) || player.Zone is null)
            return;

        var zoneId = player.Zone.Id;
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        _logger.LogInformation(
            "Prototype tree interaction received zone={ZoneId} character={CharacterId}.",
            zoneId, characterId);

        if (!_debugTreesByZoneId.TryRemove(zoneId, out var tree))
        {
            _logger.LogWarning(
                "Prototype tree interaction but no tracked tree zone={ZoneId} character={CharacterId}.",
                zoneId, characterId);
            return;
        }

        var guid = tree.Guid;
        tree.Dispose();
        PersistObstacleCleared(characterId, FarmingPrototypeConfig.WildsTreeObstacleKey);

        _logger.LogInformation(
            "Prototype tree cleared/despawned zone={ZoneId} guid={Guid} character={CharacterId} key={ObstacleKey}.",
            zoneId, guid, characterId, FarmingPrototypeConfig.WildsTreeObstacleKey);

        ChatHelper.SendSystemMessage(player, "Tree cleared.");
    }

    public string DescribeObstaclesStatus(Player player)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);
        var cleared = GetClearedObstacleKeys(characterId);

        static string Line(string key, bool isCleared)
            => $"{key}: {(isCleared ? "cleared" : "uncleared")}";

        return
            $"[{FarmingPrototypeConfig.WildsFarmKey}] character={characterId} " +
            $"{Line(FarmingPrototypeConfig.WildsWeedObstacleKey, cleared.Contains(FarmingPrototypeConfig.WildsWeedObstacleKey))}; " +
            $"{Line(FarmingPrototypeConfig.WildsRockObstacleKey, cleared.Contains(FarmingPrototypeConfig.WildsRockObstacleKey))}; " +
            $"{Line(FarmingPrototypeConfig.WildsTreeObstacleKey, cleared.Contains(FarmingPrototypeConfig.WildsTreeObstacleKey))}.";
    }

    public bool TryResetObstacles(Player player, out string message)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        using (var dbContext = _dbContextFactory.CreateDbContext())
        {
            var rows = dbContext.FarmObstacles
                .Where(row =>
                    row.CharacterId == characterId &&
                    row.FarmKey == FarmingPrototypeConfig.WildsFarmKey)
                .ToList();

            if (rows.Count > 0)
            {
                dbContext.FarmObstacles.RemoveRange(rows);
                if (dbContext.SaveChanges() <= 0)
                {
                    message = "Failed to reset obstacle cleared state in DB.";
                    return false;
                }
            }
        }

        _logger.LogInformation(
            "Farm obstacles reset to uncleared character={CharacterId} farm={FarmKey}.",
            characterId, FarmingPrototypeConfig.WildsFarmKey);

        if (IsInWildsTestInstance(player) && player.Zone is not null)
        {
            CancelPendingRockClear(player.Zone.Id);
            SpawnUnclearedObstacles(player.Zone, characterId);
            message =
                "DEBUG: all three obstacles reset to uncleared and re-spawned in current Wilds farm.";
            return true;
        }

        message =
            "DEBUG: all three obstacles reset to uncleared. Re-enter Wilds farm (!farmtest enter) to spawn them.";
        return true;
    }

    /// <summary>
    /// EXPERIMENTAL / debug-only: tunneled Factory OpenToolshed (188/26) with empty type.
    /// Inner payload must be BC001A0000000000. No ListToolsResponse, EquipTool, or NPC spawn.
    /// </summary>
    public void SendExperimentalOpenToolshed(Player player)
    {
        player.SendTunneled(new FactoryPacketOpenToolshed { Type = string.Empty });

        _logger.LogInformation(
            "EXPERIMENTAL FACTORY TOOLS: sent OpenToolshed 188/26 empty-type packet to PlayerGuid={PlayerGuid}",
            player.Guid);
    }

    /// <summary>
    /// EXPERIMENTAL / debug-only: PlayerUpdatePacketSetAnimation with AnimationId=3900003 (farm_dig), Flags=0 (play now).
    /// Private Wilds farm only. No Shovel requirement, no mesh attach, no obstacle/crop/DB changes.
    /// </summary>
    public bool TrySendExperimentalDigAnimation(Player player, out string message)
    {
        if (!IsInWildsTestInstance(player))
        {
            message = FarmingDigAnimExperiment.NotInWildsFarmMessage;
            return false;
        }

        var packet = FarmingDigAnimExperiment.CreatePlayNowPacket(player.Guid);
        player.SendTunneledToVisible(packet, sendToSelf: true);

        _logger.LogInformation(
            "EXPERIMENTAL FARM DIG ANIM: sent PlayerUpdatePacketSetAnimation AnimationId={AnimationId} (farm_dig) Flags={Flags} Unknown={Unknown} to PlayerGuid={PlayerGuid}",
            packet.AnimationId,
            packet.Flags,
            packet.Unknown,
            player.Guid);

        message = FarmingDigAnimExperiment.SuccessMessage;
        return true;
    }

    /// <summary>
    /// Query persisted clears and spawn only uncleared prototype obstacles (no session duplicates).
    /// </summary>
    private void SpawnUnclearedObstacles(IZone zone, ulong characterId)
    {
        var cleared = GetClearedObstacleKeys(characterId);

        if (!cleared.Contains(FarmingPrototypeConfig.WildsWeedObstacleKey))
        {
            if (TrySpawnWeedInZone(zone, out var weedMsg))
                _logger.LogInformation("Auto-spawn weed: {Message}", weedMsg);
            else
                _logger.LogWarning("Auto-spawn weed skipped: {Message}", weedMsg);
        }

        if (!cleared.Contains(FarmingPrototypeConfig.WildsRockObstacleKey))
        {
            if (TrySpawnRockInZone(zone, out var rockMsg))
                _logger.LogInformation("Auto-spawn rock: {Message}", rockMsg);
            else
                _logger.LogWarning("Auto-spawn rock skipped: {Message}", rockMsg);
        }

        if (!cleared.Contains(FarmingPrototypeConfig.WildsTreeObstacleKey))
        {
            if (TrySpawnTreeInZone(zone, out var treeMsg))
                _logger.LogInformation("Auto-spawn tree: {Message}", treeMsg);
            else
                _logger.LogWarning("Auto-spawn tree skipped: {Message}", treeMsg);
        }
    }

    private HashSet<string> GetClearedObstacleKeys(ulong characterId)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        return dbContext.FarmObstacles
            .Where(row =>
                row.CharacterId == characterId &&
                row.FarmKey == FarmingPrototypeConfig.WildsFarmKey)
            .Select(row => row.ObstacleKey)
            .ToHashSet(StringComparer.Ordinal);
    }

    private void PersistObstacleCleared(ulong characterId, string obstacleKey)
    {
        var now = DateTimeOffset.UtcNow;

        using var dbContext = _dbContextFactory.CreateDbContext();
        var row = dbContext.FarmObstacles.SingleOrDefault(entry =>
            entry.CharacterId == characterId &&
            entry.FarmKey == FarmingPrototypeConfig.WildsFarmKey &&
            entry.ObstacleKey == obstacleKey);

        if (row is null)
        {
            dbContext.FarmObstacles.Add(new DbFarmObstacle
            {
                CharacterId = characterId,
                FarmKey = FarmingPrototypeConfig.WildsFarmKey,
                ObstacleKey = obstacleKey,
                ClearedAtUtc = now
            });
        }
        else
        {
            row.ClearedAtUtc = now;
        }

        if (dbContext.SaveChanges() <= 0)
        {
            _logger.LogError(
                "Failed to persist obstacle clear character={CharacterId} farm={FarmKey} key={ObstacleKey}.",
                characterId, FarmingPrototypeConfig.WildsFarmKey, obstacleKey);
            return;
        }

        _logger.LogInformation(
            "Persisted obstacle clear character={CharacterId} farm={FarmKey} key={ObstacleKey} at {ClearedAt:u}.",
            characterId, FarmingPrototypeConfig.WildsFarmKey, obstacleKey, now);
    }

    private bool TrySpawnWeedInZone(IZone zone, out string message)
    {
        if (!_resourceManager.Models.ContainsKey(FarmingPrototypeConfig.DebugWeedModelId))
        {
            message = $"Weed model {FarmingPrototypeConfig.DebugWeedModelId} missing from Models.txt.";
            return false;
        }

        if (_debugWeedsByZoneId.ContainsKey(zone.Id))
        {
            message =
                $"Weed already present ({FarmingPrototypeConfig.WildsWeedObstacleKey}, " +
                $"model {FarmingPrototypeConfig.DebugWeedModelId}). No duplicate.";
            return false;
        }

        if (!zone.TryCreateNpc(null, out var weed))
        {
            message = "Failed to create weed NPC.";
            return false;
        }

        weed.Name = FarmingPrototypeConfig.DebugWeedNpcName;
        weed.ModelId = FarmingPrototypeConfig.DebugWeedModelId;
        weed.Scale = 1f;
        weed.Visible = true;
        weed.IsInteractable = true;
        weed.InteractRange = FarmingPrototypeConfig.InteractRange;
        weed.CursorId = FarmingPrototypeConfig.CursorId;
        weed.HideNamePlate = false;
        weed.InteractAction = HandleDebugWeedInteract;

        var heading = FarmingPrototypeConfig.DebugWeedHeading;
        var rotation = new Quaternion(MathF.Sin(heading), 0f, MathF.Cos(heading), 0f);
        weed.UpdatePosition(FarmingPrototypeConfig.DebugWeedPosition, rotation);

        _debugWeedsByZoneId[zone.Id] = weed;

        _logger.LogInformation(
            "Prototype weed spawned key={ObstacleKey} zone={ZoneId} guid={Guid} model={ModelId} at ({X:0.##}, {Y:0.##}, {Z:0.##}).",
            FarmingPrototypeConfig.WildsWeedObstacleKey, zone.Id, weed.Guid,
            FarmingPrototypeConfig.DebugWeedModelId,
            FarmingPrototypeConfig.DebugWeedX, FarmingPrototypeConfig.DebugWeedY, FarmingPrototypeConfig.DebugWeedZ);

        message =
            $"Weed spawned ({FarmingPrototypeConfig.WildsWeedObstacleKey}, model {FarmingPrototypeConfig.DebugWeedModelId}) at " +
            $"[{FarmingPrototypeConfig.DebugWeedX}, {FarmingPrototypeConfig.DebugWeedY}, {FarmingPrototypeConfig.DebugWeedZ}]. Click to clear.";
        return true;
    }

    private bool TrySpawnRockInZone(IZone zone, out string message)
    {
        if (!_resourceManager.Models.ContainsKey(FarmingPrototypeConfig.DebugRockModelId))
        {
            message = $"Rock model {FarmingPrototypeConfig.DebugRockModelId} missing from Models.txt.";
            return false;
        }

        if (_debugRocksByZoneId.ContainsKey(zone.Id))
        {
            message =
                $"Rock already present ({FarmingPrototypeConfig.WildsRockObstacleKey}, " +
                $"model {FarmingPrototypeConfig.DebugRockModelId}). No duplicate.";
            return false;
        }

        if (!zone.TryCreateNpc(null, out var rock))
        {
            message = "Failed to create rock NPC.";
            return false;
        }

        rock.Name = FarmingPrototypeConfig.DebugRockNpcName;
        rock.ModelId = FarmingPrototypeConfig.DebugRockModelId;
        rock.Scale = 1f;
        rock.Visible = true;
        rock.IsInteractable = true;
        rock.InteractRange = FarmingPrototypeConfig.InteractRange;
        rock.CursorId = FarmingPrototypeConfig.CursorId;
        rock.HideNamePlate = false;
        rock.InteractAction = HandleDebugRockInteract;

        var heading = FarmingPrototypeConfig.DebugRockHeading;
        var rotation = new Quaternion(MathF.Sin(heading), 0f, MathF.Cos(heading), 0f);
        rock.UpdatePosition(FarmingPrototypeConfig.DebugRockPosition, rotation);

        _debugRocksByZoneId[zone.Id] = rock;

        _logger.LogInformation(
            "Prototype rock spawned key={ObstacleKey} zone={ZoneId} guid={Guid} model={ModelId} at ({X:0.##}, {Y:0.##}, {Z:0.##}).",
            FarmingPrototypeConfig.WildsRockObstacleKey, zone.Id, rock.Guid,
            FarmingPrototypeConfig.DebugRockModelId,
            FarmingPrototypeConfig.DebugRockX, FarmingPrototypeConfig.DebugRockY, FarmingPrototypeConfig.DebugRockZ);

        message =
            $"Rock spawned ({FarmingPrototypeConfig.WildsRockObstacleKey}, model {FarmingPrototypeConfig.DebugRockModelId}) at " +
            $"[{FarmingPrototypeConfig.DebugRockX}, {FarmingPrototypeConfig.DebugRockY}, {FarmingPrototypeConfig.DebugRockZ}]. Click to clear.";
        return true;
    }

    private bool TrySpawnTreeInZone(IZone zone, out string message)
    {
        if (!_resourceManager.Models.ContainsKey(FarmingPrototypeConfig.DebugTreeModelId))
        {
            message = $"Tree model {FarmingPrototypeConfig.DebugTreeModelId} missing from Models.txt.";
            return false;
        }

        if (_debugTreesByZoneId.ContainsKey(zone.Id))
        {
            message =
                $"Tree already present ({FarmingPrototypeConfig.WildsTreeObstacleKey}, " +
                $"model {FarmingPrototypeConfig.DebugTreeModelId}). No duplicate.";
            return false;
        }

        if (!zone.TryCreateNpc(null, out var tree))
        {
            message = "Failed to create tree NPC.";
            return false;
        }

        tree.Name = FarmingPrototypeConfig.DebugTreeNpcName;
        tree.ModelId = FarmingPrototypeConfig.DebugTreeModelId;
        tree.Scale = 1f;
        tree.Visible = true;
        tree.IsInteractable = true;
        tree.InteractRange = FarmingPrototypeConfig.InteractRange;
        tree.CursorId = FarmingPrototypeConfig.CursorId;
        tree.HideNamePlate = false;
        tree.InteractAction = HandleDebugTreeInteract;

        var heading = FarmingPrototypeConfig.DebugTreeHeading;
        var rotation = new Quaternion(MathF.Sin(heading), 0f, MathF.Cos(heading), 0f);
        tree.UpdatePosition(FarmingPrototypeConfig.DebugTreePosition, rotation);

        _debugTreesByZoneId[zone.Id] = tree;

        _logger.LogInformation(
            "Prototype tree spawned key={ObstacleKey} zone={ZoneId} guid={Guid} model={ModelId} at ({X:0.##}, {Y:0.##}, {Z:0.##}).",
            FarmingPrototypeConfig.WildsTreeObstacleKey, zone.Id, tree.Guid,
            FarmingPrototypeConfig.DebugTreeModelId,
            FarmingPrototypeConfig.DebugTreeX, FarmingPrototypeConfig.DebugTreeY, FarmingPrototypeConfig.DebugTreeZ);

        message =
            $"Tree spawned ({FarmingPrototypeConfig.WildsTreeObstacleKey}, model {FarmingPrototypeConfig.DebugTreeModelId}) at " +
            $"[{FarmingPrototypeConfig.DebugTreeX}, {FarmingPrototypeConfig.DebugTreeY}, {FarmingPrototypeConfig.DebugTreeZ}]. Click to clear.";
        return true;
    }

    public CropPlotStage GetStage(Player player)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);
        var plotKey = ResolvePlotKey(player);

        using var dbContext = _dbContextFactory.CreateDbContext();
        var plot = dbContext.CropPlots.SingleOrDefault(row =>
            row.CharacterId == characterId && row.PlotKey == plotKey);

        return ResolveStage(plot, DateTimeOffset.UtcNow);
    }

    public string DescribeStatus(Player player)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);
        var plotKey = ResolvePlotKey(player);

        using var dbContext = _dbContextFactory.CreateDbContext();
        var plot = dbContext.CropPlots.SingleOrDefault(row =>
            row.CharacterId == characterId && row.PlotKey == plotKey);

        var now = DateTimeOffset.UtcNow;
        var stage = ResolveStage(plot, now);
        var visual = ResolveVisualStage(plot, now);
        var where = IsInWildsTestInstance(player) ? "Wilds private test" : "Farnum harness";
        var runtime = ResolvePlotRuntime(player);
        var cropModel = runtime?.Crop?.ModelId ?? 0;
        var cropGuid = runtime?.Crop?.Guid ?? 0UL;

        return stage switch
        {
            CropPlotStage.Empty =>
                $"[{where}] Plot {plotKey}: Empty. Visual={visual}/{FarmingPrototypeConfig.EmptyModelId}. Interact while holding seed {FarmingPrototypeConfig.SeedDefinitionId}.",
            CropPlotStage.Growing =>
                $"[{where}] Plot {plotKey}: Growing. Visual={visual} model={FarmingPrototypeConfig.ModelIdForVisual(visual)} cropGuid={cropGuid}. Ready in {SecondsUntilHarvestable(plot!, now)}s.",
            CropPlotStage.Harvestable =>
                $"[{where}] Plot {plotKey}: Harvestable. Visual={visual} model={FarmingPrototypeConfig.HarvestableModelId} cropGuid={cropGuid} cropModel={cropModel}.",
            _ => $"[{where}] Plot {plotKey}: Unknown."
        };
    }

    public void HandleInteract(Player player)
    {
        var plotKey = ResolvePlotKey(player);
        var runtime = ResolvePlotRuntime(player);

        if (runtime is null)
        {
            ChatHelper.SendSystemMessage(player,
                IsInWildsTestInstance(player)
                    ? "Wilds Farm test plot is missing."
                    : "Farming prototype plot is not spawned. Try !farmtest enter for Wilds, or check Farnum harness.");
            return;
        }

        var characterId = GuidHelper.GetPlayerId(player.Guid);
        var now = DateTimeOffset.UtcNow;

        using var dbContext = _dbContextFactory.CreateDbContext();
        var plot = dbContext.CropPlots.SingleOrDefault(row =>
            row.CharacterId == characterId && row.PlotKey == plotKey);

        var stage = ResolveStage(plot, now);
        // Resync client visual from DB every interact (fixes prior silent ModelId updates).
        ApplyPlotVisual(runtime, ResolveVisualStage(plot, now), force: true, reason: "interact-resync");

        switch (stage)
        {
            case CropPlotStage.Empty:
                TryPlant(player, dbContext, plot, characterId, now, plotKey, runtime);
                break;
            case CropPlotStage.Growing:
                ChatHelper.SendSystemMessage(player,
                    $"Your crop is growing. Ready in {SecondsUntilHarvestable(plot!, now)}s.");
                break;
            case CropPlotStage.Harvestable:
                TryHarvest(player, dbContext, plot!, plotKey, runtime);
                break;
        }
    }

    public bool TryForceGrow(Player player)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);
        var plotKey = ResolvePlotKey(player);

        using var dbContext = _dbContextFactory.CreateDbContext();
        var plot = dbContext.CropPlots.SingleOrDefault(row =>
            row.CharacterId == characterId && row.PlotKey == plotKey);

        if (plot?.PlantedAtUtc is null)
            return false;

        plot.PlantedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-FarmingPrototypeConfig.GrowthSeconds - 1);

        if (dbContext.SaveChanges() <= 0)
            return false;

        var runtime = ResolvePlotRuntime(player);
        if (runtime is not null)
        {
            ClearGrowthVisualTimer(runtime);
            ApplyPlotVisual(runtime, CropVisualStage.Ready, force: true, reason: "force-grow");
        }

        _logger.LogInformation(
            "Farming force-grow plot={PlotKey} character={CharacterId} visual=Ready model={ModelId} ({Asset}).",
            plotKey, characterId, FarmingPrototypeConfig.HarvestableModelId,
            FarmingPrototypeConfig.AssetNameForVisual(CropVisualStage.Ready));
        return true;
    }

    public bool TryReset(Player player)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);
        var plotKey = ResolvePlotKey(player);

        using var dbContext = _dbContextFactory.CreateDbContext();
        var plot = dbContext.CropPlots.SingleOrDefault(row =>
            row.CharacterId == characterId && row.PlotKey == plotKey);

        var runtime = ResolvePlotRuntime(player);

        if (plot is null)
        {
            if (runtime is not null)
            {
                ClearGrowthVisualTimer(runtime);
                ApplyPlotVisual(runtime, CropVisualStage.Empty, force: true, reason: "reset-empty");
            }
            return true;
        }

        dbContext.CropPlots.Remove(plot);

        if (dbContext.SaveChanges() <= 0)
            return false;

        if (runtime is not null)
        {
            ClearGrowthVisualTimer(runtime);
            ApplyPlotVisual(runtime, CropVisualStage.Empty, force: true, reason: "reset");
        }

        _logger.LogInformation(
            "Farming reset plot={PlotKey} character={CharacterId} visual=Empty foundation={ModelId}.",
            plotKey, characterId, FarmingPrototypeConfig.EmptyModelId);
        return true;
    }

    public void TeleportToPlot(Player player)
    {
        var heading = FarmingPrototypeConfig.Heading;
        var rotation = new Quaternion(MathF.Sin(heading), 0f, MathF.Cos(heading), 0f);
        var position = FarmingPrototypeConfig.PlotPosition + new Vector4(2f, 0f, 0f, 0f);

        // Farnum harness only — does not enter Wilds private instance.
        if (player.Zone?.DefinitionId == FarmingPrototypeConfig.ZoneDefinitionId)
        {
            player.TeleportToZone(player.Zone, position, rotation, forceEvenIfSameZone: true);
            return;
        }

        player.TeleportToZone(_zoneManager.StartingZone, position, rotation);
    }

    private string ResolvePlotKey(Player player)
        => IsInWildsTestInstance(player)
            ? FarmingPrototypeConfig.WildsPlotKey
            : FarmingPrototypeConfig.PlotKey;

    private PlotRuntime? ResolvePlotRuntime(Player player)
    {
        if (IsInWildsTestInstance(player))
        {
            if (_wildsPlotsByZoneId.TryGetValue(player.Zone.Id, out var runtime))
                return runtime;

            // Fallback: find foundation by stable name (foundation ModelId never changes).
            var foundation = player.Zone.Npcs.FirstOrDefault(npc =>
                npc.Name == FarmingPrototypeConfig.FoundationNpcName &&
                npc.ModelId == FarmingPrototypeConfig.EmptyModelId);

            if (foundation is null)
                return null;

            var recovered = new PlotRuntime
            {
                PlotKey = FarmingPrototypeConfig.WildsPlotKey,
                Foundation = foundation,
                Crop = player.Zone.Npcs.FirstOrDefault(npc =>
                    npc.Name == FarmingPrototypeConfig.CropNpcName)
            };
            _wildsPlotsByZoneId[player.Zone.Id] = recovered;
            return recovered;
        }

        return _farnumPlot;
    }

    private bool ModelsAvailable()
        => _resourceManager.Models.ContainsKey(FarmingPrototypeConfig.EmptyModelId) &&
           _resourceManager.Models.ContainsKey(FarmingPrototypeConfig.PlantedModelId) &&
           _resourceManager.Models.ContainsKey(FarmingPrototypeConfig.GrowingModelId) &&
           _resourceManager.Models.ContainsKey(FarmingPrototypeConfig.HarvestableModelId);

    private bool TryCreatePlotRuntime(
        IZone zone,
        Vector4 position,
        float heading,
        string plotKey,
        out PlotRuntime runtime)
    {
        runtime = null!;

        if (!zone.TryCreateNpc(null, out var foundation))
            return false;

        foundation.Name = FarmingPrototypeConfig.FoundationNpcName;
        foundation.ModelId = FarmingPrototypeConfig.EmptyModelId;
        foundation.Scale = 1f;
        foundation.Visible = true;
        foundation.IsInteractable = true;
        foundation.InteractRange = FarmingPrototypeConfig.InteractRange;
        foundation.CursorId = FarmingPrototypeConfig.CursorId;
        foundation.HideNamePlate = false;
        foundation.InteractAction = HandleInteract;

        var rotation = new Quaternion(MathF.Sin(heading), 0f, MathF.Cos(heading), 0f);
        foundation.UpdatePosition(position, rotation);

        runtime = new PlotRuntime
        {
            PlotKey = plotKey,
            Foundation = foundation,
            LastPushedVisual = CropVisualStage.Empty
        };

        _logger.LogInformation(
            "Farming plot foundation created plotKey={PlotKey} zone={Zone} guid={Guid} model={ModelId} ({X:0.##}, {Y:0.##}, {Z:0.##}).",
            plotKey, zone.Name, foundation.Guid, FarmingPrototypeConfig.EmptyModelId,
            position.X, position.Y, position.Z);

        return true;
    }

    private void TryPlant(
        Player player,
        DatabaseContext dbContext,
        DbCropPlot? plot,
        ulong characterId,
        DateTimeOffset now,
        string plotKey,
        PlotRuntime runtime)
    {
        if (!TryStageSeedConsume(player, dbContext, out var clientItem, out var deleteItem, out var remainingCount))
        {
            ChatHelper.SendSystemMessage(player,
                $"You need a Bumbleberry Seed ({FarmingPrototypeConfig.SeedDefinitionId}) to plant. Try !farmtest seed.");
            return;
        }

        if (plot is null)
        {
            plot = new DbCropPlot
            {
                PlotKey = plotKey,
                CharacterId = characterId
            };
            dbContext.CropPlots.Add(plot);
        }

        plot.SeedDefinitionId = FarmingPrototypeConfig.SeedDefinitionId;
        plot.PlantedAtUtc = now;

        if (dbContext.SaveChanges() <= 0)
        {
            ChatHelper.SendSystemMessage(player, "Failed to save planted crop.");
            return;
        }

        ApplySeedConsumeToClient(player, clientItem!, deleteItem, remainingCount);

        ApplyPlotVisual(runtime, CropVisualStage.Planted, force: true, reason: "plant");
        ArmGrowthVisualTimer(runtime, now);
        _logger.LogInformation(
            "Farming plant plot={PlotKey} character={CharacterId} seed={SeedId} plantedAt={PlantedAt:u} visual=Planted model={ModelId} ({Asset}).",
            plotKey, characterId, FarmingPrototypeConfig.SeedDefinitionId, now,
            FarmingPrototypeConfig.PlantedModelId,
            FarmingPrototypeConfig.AssetNameForVisual(CropVisualStage.Planted));
        ChatHelper.SendSystemMessage(player,
            $"Planted! Ready in {FarmingPrototypeConfig.GrowthSeconds}s.");
    }

    private void TryHarvest(
        Player player,
        DatabaseContext dbContext,
        DbCropPlot plot,
        string plotKey,
        PlotRuntime runtime)
    {
        var characterId = plot.CharacterId;
        var sourceGuid = runtime.Foundation.Guid;

        // Grant first; only clear crop after successful grant (preserve Ready on failure).
        if (!_rewardManager.TryGrantItem(player, FarmingPrototypeConfig.HarvestDefinitionId, tint: 0, quantity: 1, sourceGuid))
        {
            ChatHelper.SendSystemMessage(player, "Harvest failed to grant crop item. Plot left Harvestable.");
            _logger.LogError(
                "Farming harvest grant failed plot={PlotKey} character={CharacterId} item={ItemId}; crop preserved.",
                plotKey, characterId, FarmingPrototypeConfig.HarvestDefinitionId);
            return;
        }

        dbContext.CropPlots.Remove(plot);

        if (dbContext.SaveChanges() <= 0)
        {
            ChatHelper.SendSystemMessage(player,
                "Harvest item granted but failed to clear plot DB row — contact admin / !farmtest reset.");
            _logger.LogError(
                "Farming harvest DB clear failed after grant plot={PlotKey} character={CharacterId}.",
                plotKey, characterId);
            return;
        }

        ClearGrowthVisualTimer(runtime);
        ApplyPlotVisual(runtime, CropVisualStage.Empty, force: true, reason: "harvest");
        _logger.LogInformation(
            "Farming harvest plot={PlotKey} character={CharacterId} item={ItemId} visual=Empty.",
            plotKey, characterId, FarmingPrototypeConfig.HarvestDefinitionId);
        ChatHelper.SendSystemMessage(player, "Harvested! Plot is empty again.");
    }

    private bool TryStageSeedConsume(
        Player player,
        DatabaseContext dbContext,
        out ClientItem? clientItem,
        out bool deleteItem,
        out int remainingCount)
    {
        clientItem = null;
        deleteItem = false;
        remainingCount = 0;

        var characterId = GuidHelper.GetPlayerId(player.Guid);
        var inventoryItem = player.Items.FirstOrDefault(item =>
            item.Definition == FarmingPrototypeConfig.SeedDefinitionId);

        if (inventoryItem is null || inventoryItem.Count < 1)
            return false;

        clientItem = inventoryItem;

        var dbCharacter = dbContext.Characters
            .Include(character => character.Items)
            .SingleOrDefault(character => character.Id == characterId);

        if (dbCharacter is null)
            return false;

        var inventoryItemId = inventoryItem.Id;
        var dbItem = dbCharacter.Items.SingleOrDefault(item =>
            item.Id == inventoryItemId && item.Definition == FarmingPrototypeConfig.SeedDefinitionId);

        if (dbItem is null || dbItem.Count < 1)
            return false;

        deleteItem = dbItem.Count == 1;

        if (deleteItem)
        {
            dbContext.Items.Remove(dbItem);
            remainingCount = 0;
        }
        else
        {
            dbItem.Count -= 1;
            remainingCount = dbItem.Count;
        }

        return true;
    }

    private static void ApplySeedConsumeToClient(Player player, ClientItem clientItem, bool deleteItem, int remainingCount)
    {
        if (deleteItem)
        {
            player.Items.Remove(clientItem);
            player.SendTunneled(new ClientUpdatePacketItemDelete { ItemGuid = clientItem.Id });
            return;
        }

        clientItem.Count = remainingCount;
        player.SendTunneled(new ClientUpdatePacketItemUpdate
        {
            ItemGuid = clientItem.Id,
            Count = clientItem.Count
        });
    }

    /// <summary>
    /// Restore crop overlay from DbCropPlot. Foundation stays wilds dirt (3416).
    /// </summary>
    private void RestorePlotVisualFromDb(PlotRuntime runtime, ulong characterId)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var plot = dbContext.CropPlots.SingleOrDefault(row =>
            row.CharacterId == characterId && row.PlotKey == runtime.PlotKey);

        var now = DateTimeOffset.UtcNow;
        var visual = ResolveVisualStage(plot, now);
        ApplyPlotVisual(runtime, visual, force: true, reason: "restore-db");

        if (plot?.PlantedAtUtc is not null && ResolveStage(plot, now) == CropPlotStage.Growing)
            ArmGrowthVisualTimer(runtime, plot.PlantedAtUtc.Value);
        else
            ClearGrowthVisualTimer(runtime);

        _logger.LogInformation(
            "Farming restore plot={PlotKey} character={CharacterId} persisted={Stage} visual={Visual} model={ModelId} ({Asset}).",
            runtime.PlotKey, characterId, ResolveStage(plot, now), visual,
            FarmingPrototypeConfig.ModelIdForVisual(visual),
            FarmingPrototypeConfig.AssetNameForVisual(visual));
    }

    /// <summary>
    /// Drive Planted → Growing → Ready client visuals from planted UTC while player is in farm.
    /// </summary>
    private void ArmGrowthVisualTimer(PlotRuntime runtime, DateTimeOffset plantedAtUtc)
    {
        runtime.Foundation.UpdateEverySecondAction = () =>
        {
            var now = DateTimeOffset.UtcNow;
            var visual = ResolveVisualStageFromPlantedAt(plantedAtUtc, now);

            if (visual != runtime.LastPushedVisual)
            {
                ApplyPlotVisual(runtime, visual, force: true, reason: "timer");
                _logger.LogInformation(
                    "Farming timer visual plot={PlotKey} visual={Visual} model={ModelId} ({Asset}) foundationGuid={Guid}.",
                    runtime.PlotKey, visual,
                    FarmingPrototypeConfig.ModelIdForVisual(visual),
                    FarmingPrototypeConfig.AssetNameForVisual(visual),
                    runtime.Foundation.Guid);
            }

            if (visual == CropVisualStage.Ready)
                runtime.Foundation.UpdateEverySecondAction = null;
        };
    }

    private static void ClearGrowthVisualTimer(PlotRuntime runtime)
        => runtime.Foundation.UpdateEverySecondAction = null;

    /// <summary>
    /// Mechanism: dirt foundation NPC stays ModelId 3416; crop blueprint is a second NPC on top.
    /// Evidence: Models.txt labels foundations vs blueprints separately; ReplaceBaseModel unimplemented.
    /// Swap uses RemovePlayer + AddNpc (proven NPC spawn path).
    /// </summary>
    private void ApplyPlotVisual(PlotRuntime runtime, CropVisualStage visual, bool force, string reason)
    {
        // Foundation never leaves wilds dirt.
        if (runtime.Foundation.ModelId != FarmingPrototypeConfig.EmptyModelId)
        {
            ResyncNpcModel(runtime.Foundation, FarmingPrototypeConfig.EmptyModelId);
            _logger.LogWarning(
                "Farming foundation model corrected plot={PlotKey} -> {ModelId} reason={Reason}.",
                runtime.PlotKey, FarmingPrototypeConfig.EmptyModelId, reason);
        }

        if (visual == CropVisualStage.Empty)
        {
            DestroyCropNpc(runtime);
            runtime.LastPushedVisual = CropVisualStage.Empty;
            _logger.LogInformation(
                "Farming visual plot={PlotKey} visual=Empty foundation={ModelId} crop=none reason={Reason}.",
                runtime.PlotKey, FarmingPrototypeConfig.EmptyModelId, reason);
            return;
        }

        var modelId = FarmingPrototypeConfig.ModelIdForVisual(visual);

        if (runtime.Crop is null)
        {
            if (!TryCreateCropNpc(runtime, modelId))
            {
                _logger.LogError(
                    "Farming visual failed to create crop NPC plot={PlotKey} visual={Visual} model={ModelId} reason={Reason}.",
                    runtime.PlotKey, visual, modelId, reason);
                return;
            }
        }
        else if (force || runtime.Crop.ModelId != modelId || runtime.LastPushedVisual != visual)
        {
            ResyncNpcModel(runtime.Crop, modelId);
        }

        runtime.LastPushedVisual = visual;

        _logger.LogInformation(
            "Farming visual plot={PlotKey} visual={Visual} model={ModelId} ({Asset}) cropGuid={CropGuid} foundationGuid={FoundationGuid} reason={Reason}.",
            runtime.PlotKey, visual, modelId, FarmingPrototypeConfig.AssetNameForVisual(visual),
            runtime.Crop?.Guid, runtime.Foundation.Guid, reason);
    }

    private bool TryCreateCropNpc(PlotRuntime runtime, int modelId)
    {
        var foundation = runtime.Foundation;
        var zone = foundation.Zone;

        if (!zone.TryCreateNpc(null, out var crop))
            return false;

        crop.Name = FarmingPrototypeConfig.CropNpcName;
        crop.ModelId = modelId;
        crop.Scale = 1f;
        crop.Visible = true;
        crop.IsInteractable = true;
        crop.InteractRange = FarmingPrototypeConfig.InteractRange;
        crop.CursorId = FarmingPrototypeConfig.CursorId;
        crop.HideNamePlate = true;
        crop.InteractAction = HandleInteract;

        crop.UpdatePosition(foundation.Position, foundation.Rotation);
        runtime.Crop = crop;

        _logger.LogInformation(
            "Farming crop NPC created plot={PlotKey} guid={Guid} model={ModelId}.",
            runtime.PlotKey, crop.Guid, modelId);

        return true;
    }

    private void DestroyCropNpc(PlotRuntime runtime)
    {
        var crop = runtime.Crop;
        if (crop is null)
            return;

        runtime.Crop = null;
        ClearGrowthVisualTimerOnNpc(crop);
        crop.Dispose();

        _logger.LogInformation(
            "Farming crop NPC destroyed plot={PlotKey} guid={Guid}.",
            runtime.PlotKey, crop.Guid);
    }

    private static void ClearGrowthVisualTimerOnNpc(Npc npc)
        => npc.UpdateEverySecondAction = null;

    /// <summary>
    /// Re-send RemovePlayer + AddNpc so clients pick up a new ModelId.
    /// Includes all zone players (not only VisiblePlayers) to avoid silent server-only updates.
    /// </summary>
    private static void ResyncNpcModel(Npc npc, int modelId)
    {
        var recipients = GatherRecipients(npc);

        foreach (var visible in recipients.Values)
            visible.OnRemoveVisibleNpcs([npc]);

        // Keep NPC↔player visibility maps coherent with zone-tile expectations.
        foreach (var visible in recipients.Values)
            npc.OnRemoveVisiblePlayers(visible);

        npc.ModelId = modelId;

        foreach (var visible in recipients.Values)
        {
            visible.OnAddVisibleNpcs([npc]);
            npc.OnAddVisiblePlayers(visible);
        }
    }

    private static Dictionary<ulong, Player> GatherRecipients(Npc npc)
    {
        var recipients = new Dictionary<ulong, Player>();

        foreach (var visible in npc.VisiblePlayers.Values)
            recipients[visible.Guid] = visible;

        foreach (var player in npc.Zone.Players)
        {
            recipients[player.Guid] = player;
        }

        return recipients;
    }

    private static CropPlotStage ResolveStage(DbCropPlot? plot, DateTimeOffset now)
    {
        if (plot?.PlantedAtUtc is null)
            return CropPlotStage.Empty;

        var readyAt = plot.PlantedAtUtc.Value.AddSeconds(FarmingPrototypeConfig.GrowthSeconds);
        return now >= readyAt ? CropPlotStage.Harvestable : CropPlotStage.Growing;
    }

    private static CropVisualStage ResolveVisualStage(DbCropPlot? plot, DateTimeOffset now)
    {
        if (plot?.PlantedAtUtc is null)
            return CropVisualStage.Empty;

        return ResolveVisualStageFromPlantedAt(plot.PlantedAtUtc.Value, now);
    }

    private static CropVisualStage ResolveVisualStageFromPlantedAt(DateTimeOffset plantedAtUtc, DateTimeOffset now)
    {
        var elapsed = (now - plantedAtUtc).TotalSeconds;

        if (elapsed >= FarmingPrototypeConfig.GrowthSeconds)
            return CropVisualStage.Ready;

        if (elapsed >= FarmingPrototypeConfig.PlantedVisualSeconds)
            return CropVisualStage.Growing;

        return CropVisualStage.Planted;
    }

    private static int SecondsUntilHarvestable(DbCropPlot plot, DateTimeOffset now)
    {
        var readyAt = plot.PlantedAtUtc!.Value.AddSeconds(FarmingPrototypeConfig.GrowthSeconds);
        var remaining = (int)Math.Ceiling((readyAt - now).TotalSeconds);
        return Math.Max(0, remaining);
    }
}
