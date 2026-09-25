using System;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class FactoryPacketListToolsTests
{
    [TestMethod]
    public void ZeroTool_SerializesProvenListToolsResponseBytes()
    {
        // Proven packet: BC 00 16 00 00 00 00 00 (i16 188, i16 22, int32 count 0)
        var expected = Convert.FromHexString("BC00160000000000");

        CollectionAssert.AreEqual(expected, new FactoryPacketListToolsResponse().Serialize());
    }

    [TestMethod]
    public void ZeroPayload_AcceptsProvenListToolsRequestBytes()
    {
        // Proven C2S: BC 00 06 00 (i16 188, i16 6, no payload)
        var data = Convert.FromHexString("BC000600");

        Assert.IsTrue(FactoryPacketListToolsRequest.TryDeserialize(data, out _));
    }

    [TestMethod]
    public void OneShovel_SerializesExperimentalListToolsResponse()
    {
        var bytes = FactoryPacketListToolsResponse.CreateExperimentalOneShovel().Serialize();

        // Family 188, sub 22
        Assert.AreEqual(188, BitConverter.ToInt16(bytes, 0));
        Assert.AreEqual(22, BitConverter.ToInt16(bytes, 2));

        // HashList count = 1
        Assert.AreEqual(1, BitConverter.ToInt32(bytes, 4));

        // map key = 4 (experimental assumption)
        Assert.AreEqual(4, BitConverter.ToInt32(bytes, 8));

        // Body ToolId, Housing, Name, Icon, Requirement, Composite, ToolItem, Charge, Tooltip
        Assert.AreEqual(4, BitConverter.ToInt32(bytes, 12));
        Assert.AreEqual(58, BitConverter.ToInt32(bytes, 16));
        Assert.AreEqual(31036, BitConverter.ToInt32(bytes, 20));
        Assert.AreEqual(34958, BitConverter.ToInt32(bytes, 24));
        Assert.AreEqual(970, BitConverter.ToInt32(bytes, 28));
        Assert.AreEqual(0, BitConverter.ToInt32(bytes, 32));
        Assert.AreEqual(0, BitConverter.ToInt32(bytes, 36));
        Assert.AreEqual(0, BitConverter.ToInt32(bytes, 40));
        Assert.AreEqual(435253, BitConverter.ToInt32(bytes, 44));

        // DeleteConsumablesOnUnequip = 0
        Assert.AreEqual(0, bytes[48]);

        // NotificationType = 197
        Assert.AreEqual(197, BitConverter.ToInt32(bytes, 49));

        // Hidden = 0
        Assert.AreEqual(0, bytes[53]);

        // FACTORY_CATEGORY "FARMING" (len 7, no NUL / no padding)
        Assert.AreEqual(7, BitConverter.ToInt32(bytes, 54));
        Assert.AreEqual("FARMING", Encoding.ASCII.GetString(bytes, 58, 7));

        // NeedsFuelNotificationType, IsBlueprintStamper, HideIfRequirementFails
        Assert.AreEqual(0, BitConverter.ToInt32(bytes, 65));
        Assert.AreEqual(0, bytes[69]);
        Assert.AreEqual(0, bytes[70]);

        // IsCurrentlyUseable = 1 (experimental controlled TRUE)
        Assert.AreEqual(1, bytes[71]);

        // Exact length: header 4 + count 4 + record (57 + 7) = 72; no trailing padding
        Assert.AreEqual(72, bytes.Length);

        var expectedHex =
            "BC001600" +
            "01000000" +
            "04000000" +
            "04000000" +
            "3A000000" +
            "3C790000" +
            "8E880000" +
            "CA030000" +
            "00000000" +
            "00000000" +
            "00000000" +
            "35A40600" +
            "00" +
            "C5000000" +
            "00" +
            "07000000" +
            "4641524D494E47" +
            "00000000" +
            "00" +
            "00" +
            "01";

        Assert.AreEqual(expectedHex, Convert.ToHexString(bytes));
    }
}
