using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Farming;

namespace Sanctuary.Game.Tests;

/// <summary>
/// EXPERIMENTAL Tool Shed → persistent mining shovel visual policy (EquipTool success only).
/// </summary>
[TestClass]
public sealed class FarmingToolShedShovelVisualTests
{
    [TestMethod]
    public void SuccessfulShovelEquipInWilds_ShouldAttach()
    {
        Assert.IsTrue(FarmingToolShedShovelVisual.ShouldAttachOnEquipResult(
            isInWildsTestInstance: true,
            toolId: FarmingToolSelection.ShovelToolId,
            success: true));
    }

    [TestMethod]
    public void OpenToolshedOrListToolsAlone_DoesNotAttach()
    {
        // Attach is gated on EquipTool success — ListTools/OpenToolshed never call ShouldAttach with success.
        Assert.IsFalse(FarmingToolShedShovelVisual.ShouldAttachOnEquipResult(
            isInWildsTestInstance: true, toolId: 4, success: false));
        Assert.IsFalse(FarmingToolShedShovelVisual.ShouldAttachOnEquipResult(
            isInWildsTestInstance: true, toolId: 0, success: true));
    }

    [TestMethod]
    public void SuccessfulShovelEquipOutsideWilds_DoesNotAttach()
    {
        Assert.IsFalse(FarmingToolShedShovelVisual.ShouldAttachOnEquipResult(
            isInWildsTestInstance: false,
            toolId: FarmingToolSelection.ShovelToolId,
            success: true));
    }

    [TestMethod]
    public void UnsupportedToolEquip_DoesNotAttachAndDoesNotDetach()
    {
        Assert.IsFalse(FarmingToolShedShovelVisual.ShouldAttachOnEquipResult(
            isInWildsTestInstance: true, toolId: 5, success: false));
        Assert.IsFalse(FarmingToolShedShovelVisual.ShouldDetachOnFailedEquip());
    }

    [TestMethod]
    public void Persistence_RemainsWhileShovelSelected()
    {
        Assert.IsTrue(FarmingToolShedShovelVisual.ShouldRemainVisible(FarmingToolSelection.ShovelToolId));
        Assert.IsFalse(FarmingToolShedShovelVisual.ShouldRemainVisible(null));
        Assert.IsFalse(FarmingToolShedShovelVisual.ShouldRemainVisible(5));
    }

    [TestMethod]
    public void Cleanup_WhenSelectionCleared_ShouldNotRemainVisible()
    {
        // Mirrors farm leave / disconnect clearing SelectedFarmToolId.
        int? selected = FarmingToolSelection.ShovelToolId;
        selected = null;
        Assert.IsFalse(FarmingToolShedShovelVisual.ShouldRemainVisible(selected));
    }

    [TestMethod]
    public void RepeatedSuccessfulShovelEquip_StillShouldAttach()
    {
        // Idempotent re-attach path for selecting Shovel again while already selected.
        Assert.IsTrue(FarmingToolShedShovelVisual.ShouldAttachOnEquipResult(true, 4, true));
        Assert.IsTrue(FarmingToolShedShovelVisual.ShouldAttachOnEquipResult(true, 4, true));
    }

    [TestMethod]
    public void Attachment_MatchesLiveTestedMinerVisualFields()
    {
        var attachment = FarmingToolShedShovelVisual.CreateAttachment();
        var miner = FarmingMinerShovelVisualExperiment.CreateExperimentalAttachment();

        Assert.AreEqual("tool_ar_ag_weapon_shovel.adr", attachment.ModelName);
        Assert.AreEqual("miner-steel-L2", attachment.TextureAlias);
        Assert.AreEqual("dyetint", attachment.TintAlias);
        Assert.AreEqual(0, attachment.TintId);
        Assert.AreEqual(0, attachment.CompositeEffectId);
        Assert.AreEqual(7, attachment.Slot);

        Assert.AreEqual(miner.ModelName, attachment.ModelName);
        Assert.AreEqual(miner.TextureAlias, attachment.TextureAlias);
        Assert.AreEqual(miner.TintAlias, attachment.TintAlias);
        Assert.AreEqual(miner.Slot, attachment.Slot);

        Assert.AreEqual(0, FarmingToolShedShovelVisual.ItemInstanceId);
        Assert.AreEqual(0, FarmingToolShedShovelVisual.WieldType);
        Assert.AreNotEqual(1910, FarmingToolShedShovelVisual.ItemInstanceId);
    }

    [TestMethod]
    public void EquipmentRestoration_UsesProfileNotCatalogId()
    {
        // Visual-only sentinel; restore path must use real profile instance ids when present.
        Assert.AreEqual(0, FarmingToolShedShovelVisual.ItemInstanceId);
        Assert.AreEqual(
            FarmingMinerShovelVisualExperiment.ExperimentalItemInstanceId,
            FarmingToolShedShovelVisual.ItemInstanceId);
    }

    [TestMethod]
    public void SelectionPolicy_StillRecordsShovelWithoutInventingUnequip()
    {
        int? selected = null;
        selected = FarmingToolSelection.ApplyEquipToolResult(selected, 4, success: true);
        Assert.AreEqual(4, selected);

        // Failed other tool leaves selection — visual policy must not detach on that alone.
        selected = FarmingToolSelection.ApplyEquipToolResult(selected, 5, success: false);
        Assert.AreEqual(4, selected);
        Assert.IsTrue(FarmingToolShedShovelVisual.ShouldRemainVisible(selected));
        Assert.IsFalse(FarmingToolShedShovelVisual.ShouldDetachOnFailedEquip());
    }
}
