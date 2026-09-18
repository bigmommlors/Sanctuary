using System;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.IO;
using Sanctuary.Game.Resources;
using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class ActivityPacketTests
{
    private static ClientActivityDefinitionCollection LoadDefinitions()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Resources", "ClientActivityDefinitions.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "Build the solution to copy its activity resource before running this test.");
        var definitions = new ClientActivityDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(definitions.Load(Path.Combine(directory.FullName, "Resources", "ClientActivityDefinitions.json")));
        return definitions;
    }

    [TestMethod]
    public void ReferenceResource_LoadsBothServerTypes()
    {
        var definitions = LoadDefinitions();
        Assert.AreEqual(417, definitions.Count);
        Assert.AreEqual(179, definitions.Values.Count(x => x.ServerType == 2));
        Assert.AreEqual(238, definitions.Values.Count(x => x.ServerType == 1));
    }

    [TestMethod]
    [DataRow(2, 179)]
    [DataRow(1, 238)]
    public void List_SerializesReferenceHeaderAndAllEntries(int serverType, int expectedCount)
    {
        var activities = LoadDefinitions().Values.Where(x => x.ServerType == serverType).ToList();
        var packet = new ActivityPacketListOfActivities { ServerType = serverType, Activities = activities };
        using var stream = new MemoryStream(packet.Serialize());
        using var reader = new BinaryReader(stream);
        CollectionAssert.AreEqual(Convert.FromHexString("A7000101"), reader.ReadBytes(4));
        Assert.AreEqual(serverType, reader.ReadInt32());
        Assert.AreEqual(expectedCount, reader.ReadInt32());

        // Walk the reference wire layout independently, including variable-length entries.
        foreach (var activity in activities)
        {
            Assert.AreEqual(activity.Id, reader.ReadInt32());
            reader.ReadBytes(8 * sizeof(int));
            Assert.AreEqual(serverType, reader.ReadInt32());
            reader.ReadBoolean();
            reader.ReadInt32();
            var featuredCount = reader.ReadInt32();
            Assert.AreEqual(activity.FeaturedActivities.Count, featuredCount);
            // Dictionary key + two Int64s + three Int32s + one Boolean.
            Assert.AreEqual(featuredCount * 33, reader.ReadBytes(featuredCount * 33).Length);
            reader.ReadInt32();
            reader.ReadBoolean();
            for (var i = 0; i < 2; i++)
            {
                var length = reader.ReadInt32();
                Assert.IsGreaterThanOrEqualTo(0, length);
                Assert.AreEqual(length, reader.ReadBytes(length).Length);
            }
            reader.ReadBytes(4 * sizeof(int));
        }
        Assert.AreEqual(stream.Length, stream.Position, "Serialized count must cover exactly all entries.");
    }

    [TestMethod]
    public void Header_RejectsTruncationAndWrongOpcodes()
    {
        var packet = new ActivityPacketListOfActivities();
        var header = Convert.FromHexString("A7000101");
        var validReader = new PacketReader(header);
        Assert.IsTrue(packet.TryRead(ref validReader));
        for (var length = 0; length < header.Length; length++)
        {
            var reader = new PacketReader(header.AsSpan(0, length));
            Assert.IsFalse(packet.TryRead(ref reader));
        }
        for (var index = 0; index < header.Length; index++)
        {
            var invalid = (byte[])header.Clone();
            invalid[index] ^= 0x10;
            var reader = new PacketReader(invalid);
            Assert.IsFalse(packet.TryRead(ref reader));
        }
    }
}
