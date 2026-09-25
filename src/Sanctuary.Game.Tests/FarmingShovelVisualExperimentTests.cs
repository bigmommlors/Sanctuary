using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Farming;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

/// <summary>
/// EXPERIMENTAL shovelvisual: temporary farming shovel ADR attach + farm_dig + restore policy.
/// </summary>
[TestClass]
public sealed class FarmingShovelVisualExperimentTests
{
    [TestMethod]
    public void CreateExperimentalAttachment_UsesProvenAdrFieldsAndExperimentalSlot()
    {
        var attachment = FarmingShovelVisualExperiment.CreateExperimentalAttachment();

        Assert.AreEqual("tool_ar_ag_weapon_farmingshovel.adr", attachment.ModelName);
        Assert.AreEqual("freestyle-farming-M", attachment.TextureAlias);
        Assert.AreEqual("dyetint", attachment.TintAlias);
        Assert.AreEqual(0, attachment.TintId);
        Assert.AreEqual(0, attachment.CompositeEffectId);
        Assert.AreEqual(7, attachment.Slot);
        Assert.AreEqual(FarmingShovelVisualExperiment.ExperimentalSlot, attachment.Slot);
    }

    [TestMethod]
    public void ExperimentalCandidates_AreDocumentedNotRetailProven()
    {
        Assert.AreEqual(7, FarmingShovelVisualExperiment.ExperimentalSlot);
        Assert.AreEqual(0, FarmingShovelVisualExperiment.ExperimentalWieldType);
        Assert.AreEqual(0, FarmingShovelVisualExperiment.ExperimentalItemInstanceId);
        Assert.AreEqual(2500, FarmingShovelVisualExperiment.ExperimentalVisualDurationMs);
    }

    [TestMethod]
    public void CreatePlayNowPacket_StillUsesFarmDig3900003()
    {
        var packet = FarmingDigAnimExperiment.CreatePlayNowPacket(42);
        Assert.AreEqual(3900003, packet.AnimationId);
    }

    [TestMethod]
    public void CreateSelfEquipPacket_SerializesModelNameAndEquipFlag()
    {
        var attachment = FarmingShovelVisualExperiment.CreateExperimentalAttachment();
        var packet = FarmingShovelVisualExperiment.CreateSelfEquipPacket(
            attachment, profileId: 1, itemInstanceGuid: 0, equip: true);
        var bytes = packet.Serialize();

        Assert.IsTrue(bytes.Length > 20);
        Assert.AreEqual(ClientUpdatePacketEquipItem.OpCode, BitConverter.ToInt16(bytes, 2));
        StringAssert.Contains(System.Text.Encoding.ASCII.GetString(bytes), "tool_ar_ag_weapon_farmingshovel.adr");
        StringAssert.Contains(System.Text.Encoding.ASCII.GetString(bytes), "freestyle-farming-M");
        Assert.AreEqual(0, packet.Guid);
        Assert.IsTrue(packet.Equip);
    }

    [TestMethod]
    public void CreateVisibleEquipPacket_UsesZeroItemIdSentinelNotInventedCatalogId()
    {
        var attachment = FarmingShovelVisualExperiment.CreateExperimentalAttachment();
        var packet = FarmingShovelVisualExperiment.CreateVisibleEquipPacket(
            playerGuid: 99,
            itemInstanceId: FarmingShovelVisualExperiment.ExperimentalItemInstanceId,
            attachment,
            profileId: 1,
            wieldType: FarmingShovelVisualExperiment.ExperimentalWieldType);

        Assert.AreEqual(0, packet.Id);
        Assert.AreEqual(0, packet.WieldType);
        Assert.AreEqual(7, packet.Attachment.Slot);
        Assert.AreEqual(99UL, packet.Guid);

        var bytes = packet.Serialize();
        Assert.AreEqual(PlayerUpdatePacketEquipItemChange.OpCode, BitConverter.ToInt16(bytes, 2));
        Assert.AreEqual(0, BitConverter.ToInt32(bytes, 12)); // Id after Guid
    }

    [TestMethod]
    public void TryCreateSnapshot_RejectsEquippedSlotWithoutAttachment()
    {
        var ok = FarmingShovelVisualExperiment.TryCreateSnapshot(
            profileId: 1,
            slotHasProfileItem: true,
            attachment: null,
            wieldType: 0,
            profileItemId: 55,
            out _,
            out var reject);

        Assert.IsFalse(ok);
        StringAssert.Contains(reject, "safely");
    }

    [TestMethod]
    public void TryCreateSnapshot_AllowsEmptySlot()
    {
        var ok = FarmingShovelVisualExperiment.TryCreateSnapshot(
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
        Assert.IsNull(snapshot.ProfileItemId);
    }

    [TestMethod]
    public void TryCreateSnapshot_ClonesAttachmentForEquippedWeapon()
    {
        var source = new CharacterAttachmentData
        {
            ModelName = "tool_ar_ag_weapon_shovel.adr",
            TextureAlias = "miner-steel-L2",
            TintAlias = "dyetint",
            TintId = 3,
            CompositeEffectId = 0,
            Slot = 7
        };

        var ok = FarmingShovelVisualExperiment.TryCreateSnapshot(
            profileId: 2,
            slotHasProfileItem: true,
            attachment: source,
            wieldType: 0,
            profileItemId: 1910,
            out var snapshot,
            out _);

        Assert.IsTrue(ok);
        Assert.IsTrue(snapshot.HadEquippedItem);
        Assert.AreEqual(1910, snapshot.ProfileItemId);
        Assert.AreEqual("tool_ar_ag_weapon_shovel.adr", snapshot.Attachment!.ModelName);
        source.ModelName = "mutated";
        Assert.AreEqual("tool_ar_ag_weapon_shovel.adr", snapshot.Attachment.ModelName);
    }

    [TestMethod]
    public void DueTiming_MatchesExperimentalDuration()
    {
        var start = DateTimeOffset.Parse("2026-09-24T12:00:00Z");
        var due = FarmingShovelVisualExperiment.ComputeDueAtUtc(start);
        Assert.AreEqual(start.AddMilliseconds(2500), due);
        Assert.IsFalse(FarmingShovelVisualExperiment.IsDue(start.AddMilliseconds(2499), due));
        Assert.IsTrue(FarmingShovelVisualExperiment.IsDue(start.AddMilliseconds(2500), due));
    }

    [TestMethod]
    public void FarmOnlyAndDuplicateMessages_Documented()
    {
        StringAssert.Contains(FarmingShovelVisualExperiment.NotInWildsFarmMessage, "Wilds Farm");
        StringAssert.Contains(FarmingShovelVisualExperiment.AlreadyRunningMessage, "already running");
    }

    [TestMethod]
    public void FarmTestChatCommand_UsageIncludesShovelvisual()
    {
        var cmd = new FarmTestChatCommand(null!, null!, null!);
        StringAssert.Contains(cmd.Usage, "shovelvisual");
        StringAssert.Contains(cmd.Usage, "diganim");
        StringAssert.Contains(cmd.Usage, "minervisual");
        Assert.AreEqual(ChatCommandRole.Admin, cmd.RequiredRole);
    }

    [TestMethod]
    public void CreateVisibleClearSlot_OnlySetsSlotForUnequipStyle()
    {
        var packet = FarmingShovelVisualExperiment.CreateVisibleClearSlotPacket(
            1, 0, slot: 7, profileId: 1, wieldType: 0);

        Assert.AreEqual(7, packet.Attachment.Slot);
        Assert.IsTrue(string.IsNullOrEmpty(packet.Attachment.ModelName));
    }
}
