using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Farming;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

/// <summary>
/// EXPERIMENTAL minervisual: temporary mining shovel ADR attach + farm_dig + restore policy.
/// Separate from farming shovelvisual (missing DME/DDS).
/// </summary>
[TestClass]
public sealed class FarmingMinerShovelVisualExperimentTests
{
    [TestMethod]
    public void CreateExperimentalAttachment_UsesProvenMinerAdrFields()
    {
        var attachment = FarmingMinerShovelVisualExperiment.CreateExperimentalAttachment();

        Assert.AreEqual("tool_ar_ag_weapon_shovel.adr", attachment.ModelName);
        Assert.AreEqual("miner-steel-L2", attachment.TextureAlias);
        Assert.AreEqual("dyetint", attachment.TintAlias);
        Assert.AreEqual(0, attachment.TintId);
        Assert.AreEqual(0, attachment.CompositeEffectId);
        Assert.AreEqual(7, attachment.Slot);
    }

    [TestMethod]
    public void ExperimentalFields_UseZeroInstanceSentinelNotCatalog1910()
    {
        Assert.AreEqual(7, FarmingMinerShovelVisualExperiment.ExperimentalSlot);
        Assert.AreEqual(0, FarmingMinerShovelVisualExperiment.ExperimentalWieldType);
        Assert.AreEqual(0, FarmingMinerShovelVisualExperiment.ExperimentalItemInstanceId);
        Assert.AreNotEqual(1910, FarmingMinerShovelVisualExperiment.ExperimentalItemInstanceId);
        Assert.AreEqual(2500, FarmingMinerShovelVisualExperiment.ExperimentalVisualDurationMs);
        Assert.AreEqual(0, FarmingMinerShovelVisualExperiment.CompositeEffectId);
    }

    [TestMethod]
    public void CreatePlayNowPacket_StillUsesFarmDig3900003()
    {
        var packet = FarmingDigAnimExperiment.CreatePlayNowPacket(42);
        Assert.AreEqual(3900003, packet.AnimationId);
        Assert.AreEqual(FarmingDigAnimExperiment.FarmDigAnimationId, packet.AnimationId);
    }

    [TestMethod]
    public void CreateSelfEquipPacket_SerializesMinerModelAndTextureAlias()
    {
        var attachment = FarmingMinerShovelVisualExperiment.CreateExperimentalAttachment();
        var packet = FarmingShovelVisualExperiment.CreateSelfEquipPacket(
            attachment, profileId: 1, itemInstanceGuid: 0, equip: true);
        var bytes = packet.Serialize();

        Assert.IsTrue(bytes.Length > 20);
        Assert.AreEqual(ClientUpdatePacketEquipItem.OpCode, BitConverter.ToInt16(bytes, 2));
        var ascii = System.Text.Encoding.ASCII.GetString(bytes);
        StringAssert.Contains(ascii, "tool_ar_ag_weapon_shovel.adr");
        StringAssert.Contains(ascii, "miner-steel-L2");
        Assert.IsFalse(ascii.Contains("farmingshovel"));
        Assert.AreEqual(0, packet.Guid);
        Assert.IsTrue(packet.Equip);
    }

    [TestMethod]
    public void CreateVisibleEquipPacket_UsesZeroItemIdSentinelNotCatalogId()
    {
        var attachment = FarmingMinerShovelVisualExperiment.CreateExperimentalAttachment();
        var packet = FarmingShovelVisualExperiment.CreateVisibleEquipPacket(
            playerGuid: 99,
            itemInstanceId: FarmingMinerShovelVisualExperiment.ExperimentalItemInstanceId,
            attachment,
            profileId: 1,
            wieldType: FarmingMinerShovelVisualExperiment.ExperimentalWieldType);

        Assert.AreEqual(0, packet.Id);
        Assert.AreEqual(0, packet.WieldType);
        Assert.AreEqual(7, packet.Attachment.Slot);
        Assert.AreEqual("tool_ar_ag_weapon_shovel.adr", packet.Attachment.ModelName);
        Assert.AreEqual(99UL, packet.Guid);

        var bytes = packet.Serialize();
        Assert.AreEqual(PlayerUpdatePacketEquipItemChange.OpCode, BitConverter.ToInt16(bytes, 2));
        Assert.AreEqual(0, BitConverter.ToInt32(bytes, 12)); // Id after Guid
    }

    [TestMethod]
    public void TryCreateSnapshot_RejectsEquippedSlotWithoutAttachment()
    {
        var ok = FarmingMinerShovelVisualExperiment.TryCreateSnapshot(
            profileId: 1,
            slotHasProfileItem: true,
            attachment: null,
            wieldType: 0,
            profileItemId: 55,
            out _,
            out var reject);

        Assert.IsFalse(ok);
        StringAssert.Contains(reject, "minervisual");
    }

    [TestMethod]
    public void TryCreateSnapshot_AllowsEmptySlot()
    {
        var ok = FarmingMinerShovelVisualExperiment.TryCreateSnapshot(
            profileId: 1,
            slotHasProfileItem: false,
            attachment: null,
            wieldType: 0,
            profileItemId: null,
            out var snapshot,
            out var reject);

        Assert.IsTrue(ok);
        Assert.IsNull(reject);
        Assert.IsFalse(snapshot.HadEquippedItem);
    }

    [TestMethod]
    public void DueTiming_MatchesExperimentalDuration()
    {
        var start = DateTimeOffset.Parse("2026-09-24T12:00:00Z");
        var due = FarmingMinerShovelVisualExperiment.ComputeDueAtUtc(start);
        Assert.AreEqual(start.AddMilliseconds(2500), due);
        Assert.IsFalse(FarmingMinerShovelVisualExperiment.IsDue(start.AddMilliseconds(2499), due));
        Assert.IsTrue(FarmingMinerShovelVisualExperiment.IsDue(start.AddMilliseconds(2500), due));
    }

    [TestMethod]
    public void FarmOnlyAndDuplicateMessages_Documented()
    {
        StringAssert.Contains(FarmingMinerShovelVisualExperiment.NotInWildsFarmMessage, "Wilds Farm");
        StringAssert.Contains(FarmingMinerShovelVisualExperiment.AlreadyRunningMessage, "already running");
        StringAssert.Contains(FarmingMinerShovelVisualExperiment.SuccessMessage, "mining shovel");
    }

    [TestMethod]
    public void FarmTestChatCommand_UsageIncludesMinervisualAndPreservesShovelvisual()
    {
        var cmd = new FarmTestChatCommand(null!, null!, null!);
        StringAssert.Contains(cmd.Usage, "minervisual");
        StringAssert.Contains(cmd.Usage, "shovelvisual");
        StringAssert.Contains(cmd.Usage, "diganim");
        Assert.AreEqual(ChatCommandRole.Admin, cmd.RequiredRole);
    }

    [TestMethod]
    public void MinerAttachment_DiffersFromFarmingShovelvisualAdr()
    {
        var miner = FarmingMinerShovelVisualExperiment.CreateExperimentalAttachment();
        var farming = FarmingShovelVisualExperiment.CreateExperimentalAttachment();

        Assert.AreNotEqual(farming.ModelName, miner.ModelName);
        Assert.AreNotEqual(farming.TextureAlias, miner.TextureAlias);
        Assert.AreEqual(farming.Slot, miner.Slot);
        Assert.AreEqual(farming.TintAlias, miner.TintAlias);
    }
}
