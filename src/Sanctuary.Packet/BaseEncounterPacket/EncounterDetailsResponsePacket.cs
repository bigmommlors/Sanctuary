using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Restricted empty-collection OFFER layout for controlled local tests.
// Participant/team collections remain empty (experimental). Authenticated offer
// identity fields follow reassembled retail initial-offer conventions:
// commonFirstInts=[H1,2], MiniGameInfo final Int32=H1, packet-tail Int32=ActivityId,
// Launch=false, store bundles={2194,2919,2920,2921,3752}.
public sealed class EncounterDetailsResponsePacket(int headerValue1, int headerValue2)
    : BaseEncounterInstancePacket(OpCode, headerValue1, headerValue2), ISerializablePacket
{
    public new const short OpCode = 114;

    public static readonly int[] AuthenticInitialOfferStoreBundleIds = [2194, 2919, 2920, 2921, 3752];

    public required int NameId { get; init; }
    public required int IconId { get; init; }
    public required int DescriptionId { get; init; }
    public required int Difficulty { get; init; }
    public required int ProfileType { get; init; }
    public required int MiniGameType { get; init; }
    public required bool MembersOnly { get; init; }
    public required int ZoneContext { get; init; }
    public required int TeleportEffectId { get; init; }
    public required bool Tutorial { get; init; }
    public required int RespawnTime { get; init; }
    public required int ActivityId { get; init; }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);

        writer.Write(HeaderValue1); // Common first Int32 = H1 (authentic initial offer)
        writer.Write(2); // Common second Int32 (authentic initial offer)
        writer.Write(0); // Unknown collection count (experimental empty participants)
        writer.Write(0); // Team collection count (experimental empty teams)
        writer.Write(ZoneContext);
        writer.Write(TeleportEffectId);
        writer.Write(true); // Reference Unknown5
        writer.Write(false); // Reference Unknown6
        writer.Write(Tutorial);
        writer.Write(0); // Common Unknown8
        writer.Write(RespawnTime);

        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(DescriptionId);
        writer.Write(Difficulty);
        writer.Write(ProfileType);
        writer.Write(MiniGameType);
        writer.Write(MembersOnly);
        WriteAuthenticEmptyRewardBundle(writer);
        WriteAuthenticEmptyRewardBundle(writer);
        WriteAuthenticEmptyRewardBundle(writer);
        writer.Write(0); // Objective count (may be empty)
        for (var i = 0; i < 5; i++) writer.Write(true); // Reference U8..U12
        writer.Write((string?)null); // U13
        writer.Write(1); // U14
        writer.Write(true); // U15
        writer.Write(0); // PreselectedGameId
        for (var i = 0; i < 4; i++) writer.Write(false); // U16..U19
        writer.Write(HeaderValue1); // MiniGameInfo final Int32 = H1 (authentic initial offer)

        writer.Write(false); // Common UNK0
        writer.Write(true); // Common UNK1: reference offer visibility gate
        writer.Write(false); // Launch=false: only the offer form
        writer.Write(ActivityId); // Packet-tail Int32 = ActivityId (authentic initial offer)
        writer.Write(AuthenticInitialOfferStoreBundleIds.Length);
        foreach (var storeBundleId in AuthenticInitialOfferStoreBundleIds)
            writer.Write(storeBundleId);
        return writer.Buffer;
    }

    private static void WriteAuthenticEmptyRewardBundle(PacketWriter writer)
    {
        // 69-byte empty form: bool + nine Int32s + two UInt64s + icon/name + entry count + final Int32.
        // Authentic empty defaults include float 1.0 (1065353216) and icon/name = -1.
        writer.Write(false);
        for (var i = 0; i < 6; i++)
            writer.Write(0);
        writer.Write(1065353216); // float 1.0 bit pattern
        writer.Write(0);
        writer.Write(0);
        writer.Write(0L); // SourceGuid
        writer.Write(0L); // PlayerGuid
        writer.Write(-1); // IconId
        writer.Write(-1); // NameId
        writer.Write(0); // Entry count
        writer.Write(0); // Final Int32
    }
}
