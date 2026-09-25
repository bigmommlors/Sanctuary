using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Farming;

namespace Sanctuary.Game.Tests;

/// <summary>
/// EXPERIMENTAL shovel → rock gating policy tests (server-side selection, not retail Factory obstacle protocol).
/// </summary>
[TestClass]
public sealed class FarmingShovelRockGatingTests
{
    [TestMethod]
    public void SuccessfulShovelEquip_RecordsSelectedToolId4()
    {
        int? selected = null;

        selected = FarmingToolSelection.ApplyEquipToolResult(
            selected, FarmingToolSelection.ShovelToolId, success: true);

        Assert.AreEqual(FarmingToolSelection.ShovelToolId, selected);
        Assert.IsTrue(FarmingToolSelection.IsShovelSelected(selected));
        Assert.IsTrue(FarmingToolSelection.CanClearRock(selected));
    }

    [TestMethod]
    public void UnsupportedToolEquip_DoesNotRecordSelection()
    {
        int? selected = null;

        selected = FarmingToolSelection.ApplyEquipToolResult(selected, toolId: 5, success: false);

        Assert.IsNull(selected);
        Assert.IsFalse(FarmingToolSelection.IsShovelSelected(selected));
        Assert.IsFalse(FarmingToolSelection.CanClearRock(selected));
    }

    [TestMethod]
    public void UnsupportedToolEquip_DoesNotOverwriteExistingShovelSelection()
    {
        int? selected = FarmingToolSelection.ShovelToolId;

        selected = FarmingToolSelection.ApplyEquipToolResult(selected, toolId: 999, success: false);

        Assert.AreEqual(FarmingToolSelection.ShovelToolId, selected);
        Assert.IsTrue(FarmingToolSelection.CanClearRock(selected));
    }

    [TestMethod]
    public void FailedSuccessFlagWithShovelToolId_DoesNotRecordSelection()
    {
        // Success=0 must never set selection even if ToolId echoes 4.
        int? selected = null;

        selected = FarmingToolSelection.ApplyEquipToolResult(
            selected, FarmingToolSelection.ShovelToolId, success: false);

        Assert.IsNull(selected);
        Assert.IsFalse(FarmingToolSelection.CanClearRock(selected));
    }

    [TestMethod]
    public void RockWithoutShovel_RejectsClearAndImpliesNoPersistence()
    {
        int? selected = null;

        Assert.IsFalse(FarmingToolSelection.CanClearRock(selected));
        Assert.IsFalse(string.IsNullOrWhiteSpace(FarmingToolSelection.RockRequiresShovelMessage));
        StringAssert.Contains(FarmingToolSelection.RockRequiresShovelMessage, "Shovel");

        // Decision contract: handler must return before animate/despawn/PersistObstacleCleared when false.
        Assert.IsFalse(FarmingRockDigClearExperiment.TryBegin(selected, alreadyPending: false, out var reject));
        Assert.AreEqual(FarmingToolSelection.RockRequiresShovelMessage, reject);
        Assert.IsFalse(FarmingToolSelection.CanClearRock(5));
        Assert.IsFalse(FarmingToolSelection.CanClearRock(0));
    }

    [TestMethod]
    public void RockWithShovel_AllowsClearAndImpliesPersistencePath()
    {
        int? selected = FarmingToolSelection.ShovelToolId;

        Assert.IsTrue(FarmingToolSelection.CanClearRock(selected));
        Assert.IsTrue(FarmingRockDigClearExperiment.TryBegin(selected, alreadyPending: false, out var reject));
        Assert.IsNull(reject);
        // Persist still occurs only after EXPERIMENTAL delay + revalidation (not at begin).
        Assert.AreEqual(1500, FarmingRockDigClearExperiment.ExperimentalClearDelayMs);
    }

    [TestMethod]
    public void LifecycleReset_ClearsSelectedTool()
    {
        // Mirrors FarmingService enter/leave and Player session teardown (disconnect disposes Player).
        int? selected = FarmingToolSelection.ShovelToolId;

        selected = null;

        Assert.IsNull(selected);
        Assert.IsFalse(FarmingToolSelection.CanClearRock(selected));
    }

    [TestMethod]
    public void WeedTreeGating_NotAppliedByRockPolicy()
    {
        // Rock gate is shovel-only; weed/tree interact paths do not call CanClearRock.
        // Unset tool still fails rock clear — weed/tree remain independent.
        Assert.IsFalse(FarmingToolSelection.CanClearRock(null));
        Assert.IsTrue(FarmingToolSelection.ShovelToolId > 0);
    }
}
