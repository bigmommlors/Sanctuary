using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class FactoryPacketListToolsRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(FactoryPacketListToolsRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!FactoryPacketListToolsRequest.TryDeserialize(data, out _))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(FactoryPacketListToolsRequest));
            return false;
        }

        // EXPERIMENTAL controlled one-Shovel ListToolsResponse (188/22). Revert: new FactoryPacketListToolsResponse().
        connection.SendTunneled(FactoryPacketListToolsResponse.CreateExperimentalOneShovel());

        _logger.LogInformation(
            "EXPERIMENTAL FACTORY TOOLS: received ListToolsRequest 188/6; sent one-tool ListToolsResponse 188/22 with Shovel ToolId=4, Useable=true.");

        return true;
    }
}
