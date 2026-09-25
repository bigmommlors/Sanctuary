using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Farming;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// EXPERIMENTAL 188/7 → 188/23 EquipTool exchange. Revert by removing route from
/// <see cref="BaseFactoryPacketHandler"/> (and optionally this file).
/// Policy: ToolId=4 (Shovel) → Success=1 + record SelectedFarmToolId; all other ToolIds → Success=0 (echoed), selection unchanged.
/// No inventory/charge/visuals / retail unequip packets.
/// </summary>
[PacketHandler]
public static class FactoryPacketEquipToolRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(FactoryPacketEquipToolRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!FactoryPacketEquipToolRequest.TryDeserialize(data, out var request))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(FactoryPacketEquipToolRequest));
            return false;
        }

        // EXPERIMENTAL: only Shovel (ToolId=4) succeeds; others fail with echoed ToolId.
        FactoryPacketEquipToolResponse response;
        if (request.ToolId == FarmingToolSelection.ShovelToolId)
        {
            response = FactoryPacketEquipToolResponse.CreateExperimentalSuccess(request.ToolId);
            connection.SendTunneled(response);

            // Session-only shovel selection for prototype rock gating (not retail tool visuals).
            if (connection.Player is not null)
            {
                connection.Player.SelectedFarmToolId = FarmingToolSelection.ApplyEquipToolResult(
                    connection.Player.SelectedFarmToolId,
                    request.ToolId,
                    success: true);
            }

            _logger.LogInformation(
                "EXPERIMENTAL FACTORY TOOLS: received EquipToolRequest 188/7 ToolId={ToolId}; sent EquipToolResponse 188/23 Success={Success} ToolId={EchoToolId}; SelectedFarmToolId={SelectedFarmToolId}.",
                request.ToolId,
                response.Success,
                response.ToolId,
                connection.Player?.SelectedFarmToolId);
        }
        else
        {
            response = FactoryPacketEquipToolResponse.CreateExperimentalFailure(request.ToolId);
            connection.SendTunneled(response);

            // Do not record unsupported ToolIds; leave any prior selection unchanged.
            if (connection.Player is not null)
            {
                connection.Player.SelectedFarmToolId = FarmingToolSelection.ApplyEquipToolResult(
                    connection.Player.SelectedFarmToolId,
                    request.ToolId,
                    success: false);
            }

            _logger.LogInformation(
                "EXPERIMENTAL FACTORY TOOLS: EquipToolRequest 188/7 ToolId={ToolId} unsupported; sent EquipToolResponse 188/23 Success={Success} ToolId={EchoToolId}; SelectedFarmToolId={SelectedFarmToolId}.",
                request.ToolId,
                response.Success,
                response.ToolId,
                connection.Player?.SelectedFarmToolId);
        }

        return true;
    }
}
