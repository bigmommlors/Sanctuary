using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class FactoryPacketOpenToolshedTests
{
    [TestMethod]
    public void EmptyType_SerializesProvenOpenToolshedBytes()
    {
        // Proven packet: BC 00 1A 00 00 00 00 00 (i16 188, i16 26, empty string)
        var expected = Convert.FromHexString("BC001A0000000000");

        CollectionAssert.AreEqual(expected, new FactoryPacketOpenToolshed().Serialize());
        CollectionAssert.AreEqual(expected, new FactoryPacketOpenToolshed { Type = string.Empty }.Serialize());
        CollectionAssert.AreEqual(expected, new FactoryPacketOpenToolshed { Type = null }.Serialize());
    }
}
