using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketClientIsReadyHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        _logger = loggerFactory.CreateLogger(nameof(PacketClientIsReadyHandler));
    }

    public static bool HandlePacket(GatewayConnection connection)
    {
        var player = connection.Player;
        var awaitingState6 = ExperimentalBanditEncounterSession.TryPeekAwaitingState6AfterZoning(connection, out var pending);

        if (awaitingState6)
        {
            _logger.LogInformation(
                "EXPERIMENTAL BANDIT ZONING TEST: received PacketClientIsReady after BeginZoning. " +
                "H1={H1} H2={H2} ActivityId={ActivityId} ZoneName={ZoneName} ZoneId={ZoneId} Position={Position}. " +
                "State=6 still deferred to PacketClientFinishedLoading.",
                pending.HeaderValue1, pending.HeaderValue2, pending.ActivityId,
                player?.Zone?.Name, player?.Zone?.Id, player?.Position);
        }
        else
        {
            _logger.LogTrace("Received {name} packet.", nameof(PacketClientIsReady));
        }

        if (player is null)
            return false;

        player.Zone.OnClientIsReady(player);

        return true;
    }
}
