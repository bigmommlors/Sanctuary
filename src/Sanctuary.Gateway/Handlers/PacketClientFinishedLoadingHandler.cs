using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketClientFinishedLoadingHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketClientFinishedLoadingHandler));
    }

    public static bool HandlePacket(GatewayConnection connection)
    {
        var player = connection.Player;
        _logger.LogInformation(
            "Received PacketClientFinishedLoading: connection={Connection}, ZoneName={ZoneName}, ZoneId={ZoneId}, Position={Position}",
            connection, player?.Zone?.Name, player?.Zone?.Id, player?.Position);

        if (player is null)
            return false;

        player.Visible = true;

        if (player.Mount is not null)
            player.Mount.Visible = true;

        player.UpdatePosition(player.Position, player.Rotation);

        if (player.Mount is not null)
        {
            player.SendTunneled(player.Mount.GetAddNpcPacket());
            player.SendTunneled(player.Mount.GetMountResponsePacket());
        }

        player.Zone.OnClientFinishedLoading(player);

        player.SendToolbar();

        if (ExperimentalBanditEncounterSession.TryTakeAwaitingState6AfterZoning(connection, out var pending))
        {
            connection.SendTunneled(new EncounterStatePacket(pending.HeaderValue1, pending.HeaderValue2, 6));

            _logger.LogInformation(
                "EXPERIMENTAL BANDIT ZONING TEST: State=6 SENT AFTER PacketClientFinishedLoading (after BeginZoning). " +
                "family=41 message=106 State=6 H1={H1} H2={H2} ActivityId={ActivityId} ZoneName={ZoneName} ZoneId={ZoneId} Position={Position}. " +
                "No launch-114, combat, rewards, or encounter completion was sent.",
                pending.HeaderValue1, pending.HeaderValue2, pending.ActivityId,
                player.Zone?.Name, player.Zone?.Id, player.Position);
        }

        if (ExperimentalBanditEncounterSession.TryTakeAwaitingReturnFinishedLoading(connection, out var awaitingReturn))
        {
            var returned = awaitingReturn.Session;
            _logger.LogInformation(
                "{Prefix} FinishedLoading returned. Visible restored. " +
                "ZoneName={ZoneName} ZoneId={ZoneId} Position={Position} Rotation={Rotation}. " +
                "StoredReturn ZoneName={ReturnZoneName} ZoneId={ReturnZoneId} Position={ReturnPosition} Rotation={ReturnRotation}. " +
                "Return location preserved until now; clearing Bandit transient session next.",
                awaitingReturn.LogPrefix,
                player.Zone?.Name, player.Zone?.Id, player.Position, player.Rotation,
                returned.ReturnZoneName, returned.ReturnZoneId, returned.ReturnPosition, returned.ReturnRotation);

            // Client stays Minigame Type=4 / blocks second Join without 39/19 (fork + live MarketingData).
            connection.SendTunneled(new MiniGameStateRemovePacket());
            _logger.LogInformation(
                "EXPERIMENTAL BANDIT REENTRY: sent MiniGameStateRemovePacket (family=39 sub=19) after overworld FinishedLoading. " +
                "Prior H1={H1} H2={H2} ActivityId={ActivityId}. Clears stuck client minigame state for fresh Join.",
                returned.HeaderValue1, returned.HeaderValue2, returned.ActivityId);

            ExperimentalBanditEncounterSession.ClearBanditTransientAfterReturn(connection);
            var after = ExperimentalBanditEncounterSession.Snapshot(connection);
            _logger.LogInformation(
                "EXPERIMENTAL BANDIT REENTRY: Bandit session fully reset after return. " +
                "HasPending={HasPending} HasAwaitingState6={HasAwaitingState6} HasActive={HasActive} HasAwaitingReturn={HasAwaitingReturn} " +
                "LastBanditInstanceZoneId={LastZoneId} (instance not disposed; reusable on next enter). " +
                "Player ZoneName={ZoneName} ZoneId={ZoneId}.",
                after.HasPending, after.HasAwaitingState6, after.HasActive, after.HasAwaitingReturn,
                after.LastBanditInstanceZoneId,
                player.Zone?.Name, player.Zone?.Id);
        }

        return true;
    }
}
