using System;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.Zones;

/// <summary>
/// Thin zone instance for encounter destinations. Does not re-send starting-zone login payloads,
/// but must still complete the normal zone-ready confirmation handshake so the client can leave
/// WaitForWorldReady (InitialZoneDataComplete / ReceivedPreloadDonePacket).
/// </summary>
public sealed class InstanceZone : BaseZone
{
    public InstanceZone(InstanceZoneDefinition zoneDefinition, IServiceProvider serviceProvider)
        : base(zoneDefinition, serviceProvider)
    {
    }

    public override void OnClientIsReady(Player player)
    {
        // Same minimum confirmations StartingZone sends after its login flood.
        // Client WaitForWorldReady requires these before PacketClientFinishedLoading.
        var packetZoneDoneSendingInitialData = new PacketZoneDoneSendingInitialData();
        player.SendTunneled(packetZoneDoneSendingInitialData);

        Logger.LogInformation(
            "EXPERIMENTAL BANDIT INSTANCE READY: sent PacketZoneDoneSendingInitialData. " +
            "ZoneName={ZoneName} ZoneId={ZoneId} PlayerGuid={PlayerGuid}",
            Name, Id, player.Guid);

        var clientUpdatePacketDoneSendingPreloadCharacters = new ClientUpdatePacketDoneSendingPreloadCharacters();
        player.SendTunneled(clientUpdatePacketDoneSendingPreloadCharacters);

        Logger.LogInformation(
            "EXPERIMENTAL BANDIT INSTANCE READY: sent ClientUpdatePacketDoneSendingPreloadCharacters. " +
            "ZoneName={ZoneName} ZoneId={ZoneId} PlayerGuid={PlayerGuid}",
            Name, Id, player.Guid);
    }

    public override void OnClientFinishedLoading(Player player)
    {
        // Intentional no-op; encounter State=6 is handled by the gateway FinishedLoading path.
    }
}
