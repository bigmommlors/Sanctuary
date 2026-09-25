using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Farming;
using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

/// <summary>
/// EXPERIMENTAL diganim: PlayerUpdatePacketSetAnimation farm_dig (3900003) + farm-only command wiring.
/// </summary>
[TestClass]
public sealed class FarmingDigAnimExperimentTests
{
    [TestMethod]
    public void CreatePlayNowPacket_UsesProvenFarmDigIdAndPlayNowFlags()
    {
        const ulong guid = 0x1122334455667788UL;

        var packet = FarmingDigAnimExperiment.CreatePlayNowPacket(guid);

        Assert.AreEqual(guid, packet.Guid);
        Assert.AreEqual(3900003, packet.AnimationId);
        Assert.AreEqual(FarmingDigAnimExperiment.FarmDigAnimationId, packet.AnimationId);
        Assert.AreEqual(0, packet.Unknown);
        Assert.AreEqual((byte)0, packet.Flags);
        Assert.AreEqual(FarmingDigAnimExperiment.PlayNowFlags, packet.Flags);
    }

    [TestMethod]
    public void CreatePlayNowPacket_SerializesExistingSetAnimationLayout()
    {
        // Existing PlayerUpdatePacketSetAnimation wire:
        // i16 BaseOp=35, i16 SubOp=8, u64 Guid, i32 AnimationId, i32 Unknown, u8 Flags
        const ulong guid = 0x0102030405060708UL;
        var packet = FarmingDigAnimExperiment.CreatePlayNowPacket(guid);
        var bytes = packet.Serialize();

        Assert.AreEqual(21, bytes.Length);
        Assert.AreEqual(35, BitConverter.ToInt16(bytes, 0));
        Assert.AreEqual(PlayerUpdatePacketSetAnimation.OpCode, BitConverter.ToInt16(bytes, 2));
        Assert.AreEqual(8, BitConverter.ToInt16(bytes, 2));
        Assert.AreEqual(guid, BitConverter.ToUInt64(bytes, 4));
        Assert.AreEqual(3900003, BitConverter.ToInt32(bytes, 12));
        Assert.AreEqual(0, BitConverter.ToInt32(bytes, 16));
        Assert.AreEqual(0, bytes[20]);
    }

    [TestMethod]
    public void FarmOnlyRestriction_MessageDocumentsWildsFarmRequirement()
    {
        StringAssert.Contains(FarmingDigAnimExperiment.NotInWildsFarmMessage, "Wilds Farm");
        StringAssert.Contains(FarmingDigAnimExperiment.NotInWildsFarmMessage, "farmtest enter");
    }

    [TestMethod]
    public void FarmTestChatCommand_UsageIncludesDiganim()
    {
        var cmd = new FarmTestChatCommand(null!, null!, null!);

        StringAssert.Contains(cmd.Usage, "diganim");
        Assert.AreEqual("farmtest", cmd.KeyWord);
    }
}
