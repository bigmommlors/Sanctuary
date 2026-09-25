using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Farming;
using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

/// <summary>
/// Physical Tool Shed: Models.txt 3415 identity, spawn/interact policy, OpenToolshed routing.
/// </summary>
[TestClass]
public sealed class FarmingToolShedPrototypeTests
{
    [TestMethod]
    public void ModelId_IsModelsTxtToolShedNotAssumedNpcDefinition()
    {
        Assert.AreEqual(3415, FarmingToolShedPrototype.ModelId);
        Assert.AreEqual(3415, FarmingPrototypeConfig.DebugToolShedModelId);
        Assert.AreEqual("farming_tool_shed_lv1_01.adr", FarmingToolShedPrototype.ModelFileName);
        Assert.IsTrue(FarmingToolShedPrototype.IsToolShedModel(3415));
        Assert.IsFalse(FarmingToolShedPrototype.IsToolShedModel(3424)); // weed
        Assert.IsFalse(FarmingToolShedPrototype.IsToolShedModel(0));
    }

    [TestMethod]
    public void SpawnPosition_IsConfigurableAndAwayFromKnownObstacles()
    {
        Assert.AreEqual(1365f, FarmingPrototypeConfig.DebugToolShedX);
        Assert.AreEqual(0f, FarmingPrototypeConfig.DebugToolShedY);
        Assert.AreEqual(760f, FarmingPrototypeConfig.DebugToolShedZ);

        // Not on spawn pad, crop plot, or existing obstacle coords.
        Assert.AreNotEqual(FarmingPrototypeConfig.WildsSpawnX, FarmingPrototypeConfig.DebugToolShedX);
        Assert.AreNotEqual(FarmingPrototypeConfig.WildsPlotX, FarmingPrototypeConfig.DebugToolShedX);
        Assert.AreNotEqual(FarmingPrototypeConfig.DebugWeedX, FarmingPrototypeConfig.DebugToolShedX);
        Assert.AreNotEqual(FarmingPrototypeConfig.DebugRockX, FarmingPrototypeConfig.DebugToolShedX);
        Assert.AreNotEqual(FarmingPrototypeConfig.DebugTreeX, FarmingPrototypeConfig.DebugToolShedX);
    }

    [TestMethod]
    public void DuplicatePrevention_SkipsWhenAlreadyPresentForZone()
    {
        Assert.IsTrue(FarmingToolShedPrototype.ShouldSkipSpawnBecauseAlreadyPresent(alreadyPresentForZone: true));
        Assert.IsFalse(FarmingToolShedPrototype.ShouldSkipSpawnBecauseAlreadyPresent(alreadyPresentForZone: false));
    }

    [TestMethod]
    public void Interact_OpensToolshedOnlyInWildsFarmInstance()
    {
        Assert.IsTrue(FarmingToolShedPrototype.ShouldOpenToolshedOnInteract(isInWildsTestInstance: true));
        Assert.IsFalse(FarmingToolShedPrototype.ShouldOpenToolshedOnInteract(isInWildsTestInstance: false));
    }

    [TestMethod]
    public void ClickRouting_UsesSameOpenToolshedPacketAsFarmtestToolshed()
    {
        // FarmingService.SendExperimentalOpenToolshed sends empty-type 188/26 — same proven bytes.
        var expected = Convert.FromHexString("BC001A0000000000");
        CollectionAssert.AreEqual(expected, new FactoryPacketOpenToolshed { Type = string.Empty }.Serialize());
    }

    [TestMethod]
    public void NpcName_IsToolShedLabel()
    {
        Assert.AreEqual("Tool Shed", FarmingToolShedPrototype.NpcName);
        Assert.AreEqual(FarmingPrototypeConfig.DebugToolShedNpcName, FarmingToolShedPrototype.NpcName);
    }
}
