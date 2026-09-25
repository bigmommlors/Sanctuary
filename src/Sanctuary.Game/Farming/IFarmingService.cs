using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Farming;

public interface IFarmingService
{
    bool TrySpawnPrototypePlot();

    Npc? PlotNpc { get; }

    CropPlotStage GetStage(Player player);

    string DescribeStatus(Player player);

    void HandleInteract(Player player);

    bool TryForceGrow(Player player);

    bool TryReset(Player player);

    void TeleportToPlot(Player player);

    bool TryEnterWildsTestInstance(Player player, out string message);

    bool TryLeaveWildsTestInstance(Player player, out string message);

    bool IsInWildsTestInstance(Player player);

    /// <summary>
    /// Spawn one click-to-clear weed NPC in private Wilds Farm if not already present.
    /// Auto-spawn on enter uses the same slot; will not duplicate.
    /// </summary>
    bool TrySpawnDebugWeed(Player player, out string message);

    /// <summary>Remove the weed NPC if present (debug cmd; interact also clears + persists).</summary>
    bool TryRemoveDebugWeed(Player player, out string message);

    /// <summary>
    /// Spawn one click-to-clear rock NPC in private Wilds Farm if not already present.
    /// Auto-spawn on enter uses the same slot; will not duplicate.
    /// </summary>
    bool TrySpawnDebugRock(Player player, out string message);

    /// <summary>Remove the rock NPC if present (debug cmd; interact also clears + persists).</summary>
    bool TryRemoveDebugRock(Player player, out string message);

    /// <summary>
    /// Spawn one click-to-clear tree NPC in private Wilds Farm if not already present.
    /// Auto-spawn on enter uses the same slot; will not duplicate.
    /// </summary>
    bool TrySpawnDebugTree(Player player, out string message);

    /// <summary>Remove the tree NPC if present (debug cmd; interact also clears + persists).</summary>
    bool TryRemoveDebugTree(Player player, out string message);

    /// <summary>Report cleared/uncleared for the three Wilds farm prototype obstacles.</summary>
    string DescribeObstaclesStatus(Player player);

    /// <summary>
    /// DEBUG: reset all three prototype obstacles to uncleared and re-spawn if in Wilds farm.
    /// </summary>
    bool TryResetObstacles(Player player, out string message);

    /// <summary>
    /// EXPERIMENTAL / debug-only: send Factory OpenToolshed (188/26) with empty type string.
    /// Does not list tools, equip tools, spawn NPCs, or handle ListToolsRequest.
    /// </summary>
    void SendExperimentalOpenToolshed(Player player);

    /// <summary>
    /// EXPERIMENTAL / debug-only: play farm_dig (3900003) on the player via PlayerUpdatePacketSetAnimation.
    /// Restricted to private Wilds farm. Does not require Shovel, attach mesh, clear rocks, or write DB.
    /// </summary>
    bool TrySendExperimentalDigAnimation(Player player, out string message);
}
