using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ActivityPacketJoinActivityRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    // Local-only correlation identifiers for the controlled Activity 29 offer test.
    // These are NOT claimed to reproduce Sony's retail allocator.
    private static int _nextEncounterSerial;
    private static int _nextSessionToken = 1000;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(ActivityPacketJoinActivityRequestHandler));
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data, int serverType)
    {
        if (!ActivityPacketJoinActivityRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize ActivityPacketJoinActivityRequest: ServerType={ServerType}, payload={Payload}",
                serverType, Convert.ToHexString(data));
            return false;
        }

        _logger.LogInformation("Received ActivityPacketJoinActivityRequest: family=167, branch=1, message=2, ActivityId={ActivityId}, TimezoneOffset={TimezoneOffset}, ServerType={ServerType}, connection={Connection}, payload={Payload}",
            packet.ActivityId, packet.TimezoneOffset, serverType, connection, Convert.ToHexString(data));

        if (!_resourceManager.ClientActivityDefinitions.TryGetValue(packet.ActivityId, out var definition))
        {
            _logger.LogWarning("Activity join unsupported: ActivityId={ActivityId} has no loaded definition. No response sent.", packet.ActivityId);
            return true;
        }

        if (packet.ActivityId != 29)
        {
            _logger.LogWarning("Activity join unsupported: ActivityId={ActivityId}, DefinitionServerType={DefinitionServerType}, AppSystemId={AppSystemId}, Category={Category}, NameId={NameId}, DisplayNameId={DisplayNameId}. No response sent; launch flow is not implemented.",
                packet.ActivityId, definition.ServerType, definition.AppSystemId, definition.Category, definition.NameId, definition.DisplayNameId);
            return true;
        }

        SendControlledBanditOfferTest(connection, definition);
        return true;
    }

    private static void SendControlledBanditOfferTest(GatewayConnection connection, ClientActivityDefinition definition)
    {
        const int activityId = 29;
        const string reentryPrefix = "EXPERIMENTAL BANDIT REENTRY:";

        var existing = ExperimentalBanditEncounterSession.Snapshot(connection);
        var player = connection.Player;
        _logger.LogInformation(
            "{Prefix} second JoinActivityRequest (or first). ActivityId=29. ZoneName={ZoneName} ZoneId={ZoneId}. " +
            "Existing session: HasPending={HasPending} PendingH1={PendingH1} PendingH2={PendingH2} " +
            "HasActive={HasActive} ActiveH1={ActiveH1} ActiveH2={ActiveH2} " +
            "HasAwaitingState6={HasAwaitingState6} HasAwaitingReturn={HasAwaitingReturn} LastBanditInstanceZoneId={LastZoneId}.",
            reentryPrefix,
            player?.Zone?.Name, player?.Zone?.Id,
            existing.HasPending, existing.Pending.HeaderValue1, existing.Pending.HeaderValue2,
            existing.HasActive, existing.Active.HeaderValue1, existing.Active.HeaderValue2,
            existing.HasAwaitingState6, existing.HasAwaitingReturn, existing.LastBanditInstanceZoneId);

        // Stale enter/exit awaits must not block a fresh offer after a prior leave.
        if (existing.HasPending || existing.HasActive || existing.HasAwaitingState6 || existing.HasAwaitingReturn)
        {
            ExperimentalBanditEncounterSession.ClearBanditTransientAfterReturn(connection);
            _logger.LogWarning(
                "{Prefix} cleared stale Bandit transient state before new offer (pending/active/await flags were still set).",
                reentryPrefix);
        }

        var serial = Interlocked.Increment(ref _nextEncounterSerial);
        if (serial <= 0 || serial > 32767)
        {
            _logger.LogError("Bandit Hideout encounter serial exhausted. No offer sent.");
            return;
        }

        // H1 = (serial << 16) | 29. H2 is a stable positive local token (not retail H2=1008).
        var headerValue1 = (serial << 16) | activityId;
        var headerValue2 = Interlocked.Increment(ref _nextSessionToken) & 0x7FFFFFFF;
        if (headerValue2 == 0)
            headerValue2 = Interlocked.Increment(ref _nextSessionToken) & 0x7FFFFFFF;

        _logger.LogInformation(
            "{Prefix} allocating new H1/H2 for offer. ExistingH1/H2 cleared. NewH1={NewH1} (0x{H1Hex}) NewH2={NewH2}.",
            reentryPrefix, headerValue1, headerValue1.ToString("X8"), headerValue2);

        _logger.LogWarning(
            "EXPERIMENTAL Bandit Hideout encounter-offer test: ActivityId={ActivityId}, NameId={NameId}, DescriptionId={DescriptionId}, Difficulty={Difficulty}, Category={Category}, ServerType={ServerType}, H1={H1} (0x{H1Hex}), H2={H2}. Participant/team collections are intentionally empty for this diagnostic.",
            activityId, definition.NameId, definition.DescriptionId, definition.Difficulty, definition.Category, definition.ServerType,
            headerValue1, headerValue1.ToString("X8"), headerValue2);

        ExperimentalBanditEncounterSession.Register(connection, headerValue1, headerValue2, activityId);
        _logger.LogInformation(
            "EXPERIMENTAL Bandit pending session registered: H1={H1} H2={H2} ActivityId={ActivityId}",
            headerValue1, headerValue2, activityId);
        _logger.LogInformation(
            "{Prefix} offer sent path starting (states 2/3/4 + details). NewH1={H1} NewH2={H2}.",
            reentryPrefix, headerValue1, headerValue2);

        connection.SendTunneled(new EncounterStatePacket(headerValue1, headerValue2, 2));
        connection.SendTunneled(new EncounterStatePacket(headerValue1, headerValue2, 3));
        connection.SendTunneled(new EncounterStatePacket(headerValue1, headerValue2, 4));

        // Verified Bandit presentation: NameId=5172, DescriptionId=6995, Difficulty=1.
        // ProfileType/MiniGameType/ZoneContext are experimental combat-offer compatibility
        // candidates only — not verified Bandit retail values. No Frostfang rewards/objectives.
        connection.SendTunneled(new EncounterDetailsResponsePacket(headerValue1, headerValue2)
        {
            NameId = definition.NameId,
            IconId = 0,
            DescriptionId = definition.DescriptionId,
            Difficulty = definition.Difficulty,
            ProfileType = 2,
            MiniGameType = 4,
            MembersOnly = false,
            ZoneContext = 1,
            TeleportEffectId = 0,
            Tutorial = false,
            RespawnTime = 10000,
            ActivityId = activityId
        });

        _logger.LogInformation(
            "EXPERIMENTAL Bandit offer sent: H1={H1} H2={H2} ActivityId={ActivityId}. Scheduling readiness in 600 ms.",
            headerValue1, headerValue2, activityId);
        _logger.LogInformation(
            "{Prefix} offer sent. NewH1={H1} NewH2={H2}. Scheduling readiness; waiting for GO.",
            reentryPrefix, headerValue1, headerValue2);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(600);

                connection.SendTunneled(new EncounterZoneIsReadyPacket(headerValue1, headerValue2));
                connection.SendTunneled(new EncounterStatePacket(headerValue1, headerValue2, 5));

                _logger.LogInformation(
                    "EXPERIMENTAL Bandit readiness sent: H1={H1} H2={H2} ActivityId={ActivityId}. Waiting for client 41/108 (GO). Bandit zoning test targets sg_bandit_hideout.",
                    headerValue1, headerValue2, activityId);
                _logger.LogInformation(
                    "{Prefix} readiness sent; awaiting GO (41/108). H1={H1} H2={H2}.",
                    reentryPrefix, headerValue1, headerValue2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed while sending experimental Bandit readiness: H1={H1} H2={H2}", headerValue1, headerValue2);
            }
        });
    }
}
