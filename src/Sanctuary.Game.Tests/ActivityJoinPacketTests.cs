using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class ActivityJoinPacketTests
{
    [TestMethod]
    public void JoinRequest_DecodesCapturedWorldTunnelAndSignedTimezoneOffset()
    {
        var capture = Convert.FromHexString("0600010C000000A70001021D000000909DFFFF");
        Assert.IsTrue(PacketTunneledClientWorldPacket.TryDeserialize(capture, out var tunnel));
        CollectionAssert.AreEqual(Convert.FromHexString("A70001021D000000909DFFFF"), tunnel.Payload);
        Assert.IsTrue(ActivityPacketJoinActivityRequest.TryDeserialize(tunnel.Payload, out var request));
        Assert.AreEqual(29, request.ActivityId);
        Assert.AreEqual(-25200, request.TimezoneOffset);
    }

    [TestMethod]
    public void JoinRequest_RejectsTruncationWrongHeadersAndTrailingBytes()
    {
        var capture = Convert.FromHexString("A70001021D000000909DFFFF");
        for (var length = 0; length < capture.Length; length++)
            Assert.IsFalse(ActivityPacketJoinActivityRequest.TryDeserialize(capture.AsSpan(0, length), out _));

        for (var index = 0; index < 4; index++)
        {
            var invalid = (byte[])capture.Clone();
            invalid[index] ^= 0x10;
            Assert.IsFalse(ActivityPacketJoinActivityRequest.TryDeserialize(invalid, out _));
        }
        Assert.IsFalse(ActivityPacketJoinActivityRequest.TryDeserialize([.. capture, 0], out _));
    }
}
