using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseEncounterPacketHandler
{
    private const int BanditActivityId = 29;
    private const int BanditZoneDefinitionId = 29;
    private const string BanditZoneName = "sg_bandit_hideout";
    private const string ExitTestLogPrefix = "EXPERIMENTAL BANDIT EXIT TEST:";
    private const string DebugExitLogPrefix = "EXPERIMENTAL BANDIT DEBUG EXIT:";

    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static GatewayServer _gatewayServer = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(BaseEncounterPacketHandler));
        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _gatewayServer = serviceProvider.GetRequiredService<GatewayServer>();

        LeaveBanditChatCommand.TryHandleDebugLeave = TryHandleDebugLeaveFromChat;
    }

    /// <summary>
    /// LOCAL DEBUG: !leavebandit / /leavebandit — same return path as 41/109, zone-gated.
    /// </summary>
    public static bool TryHandleDebugLeaveFromChat(Player invoker)
    {
        if (!_gatewayServer.TryGetConnectionForPlayer(invoker, out var connection))
        {
            _logger.LogWarning(
                "{Prefix} command invoked but no GatewayConnection found for player Guid={Guid} Name={Name}",
                DebugExitLogPrefix, invoker.Guid, invoker.Name);
            ChatHelper.SendSystemMessage(invoker, "leavebandit failed: no connection.");
            return true;
        }

        return TryDebugLeaveBandit(connection);
    }

    public static bool TryDebugLeaveBandit(GatewayConnection connection)
    {
        var player = connection.Player;
        var currentZone = player?.Zone?.Name ?? "<null>";

        _logger.LogInformation(
            "{Prefix} command invoked. connection={Connection} CurrentZone={CurrentZone} ZoneId={ZoneId}",
            DebugExitLogPrefix, connection, currentZone, player?.Zone?.Id);

        if (player is null)
        {
            return true;
        }

        if (!string.Equals(player.Zone?.Name, BanditZoneName, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "{Prefix} rejected — player not in {BanditZone}. CurrentZone={CurrentZone}",
                DebugExitLogPrefix, BanditZoneName, currentZone);
            ChatHelper.SendSystemMessage(player, $"leavebandit only works in {BanditZoneName} (you are in {currentZone}).");
            return true;
        }

        var returned = TryReturnFromBanditHideout(connection, DebugExitLogPrefix, exitPacketH1: null, exitPacketH2: null);
        ChatHelper.SendSystemMessage(player, returned
            ? "leavebandit: returning to overworld…"
            : "leavebandit: return failed (see Gateway log).");
        return true;
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader, int serverType)
    {
        if (!reader.TryRead(out short message))
        {
            _logger.LogWarning("Truncated encounter message: ServerType={ServerType}, payload={Payload}", serverType, Convert.ToHexString(reader.Span));
            return false;
        }

        if (message == EncounterParticipantRequestExitPacket.OpCode)
            return HandleRequestExit(connection, reader, serverType);

        if (message != EncounterParticipantRequestEntrancePacket.OpCode)
        {
            _logger.LogWarning("Unhandled encounter request: family=41, message={Message}, ServerType={ServerType}, connection={Connection}, payload={Payload}",
                message, serverType, connection, Convert.ToHexString(reader.Span));
            return false;
        }

        if (!EncounterParticipantRequestEntrancePacket.TryDeserialize(reader.Span, out var packet))
        {
            _logger.LogWarning("EncounterParticipantRequestEntrancePacket does not match the archived 20-byte layout: ServerType={ServerType}, payload={Payload}. No response sent.",
                serverType, Convert.ToHexString(reader.Span));
            return false;
        }

        var suffixGuid = packet.UnknownData.Length == 8
            ? BitConverter.ToUInt64(packet.UnknownData, 0)
            : 0UL;
        var playerGuid = connection.Player?.Guid ?? 0UL;

        _logger.LogInformation(
            "Received EncounterParticipantRequestEntrancePacket (GO): family=41, message=108, H1={HeaderValue1} (0x{H1Hex}), H2={HeaderValue2}, SuffixGuid={SuffixGuid} (0x{SuffixGuidHex}), PlayerGuid={PlayerGuid}, UnknownData={UnknownData}, ServerType={ServerType}, connection={Connection}, payload={Payload}",
            packet.HeaderValue1, packet.HeaderValue1.ToString("X8"), packet.HeaderValue2,
            suffixGuid, suffixGuid.ToString("X16"), playerGuid, Convert.ToHexString(packet.UnknownData),
            serverType, connection, Convert.ToHexString(reader.Span));

        if (suffixGuid != 0 && playerGuid != 0 && suffixGuid != playerGuid)
        {
            _logger.LogWarning(
                "EXPERIMENTAL Bandit 41/108 SuffixGuid does not match connection player Guid: SuffixGuid={SuffixGuid}, PlayerGuid={PlayerGuid}. Continuing with echoed H1/H2 only.",
                suffixGuid, playerGuid);
        }

        var hadPending = ExperimentalBanditEncounterSession.TryTake(connection, out var pending);
        if (!hadPending)
        {
            var snap = ExperimentalBanditEncounterSession.Snapshot(connection);
            _logger.LogWarning(
                "EXPERIMENTAL Bandit 41/108 has no pending offer session for this connection. Echoed H1={H1} H2={H2}. No Bandit zoning response sent.",
                packet.HeaderValue1, packet.HeaderValue2);
            _logger.LogWarning(
                "EXPERIMENTAL BANDIT REENTRY: GO received but no pending H1/H2 session. ZoneName={ZoneName} ZoneId={ZoneId}. " +
                "HasPending={HasPending} HasActive={HasActive} HasAwaitingState6={HasAwaitingState6} HasAwaitingReturn={HasAwaitingReturn}.",
                connection.Player?.Zone?.Name, connection.Player?.Zone?.Id,
                snap.HasPending, snap.HasActive, snap.HasAwaitingState6, snap.HasAwaitingReturn);
            return true;
        }

        if (pending.HeaderValue1 != packet.HeaderValue1 || pending.HeaderValue2 != packet.HeaderValue2)
        {
            _logger.LogWarning(
                "EXPERIMENTAL Bandit 41/108 header mismatch: pending H1={PendingH1} H2={PendingH2} ActivityId={ActivityId}; echoed H1={H1} H2={H2}. No Bandit zoning response sent.",
                pending.HeaderValue1, pending.HeaderValue2, pending.ActivityId,
                packet.HeaderValue1, packet.HeaderValue2);
            return true;
        }

        if (pending.ActivityId != BanditActivityId)
        {
            _logger.LogWarning(
                "EXPERIMENTAL BANDIT ZONING TEST: ignored non-29 pending ActivityId={ActivityId}. No response sent.",
                pending.ActivityId);
            return true;
        }

        _logger.LogInformation(
            "EXPERIMENTAL BANDIT REENTRY: GO received and matched. H1={H1} H2={H2} ActivityId={ActivityId} ZoneName={ZoneName} ZoneId={ZoneId}. Instance creation attempted next.",
            packet.HeaderValue1, packet.HeaderValue2, pending.ActivityId,
            connection.Player?.Zone?.Name, connection.Player?.Zone?.Id);

        RunBanditHideoutZoningTest(connection, pending);
        return true;
    }

    private static bool HandleRequestExit(GatewayConnection connection, PacketReader reader, int serverType)
    {
        if (!EncounterParticipantRequestExitPacket.TryDeserialize(reader.Span, out var packet))
        {
            _logger.LogWarning(
                "{Prefix} exit packet received but layout mismatch. ServerType={ServerType}, connection={Connection}, payload={Payload}. No return teleport.",
                ExitTestLogPrefix, serverType, connection, Convert.ToHexString(reader.Span));
            return false;
        }

        _logger.LogInformation(
            "{Prefix} exit packet received. family=41, message=109, H1={H1}, H2={H2}, ServerType={ServerType}, connection={Connection}, payload={Payload}",
            ExitTestLogPrefix, packet.HeaderValue1, packet.HeaderValue2, serverType, connection, Convert.ToHexString(reader.Span));

        TryReturnFromBanditHideout(connection, ExitTestLogPrefix, packet.HeaderValue1, packet.HeaderValue2);
        return true;
    }

    /// <summary>
    /// Shared Bandit return: stored overworld zone/pos/rot → TeleportToZone/BeginZoning →
    /// IsReady/FinishedLoading (visibility) → session cleanup. No Launch/combat/rewards.
    /// </summary>
    internal static bool TryReturnFromBanditHideout(
        GatewayConnection connection,
        string logPrefix,
        int? exitPacketH1,
        int? exitPacketH2)
    {
        var player = connection.Player;
        if (player is null)
            return false;

        if (!ExperimentalBanditEncounterSession.TryTakeActive(connection, out var active))
        {
            // Fallback: still inside Bandit instance after session was cleared unexpectedly.
            if (player.Zone is InstanceZone instance && instance.DefinitionId == BanditZoneDefinitionId)
            {
                active = new ExperimentalBanditEncounterSession.ActiveBanditSession(
                    exitPacketH1 ?? 0,
                    exitPacketH2 ?? 0,
                    BanditActivityId,
                    _zoneManager.StartingZone.Name,
                    _zoneManager.StartingZone.Id,
                    player.StartingZonePosition,
                    player.StartingZoneRotation);

                _logger.LogWarning(
                    "{Prefix} no active session; using Player.StartingZonePosition/Rotation fallback. ZoneName={ZoneName} Position={Position} Rotation={Rotation}",
                    logPrefix, active.ReturnZoneName, active.ReturnPosition, active.ReturnRotation);
            }
            else
            {
                _logger.LogWarning(
                    "{Prefix} exit ignored — no active Bandit session and player not in sg_bandit_hideout. ZoneName={ZoneName} ZoneId={ZoneId}",
                    logPrefix, player.Zone?.Name, player.Zone?.Id);
                return false;
            }
        }

        _logger.LogInformation(
            "{Prefix} current zone={CurrentZone} ZoneId={CurrentZoneId}. return zone={ReturnZoneName} ZoneId={ReturnZoneId} Position={ReturnPosition} Rotation={ReturnRotation}. ActivityId={ActivityId} H1={H1} H2={H2}",
            logPrefix,
            player.Zone?.Name, player.Zone?.Id,
            active.ReturnZoneName, active.ReturnZoneId, active.ReturnPosition, active.ReturnRotation,
            active.ActivityId, active.HeaderValue1, active.HeaderValue2);

        var destination = _zoneManager.StartingZone;
        if (!string.Equals(destination.Name, active.ReturnZoneName, StringComparison.OrdinalIgnoreCase) &&
            destination.Id != active.ReturnZoneId)
        {
            _logger.LogWarning(
                "{Prefix} stored return zone {StoredName}/{StoredId} differs from StartingZone {DestName}/{DestId}; returning to StartingZone (only evidenced overworld).",
                logPrefix, active.ReturnZoneName, active.ReturnZoneId, destination.Name, destination.Id);
        }

        ExperimentalBanditEncounterSession.RegisterAwaitingReturnFinishedLoading(connection, active, logPrefix);

        _logger.LogInformation(
            "{Prefix} BeginZoning started back to overworld. Name={Name} ZoneId={ZoneId} Position={Position} Rotation={Rotation}",
            logPrefix, destination.Name, destination.Id, active.ReturnPosition, active.ReturnRotation);

        player.TeleportToZone(destination, active.ReturnPosition, active.ReturnRotation);

        // Clear any leftover enter-side awaiting state; active already taken.
        ExperimentalBanditEncounterSession.TryTakeAwaitingState6AfterZoning(connection, out _);

        _logger.LogInformation(
            "{Prefix} session cleanup (active taken; awaiting State=6 cleared). Return FinishedLoading still pending for visibility restore log.",
            logPrefix);

        return true;
    }

    private static void RunBanditHideoutZoningTest(
        GatewayConnection connection,
        ExperimentalBanditEncounterSession.PendingOffer pending)
    {
        var player = connection.Player;

        if (!_zoneManager.TryGetOrCreateInstanceZone(BanditZoneDefinitionId, out var destination))
        {
            _logger.LogError(
                "EXPERIMENTAL BANDIT ZONING TEST: failed to get/create instance zone DefinitionId={DefinitionId} (sg_bandit_hideout). No BeginZoning sent.",
                BanditZoneDefinitionId);
            _logger.LogError(
                "EXPERIMENTAL BANDIT REENTRY: instance creation attempted and FAILED. DefinitionId={DefinitionId}.",
                BanditZoneDefinitionId);
            return;
        }

        var priorZoneId = ExperimentalBanditEncounterSession.LastBanditInstanceZoneId;
        var reused = priorZoneId is int prior && prior == destination.Id;
        ExperimentalBanditEncounterSession.RememberBanditInstanceZoneId(destination.Id);
        _logger.LogInformation(
            "EXPERIMENTAL BANDIT REENTRY: instance creation attempted. Result={Result} ZoneId={ZoneId} DefinitionId={DefinitionId} PriorZoneId={PriorZoneId}.",
            reused ? "REUSED existing instance" : "NEW or first instance",
            destination.Id, destination.DefinitionId, priorZoneId);

        var spawnPosition = destination.SpawnPosition;
        // UNVERIFIED DIAGNOSTIC ROTATION from zone JSON [0,1] => Quaternion(0,0,1,0). Facing only; not client-proven.
        var spawnRotation = destination.SpawnRotation;

        var beforeZoneName = player.Zone?.Name ?? "<null>";
        var beforeZoneId = player.Zone?.Id ?? -1;
        var beforePosition = player.Position;
        var beforeRotation = player.Rotation;

        _logger.LogWarning(
            "EXPERIMENTAL BANDIT ZONING TEST: ActivityId=29 GO matched. TeleportToZone -> sg_bandit_hideout. " +
            "CLIENT-BACKED: world Name=sg_bandit_hideout, Sky=sky_bandit_hideout.xml, Spawn=(153,34,168) BanditHideout_Bed center. " +
            "DIAGNOSTIC/UNVERIFIED: Rotation={SpawnRotation} (UNVERIFIED DIAGNOSTIC ROTATION), GeometryId=0 (AJ unset; Name loads .gzne). " +
            "H1={H1} H2={H2}. Before: ZoneName={BeforeZoneName} ZoneId={BeforeZoneId} Position={BeforePosition} Rotation={BeforeRotation}. " +
            "Destination: Name={DestName} ZoneId={DestId} DefinitionId={DestDefinitionId} Sky={Sky} Spawn={Spawn}.",
            spawnRotation,
            pending.HeaderValue1, pending.HeaderValue2,
            beforeZoneName, beforeZoneId, beforePosition, beforeRotation,
            destination.Name, destination.Id, destination.DefinitionId, destination.Sky, spawnPosition);

        ExperimentalBanditEncounterSession.RegisterActive(connection, new ExperimentalBanditEncounterSession.ActiveBanditSession(
            pending.HeaderValue1,
            pending.HeaderValue2,
            pending.ActivityId,
            beforeZoneName,
            beforeZoneId,
            beforePosition,
            beforeRotation));

        ExperimentalBanditEncounterSession.RegisterAwaitingState6AfterZoning(connection, pending);

        player.TeleportToZone(destination, spawnPosition, spawnRotation);

        _logger.LogInformation(
            "EXPERIMENTAL BANDIT ZONING TEST: BeginZoning SENT (opcode 31). " +
            "Name={Name} ZoneId={ZoneId} Position={Position} Rotation={Rotation} Sky={Sky} GeometryId=0. " +
            "After: ZoneName={AfterZoneName} ZoneId={AfterZoneId} Position={AfterPosition} Rotation={AfterRotation}. " +
            "State=6 deferred until PacketClientFinishedLoading. Order: GO -> BeginZoning -> IsReady -> FinishedLoading -> State=6. H1={H1} H2={H2}.",
            destination.Name, destination.Id, spawnPosition, spawnRotation, destination.Sky,
            player.Zone?.Name, player.Zone?.Id, player.Position, player.Rotation,
            pending.HeaderValue1, pending.HeaderValue2);
    }
}
