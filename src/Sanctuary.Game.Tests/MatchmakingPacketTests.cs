using System;
using System.Buffers.Binary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class MatchmakingPacketTests
{
    [TestMethod]
    public void Request_ParsesCapturedGuidAndRejectsInvalidFrames()
    {
        var captured = Convert.FromHexString("8D0001001100000000000000");
        Assert.IsTrue(ListQueuesRequestPacket.TryDeserialize(captured, out var request));
        Assert.AreEqual(17UL, request.Guid);

        for (var length = 0; length < captured.Length; length++)
            Assert.IsFalse(ListQueuesRequestPacket.TryDeserialize(captured.AsSpan(0, length), out _));

        var wrongFamily = (byte[])captured.Clone();
        wrongFamily[0] = 192;
        Assert.IsFalse(ListQueuesRequestPacket.TryDeserialize(wrongFamily, out _));

        var wrongSubopcode = (byte[])captured.Clone();
        wrongSubopcode[2] = 2;
        Assert.IsFalse(ListQueuesRequestPacket.TryDeserialize(wrongSubopcode, out _));
        Assert.IsFalse(ListQueuesRequestPacket.TryDeserialize([.. captured, 0], out _));
    }

    [TestMethod]
    public void Response_UsesMatchmakingHeaderAndReferenceQueueLayout()
    {
        var response = new ListQueuesResponsePacket { Guid = 17 };
        response.Queues.Add(new MatchmakingQueueDefinition
        {
            Id = 5, NameId = 427834, MatchType = 13,
            MinPlayers = 1, MaxPlayers = 5, MinTeams = 1, MaxTeams = 1,
            MaxGameStartDelay = 30, Param5 = 420998, Param6 = 30434, Param7 = 1,
            EncounterDescriptionId = 420998, EncounterIcon = 30434,
            Unknown2 = 26, MemberOnly = true, Unknown3 = true
        });

        var bytes = response.Serialize();
        CollectionAssert.AreEqual(Convert.FromHexString("8D000200110000000000000001000000"), bytes[..16]);

        // Reference row: nineteen little-endian Int32 fields followed by two one-byte bools.
        int[] expected = [5, 427834, 13, 1, 5, 1, 1, 30, 0, 0, 0, 0, 420998, 30434, 1, 420998, 30434, 0, 26];
        Assert.AreEqual(94, bytes.Length);
        for (var i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16 + i * 4, 4)));
        Assert.AreEqual((byte)1, bytes[92]);
        Assert.AreEqual((byte)1, bytes[93]);
    }
}
