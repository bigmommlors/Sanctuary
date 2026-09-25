namespace Sanctuary.Game.Farming;

/// <summary>
/// EXPERIMENTAL: persistent mining-shovel hand visual while Factory Shovel (ToolId 4) is selected
/// in the private Wilds farm. Reuses proven <see cref="FarmingMinerShovelVisualExperiment"/> attachment fields.
/// Not retail Factory tool visuals. Visual-only — no inventory item, no DB equip.
/// </summary>
public static class FarmingToolShedShovelVisual
{
    /// <summary>
    /// Attach only after successful EquipTool 188/7 for Shovel inside private Wilds farm.
    /// Opening the toolshed / ListTools alone must not attach.
    /// </summary>
    public static bool ShouldAttachOnEquipResult(bool isInWildsTestInstance, int toolId, bool success) =>
        isInWildsTestInstance &&
        success &&
        toolId == FarmingToolSelection.ShovelToolId;

    /// <summary>
    /// Keep visual while session selection is Shovel. Cleared on farm leave / disconnect.
    /// </summary>
    public static bool ShouldRemainVisible(int? selectedFarmToolId) =>
        FarmingToolSelection.IsShovelSelected(selectedFarmToolId);

    /// <summary>
    /// Unsupported EquipTool failures do not clear selection (see <see cref="FarmingToolSelection.ApplyEquipToolResult"/>).
    /// There is no proven Factory unequip / tool-switch packet that clears Shovel — do not invent detach-on-fail.
    /// </summary>
    public static bool ShouldDetachOnFailedEquip() => false;

    /// <summary>Same proven attachment as live-tested !farmtest minervisual (no dig, no timeout).</summary>
    public static Sanctuary.Packet.Common.CharacterAttachmentData CreateAttachment() =>
        FarmingMinerShovelVisualExperiment.CreateExperimentalAttachment();

    public const string ModelName = FarmingMinerShovelVisualExperiment.ModelName;
    public const string TextureAlias = FarmingMinerShovelVisualExperiment.TextureAlias;
    public const string TintAlias = FarmingMinerShovelVisualExperiment.TintAlias;
    public const int CompositeEffectId = FarmingMinerShovelVisualExperiment.CompositeEffectId;
    public const int Slot = FarmingMinerShovelVisualExperiment.ExperimentalSlot;
    public const int WieldType = FarmingMinerShovelVisualExperiment.ExperimentalWieldType;
    public const int ItemInstanceId = FarmingMinerShovelVisualExperiment.ExperimentalItemInstanceId;
}
