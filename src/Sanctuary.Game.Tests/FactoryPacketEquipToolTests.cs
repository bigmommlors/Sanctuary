using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class FactoryPacketEquipToolTests
{
    [TestMethod]
    public void LiveShovel_DeserializesEquipToolRequestToolId4()
    {
        // LIVE-PROVEN C2S: BC 00 07 00 04 00 00 00 (i16 188, i16 7, int32 ToolId 4)
        var data = Convert.FromHexString("BC00070004000000");

        Assert.IsTrue(FactoryPacketEquipToolRequest.TryDeserialize(data, out var request));
        Assert.AreEqual(4, request.ToolId);
    }

    [TestMethod]
    public void ShovelSuccess_SerializesExactEquipToolResponseBytes()
    {
        // PROVEN S2C: BC 00 17 00 01 04 00 00 00 (i16 188, i16 23, uint8 Success 1, int32 ToolId 4)
        var expected = Convert.FromHexString("BC0017000104000000");

        var bytes = FactoryPacketEquipToolResponse.CreateExperimentalSuccess(4).Serialize();

        Assert.AreEqual(9, bytes.Length);
        Assert.AreEqual(188, BitConverter.ToInt16(bytes, 0));
        Assert.AreEqual(23, BitConverter.ToInt16(bytes, 2));
        Assert.AreEqual(1, bytes[4]);
        Assert.AreEqual(4, BitConverter.ToInt32(bytes, 5));
        CollectionAssert.AreEqual(expected, bytes);
        Assert.AreEqual("BC0017000104000000", Convert.ToHexString(bytes));
    }

    [TestMethod]
    public void RejectsTruncatedOrPaddedEquipToolRequest()
    {
        Assert.IsFalse(FactoryPacketEquipToolRequest.TryDeserialize(
            Convert.FromHexString("BC000700040000"), out _));

        Assert.IsFalse(FactoryPacketEquipToolRequest.TryDeserialize(
            Convert.FromHexString("BC0007000400000000"), out _));
    }

    [TestMethod]
    public void UnsupportedToolId_SerializesSuccess0WithEchoedToolId()
    {
        // Unsupported ToolId=5: Success=0, echoed ToolId → BC0017000005000000
        var expected = Convert.FromHexString("BC0017000005000000");

        var response = FactoryPacketEquipToolResponse.CreateExperimentalFailure(5);
        var bytes = response.Serialize();

        Assert.IsFalse(response.Success);
        Assert.AreEqual(5, response.ToolId);
        Assert.AreEqual(9, bytes.Length);
        Assert.AreEqual(188, BitConverter.ToInt16(bytes, 0));
        Assert.AreEqual(23, BitConverter.ToInt16(bytes, 2));
        Assert.AreEqual(0, bytes[4]);
        Assert.AreEqual(5, BitConverter.ToInt32(bytes, 5));
        CollectionAssert.AreEqual(expected, bytes);
        Assert.AreEqual("BC0017000005000000", Convert.ToHexString(bytes));
    }

    [TestMethod]
    public void UnsupportedToolId999_SerializesSuccess0WithEchoedToolId()
    {
        var response = FactoryPacketEquipToolResponse.CreateExperimentalFailure(999);
        var bytes = response.Serialize();

        Assert.IsFalse(response.Success);
        Assert.AreEqual(999, response.ToolId);
        Assert.AreEqual(9, bytes.Length);
        Assert.AreEqual(0, bytes[4]);
        Assert.AreEqual(999, BitConverter.ToInt32(bytes, 5));
    }
}
