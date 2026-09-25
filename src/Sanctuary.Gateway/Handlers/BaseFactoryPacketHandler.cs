using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseFactoryPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseFactoryPacketHandler));
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
            FactoryPacketListToolsRequest.OpCode => FactoryPacketListToolsRequestHandler.HandlePacket(connection, reader.Span),
            // EXPERIMENTAL EquipTool 188/7 → 188/23. Revert: remove this case.
            FactoryPacketEquipToolRequest.OpCode => FactoryPacketEquipToolRequestHandler.HandlePacket(connection, reader.Span),
            _ => false
        };
    }
}
