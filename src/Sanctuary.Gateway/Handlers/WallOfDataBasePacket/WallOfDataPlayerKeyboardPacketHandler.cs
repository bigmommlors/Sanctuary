using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class WallOfDataPlayerKeyboardPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(WallOfDataPlayerKeyboardPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!WallOfDataPlayerKeyboardPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError(
                "Failed to deserialize {packet}. payload={Payload}",
                nameof(WallOfDataPlayerKeyboardPacket),
                Convert.ToHexString(data));
            return false;
        }

        // Telemetry only — no server response. Same family as WallOfDataUIEventPacket.
        var firstWords = packet.RawEntries
            .Take(12)
            .Select(e => $"0x{(ushort)(e & 0xFFFF):X4}")
            .ToArray();

        var player = connection.Player;

        _logger.LogInformation(
            "EXPERIMENTAL POSTLOAD DECODE: WallOfDataPlayerKeyboardPacket (family=194/0xC2 sub=2). " +
            "EntryCount={EntryCount} ZoneName={ZoneName} ZoneId={ZoneId} PlayerGuid={PlayerGuid} " +
            "FirstEntryUInt16LE={FirstWords}. No server response. Not encounter/combat/launch.",
            packet.EntryCount,
            player?.Zone?.Name,
            player?.Zone?.Id,
            player?.Guid,
            string.Join(',', firstWords));

        return true;
    }
}
