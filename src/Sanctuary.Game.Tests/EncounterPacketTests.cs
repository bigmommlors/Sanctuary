using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class EncounterPacketTests
{
    // Actual complete inner payloads from the public OSFR captures. Header values are
    // preserved as opaque values, not used as Bandit Hideout configuration.
    [TestMethod]
    [DataRow(0x000200AE)] // p12.pcap, UDP index 21964
    [DataRow(0x000E003E)] // packets_racing_minigame.pcapng
    [DataRow(0x00070048)] // packets_soccer_minigame.pcapng
    [DataRow(0x00140041)] // packets_demoderby_minigame.pcapng
    public void CapturedStateReadyAndEntrance_HaveConsistentHeaders(int headerValue1)
    {
        var prefix = new byte[12];
        BinaryPrimitives.WriteInt16LittleEndian(prefix, 41);
        BinaryPrimitives.WriteInt16LittleEndian(prefix.AsSpan(2), 106);
        BinaryPrimitives.WriteInt32LittleEndian(prefix.AsSpan(4), headerValue1);
        BinaryPrimitives.WriteInt32LittleEndian(prefix.AsSpan(8), 1008);
        byte[] expectedState = [.. prefix, 2, 0, 0, 0];
        CollectionAssert.AreEqual(expectedState, new EncounterStatePacket(headerValue1, 1008, 2).Serialize());

        prefix[2] = 107;
        CollectionAssert.AreEqual(prefix, new EncounterZoneIsReadyPacket(headerValue1, 1008).Serialize());

        prefix[2] = 108;
        var suffix = Convert.FromHexString("91CA73956ACC524B");
        Assert.IsTrue(EncounterParticipantRequestEntrancePacket.TryDeserialize([.. prefix, .. suffix], out var request));
        Assert.AreEqual(headerValue1, request.HeaderValue1);
        Assert.AreEqual(1008, request.HeaderValue2);
        CollectionAssert.AreEqual(suffix, request.UnknownData);
    }

    [TestMethod]
    public void Entrance_RejectsWrongHeadersTruncationAndExtraBytes()
    {
        var bytes = Convert.FromHexString("29006C00AE000200F003000091CA73956ACC524B");
        for (var length = 0; length < bytes.Length; length++)
            Assert.IsFalse(EncounterParticipantRequestEntrancePacket.TryDeserialize(bytes.AsSpan(0, length), out _));
        for (var index = 0; index < 4; index++)
        {
            var invalid = (byte[])bytes.Clone();
            invalid[index] ^= 0x10;
            Assert.IsFalse(EncounterParticipantRequestEntrancePacket.TryDeserialize(invalid, out _));
        }
        Assert.IsFalse(EncounterParticipantRequestEntrancePacket.TryDeserialize([.. bytes, 0], out _));
    }

    [TestMethod]
    public void LiveBanditExit_DeserializesTwelveByteLayout()
    {
        // Gateway 2026-09-17 18:59:21 Bandit Leave: family=41 message=109 H1=0 H2=1001
        var bytes = Convert.FromHexString("29006D0000000000E9030000");
        Assert.IsTrue(EncounterParticipantRequestExitPacket.TryDeserialize(bytes, out var exit));
        Assert.AreEqual(0, exit.HeaderValue1);
        Assert.AreEqual(1001, exit.HeaderValue2);
        Assert.IsFalse(EncounterParticipantRequestExitPacket.TryDeserialize(bytes.AsSpan(0, 11), out _));
        Assert.IsFalse(EncounterParticipantRequestExitPacket.TryDeserialize([.. bytes, 0], out _));
    }

    [TestMethod]
    public void EmptyOffer_UsesAuthenticInitialOfferIdentityFields()
    {
        // Synthetic sentinel values test offsets. These are not a sendable activity configuration.
        var bytes = new EncounterDetailsResponsePacket(101, 102)
        {
            ZoneContext = 103, TeleportEffectId = 104, Tutorial = true, RespawnTime = 105,
            NameId = 106, IconId = 107, DescriptionId = 108, Difficulty = 109,
            ProfileType = 110, MiniGameType = 111, MembersOnly = true, ActivityId = 112
        }.Serialize();
        Assert.AreEqual(340, bytes.Length);
        CollectionAssert.AreEqual(Convert.FromHexString("290072006500000066000000"), bytes[..12]);
        Assert.AreEqual(101, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12, 4))); // commonFirstInts[0]=H1
        Assert.AreEqual(2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16, 4))); // commonFirstInts[1]=2
        CollectionAssert.AreEqual(new byte[8], bytes[20..28]); // Empty participant/team counts
        foreach (var (offset, value) in new[] { (28, 103), (32, 104), (43, 105), (47, 106), (51, 107),
                     (55, 108), (59, 109), (63, 110), (67, 111), (305, 101) })
            Assert.AreEqual(value, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4)));
        CollectionAssert.AreEqual(new byte[] { 1, 0, 1 }, bytes[36..39]);
        Assert.AreEqual((byte)1, bytes[71]);
        Assert.AreEqual(1065353216, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(72 + 1 + 24, 4))); // float 1.0 in first bundle
        Assert.AreEqual(-1, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(72 + 1 + 36 + 16, 4))); // icon -1
        Assert.AreEqual(-1, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(72 + 1 + 36 + 20, 4))); // name -1
        CollectionAssert.AreEqual(new byte[4], bytes[279..283]); // No objectives
        CollectionAssert.AreEqual(Convert.FromHexString("01010101010000000001000000010000000000000000"), bytes[283..305]);
        CollectionAssert.AreEqual(Convert.FromHexString("000100"), bytes[309..312]); // Offer gate, Launch=false
        Assert.AreEqual(112, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(312, 4))); // packet-tail ActivityId
        Assert.AreEqual(5, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(316, 4)));
        CollectionAssert.AreEqual(EncounterDetailsResponsePacket.AuthenticInitialOfferStoreBundleIds,
            Enumerable.Range(0, 5).Select(i => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(320 + i * 4, 4))).ToArray());
    }

    [TestMethod]
    public void BanditHideout_ReferenceMetadataDoesNotSupplyEncounterHeaderValues()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Resources", "ClientActivityDefinitions.json")))
            directory = directory.Parent;
        Assert.IsNotNull(directory);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "Resources", "ClientActivityDefinitions.json")));
        var activity = json.RootElement.EnumerateArray().Single(x => x.GetProperty("Id").GetInt32() == 29);
        Assert.AreEqual("Bandit Hideout", activity.GetProperty("Comment").GetString());
        Assert.AreEqual(1, activity.GetProperty("ServerType").GetInt32());
        Assert.AreEqual(2, activity.GetProperty("AppSystemId").GetInt32());
        Assert.AreEqual(10, activity.GetProperty("Category").GetInt32());
        Assert.AreEqual(5172, activity.GetProperty("NameId").GetInt32());
        Assert.AreEqual(425259, activity.GetProperty("DisplayNameId").GetInt32());
        Assert.AreEqual(6995, activity.GetProperty("DescriptionId").GetInt32());
        Assert.AreEqual(1, activity.GetProperty("Difficulty").GetInt32());
        Assert.AreEqual(1345, activity.GetProperty("ImageSetId").GetInt32());
        Assert.IsFalse(activity.TryGetProperty("InstanceId", out _));
        Assert.IsFalse(activity.TryGetProperty("EncounterId", out _));
    }

    [TestMethod]
    public void ExistingOverworldCombatSerialization_IsUnchanged()
    {
        CollectionAssert.AreEqual(Convert.FromHexString("2900840001"),
            new EncounterOverworldCombatPacket { InWorldCombat = true }.Serialize());
    }
}
