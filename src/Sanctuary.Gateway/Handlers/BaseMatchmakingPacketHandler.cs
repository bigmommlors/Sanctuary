using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseMatchmakingPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseMatchmakingPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out short opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        return opCode switch
        {
            ListQueuesRequestPacket.OpCode => ListQueuesRequestPacketHandler.HandlePacket(connection, reader.Span),
            _ => LogUnhandled(opCode, reader)
        };
    }

    private static bool LogUnhandled(short subOpCode, PacketReader reader)
    {
        _logger.LogWarning("Unhandled matchmaking request: family=141, subopcode={sub}, payload={hex}.",
            subOpCode, Convert.ToHexString(reader.Span));
        return false;
    }
}
