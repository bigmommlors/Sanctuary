using Sanctuary.Game.Entities;
using Sanctuary.Game.Farming;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class FarmTestChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IFarmingService _farmingService;
    private readonly IRewardManager _rewardManager;

    public string KeyWord => "farmtest";

    public string Usage =>
        "enter|leave|seed|status|grow|reset|tp|weed|weedremove|rock|rockremove|tree|treeremove|obstacles|obstaclesreset|toolshed|diganim";

    public string Description =>
        "Wilds Farm private test (enter/leave) + Farnum harness helpers + persistent weed/rock/tree obstacles. Physical Tool Shed on enter. EXPERIMENTAL toolshed / diganim.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public FarmTestChatCommand(
        IChatCommandManager chatCommandManager,
        IFarmingService farmingService,
        IRewardManager rewardManager)
    {
        _chatCommandManager = chatCommandManager;
        _farmingService = farmingService;
        _rewardManager = rewardManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
        {
            ChatHelper.SendSystemMessage(invoker, $"Usage: !farmtest {Usage}");
            return true;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "enter":
                if (!_farmingService.TryEnterWildsTestInstance(invoker, out var enterMsg))
                    ChatHelper.SendSystemMessage(invoker, enterMsg);
                else
                {
                    _chatCommandManager.LogAction(this, invoker, "Farmtest enter Wilds", null, null);
                    ChatHelper.SendSystemMessage(invoker, enterMsg);
                }
                break;
            case "leave":
                if (!_farmingService.TryLeaveWildsTestInstance(invoker, out var leaveMsg))
                    ChatHelper.SendSystemMessage(invoker, leaveMsg);
                else
                {
                    _chatCommandManager.LogAction(this, invoker, "Farmtest leave Wilds", null, null);
                    ChatHelper.SendSystemMessage(invoker, leaveMsg);
                }
                break;
            case "seed":
                GrantSeed(invoker);
                break;
            case "status":
                ChatHelper.SendSystemMessage(invoker, _farmingService.DescribeStatus(invoker));
                break;
            case "grow":
                if (!_farmingService.TryForceGrow(invoker))
                    ChatHelper.SendSystemMessage(invoker, "Nothing growing to force-ripen.");
                else
                    ChatHelper.SendSystemMessage(invoker, "Crop forced to Harvestable.");
                break;
            case "reset":
                if (!_farmingService.TryReset(invoker))
                    ChatHelper.SendSystemMessage(invoker, "Failed to reset plot.");
                else
                    ChatHelper.SendSystemMessage(invoker, "Plot reset to Empty.");
                break;
            case "tp":
                _farmingService.TeleportToPlot(invoker);
                ChatHelper.SendSystemMessage(invoker, "Teleported to Farnum farming harness plot (debug).");
                break;
            case "weed":
                if (!_farmingService.TrySpawnDebugWeed(invoker, out var weedMsg))
                    ChatHelper.SendSystemMessage(invoker, weedMsg);
                else
                {
                    _chatCommandManager.LogAction(this, invoker, "Farmtest prototype weed spawn", null, null);
                    ChatHelper.SendSystemMessage(invoker, weedMsg);
                }
                break;
            case "weedremove":
                if (!_farmingService.TryRemoveDebugWeed(invoker, out var weedRemoveMsg))
                    ChatHelper.SendSystemMessage(invoker, weedRemoveMsg);
                else
                {
                    _chatCommandManager.LogAction(this, invoker, "Farmtest prototype weed remove", null, null);
                    ChatHelper.SendSystemMessage(invoker, weedRemoveMsg);
                }
                break;
            case "rock":
                if (!_farmingService.TrySpawnDebugRock(invoker, out var rockMsg))
                    ChatHelper.SendSystemMessage(invoker, rockMsg);
                else
                {
                    _chatCommandManager.LogAction(this, invoker, "Farmtest prototype rock spawn", null, null);
                    ChatHelper.SendSystemMessage(invoker, rockMsg);
                }
                break;
            case "rockremove":
                if (!_farmingService.TryRemoveDebugRock(invoker, out var rockRemoveMsg))
                    ChatHelper.SendSystemMessage(invoker, rockRemoveMsg);
                else
                {
                    _chatCommandManager.LogAction(this, invoker, "Farmtest prototype rock remove", null, null);
                    ChatHelper.SendSystemMessage(invoker, rockRemoveMsg);
                }
                break;
            case "tree":
                if (!_farmingService.TrySpawnDebugTree(invoker, out var treeMsg))
                    ChatHelper.SendSystemMessage(invoker, treeMsg);
                else
                {
                    _chatCommandManager.LogAction(this, invoker, "Farmtest prototype tree spawn", null, null);
                    ChatHelper.SendSystemMessage(invoker, treeMsg);
                }
                break;
            case "treeremove":
                if (!_farmingService.TryRemoveDebugTree(invoker, out var treeRemoveMsg))
                    ChatHelper.SendSystemMessage(invoker, treeRemoveMsg);
                else
                {
                    _chatCommandManager.LogAction(this, invoker, "Farmtest prototype tree remove", null, null);
                    ChatHelper.SendSystemMessage(invoker, treeRemoveMsg);
                }
                break;
            case "obstacles":
                ChatHelper.SendSystemMessage(invoker, _farmingService.DescribeObstaclesStatus(invoker));
                break;
            case "obstaclesreset":
                if (!_farmingService.TryResetObstacles(invoker, out var obstaclesResetMsg))
                    ChatHelper.SendSystemMessage(invoker, obstaclesResetMsg);
                else
                {
                    _chatCommandManager.LogAction(this, invoker, "Farmtest obstacles reset", null, null);
                    ChatHelper.SendSystemMessage(invoker, obstaclesResetMsg);
                }
                break;
            case "toolshed":
                // EXPERIMENTAL / debug-only: OpenToolshed 188/26 empty type. No ListToolsResponse, no EquipTool, no NPC 3415.
                _farmingService.SendExperimentalOpenToolshed(invoker);
                _chatCommandManager.LogAction(this, invoker, "Farmtest experimental OpenToolshed 188/26", null, null);
                ChatHelper.SendSystemMessage(invoker, "EXPERIMENTAL: sent Factory OpenToolshed (188/26 empty type).");
                break;
            case "diganim":
                // EXPERIMENTAL / debug-only: farm_dig via PlayerUpdatePacketSetAnimation. Wilds farm only. No shovel/mesh/DB.
                if (!_farmingService.TrySendExperimentalDigAnimation(invoker, out var digAnimMsg))
                    ChatHelper.SendSystemMessage(invoker, digAnimMsg);
                else
                {
                    _chatCommandManager.LogAction(this, invoker, "Farmtest experimental diganim farm_dig 3900003", null, null);
                    ChatHelper.SendSystemMessage(invoker, digAnimMsg);
                }
                break;
            default:
                return false;
        }

        return true;
    }

    private void GrantSeed(Player invoker)
    {
        const int quantity = 5;

        if (!_rewardManager.TryGrantItem(invoker, FarmingPrototypeConfig.SeedDefinitionId, tint: 0, quantity))
        {
            ChatHelper.SendSystemMessage(invoker, "Failed to grant Bumbleberry Seed.");
            return;
        }

        _chatCommandManager.LogAction(this, invoker, "Farmtest seed grant", null,
            $"item={FarmingPrototypeConfig.SeedDefinitionId}, qty={quantity}");
        ChatHelper.SendSystemMessage(invoker, $"Granted {quantity}x Bumbleberry Seed ({FarmingPrototypeConfig.SeedDefinitionId}).");
    }
}
