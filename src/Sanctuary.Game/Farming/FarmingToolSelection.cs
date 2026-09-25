namespace Sanctuary.Game.Farming;

/// <summary>
/// EXPERIMENTAL server-side farming tool selection for prototype rock gating.
/// Not a reconstruction of retail Factory obstacle / tool-use protocol.
/// </summary>
public static class FarmingToolSelection
{
    /// <summary>LIVE-PROVEN Shovel ToolId from 188/22 ListTools and 188/7 EquipTool.</summary>
    public const int ShovelToolId = 4;

    /// <summary>Short system message when rock is clicked without Shovel selected.</summary>
    public const string RockRequiresShovelMessage = "Shovel required.";

    /// <summary>
    /// Apply EquipTool 188/7 result to session selection.
    /// Only Success with Shovel records selection; unsupported/failed paths leave
    /// selection unchanged (never set an unsupported ToolId as selected).
    /// </summary>
    public static int? ApplyEquipToolResult(int? currentSelectedToolId, int toolId, bool success)
    {
        if (success && toolId == ShovelToolId)
            return ShovelToolId;

        return currentSelectedToolId;
    }

    public static bool IsShovelSelected(int? selectedFarmToolId) =>
        selectedFarmToolId == ShovelToolId;

    /// <summary>
    /// Prototype Wilds rock may clear/persist only when Shovel is selected.
    /// </summary>
    public static bool CanClearRock(int? selectedFarmToolId) =>
        IsShovelSelected(selectedFarmToolId);
}
