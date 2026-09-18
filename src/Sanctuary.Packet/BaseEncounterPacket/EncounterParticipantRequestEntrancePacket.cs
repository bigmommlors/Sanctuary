using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public sealed class EncounterParticipantRequestEntrancePacket
    : BaseEncounterInstancePacket, IDeserializable<EncounterParticipantRequestEntrancePacket>
{
    public new const short OpCode = 108;

    // All four distinct archived entrance frames have an eight-byte suffix.
    // Preserve it verbatim; its semantic type/meaning is not established here.
    public byte[] UnknownData { get; private set; } = [];

    private EncounterParticipantRequestEntrancePacket() : base(OpCode, 0, 0) { }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantRequestEntrancePacket value)
    {
        value = new EncounterParticipantRequestEntrancePacket();
        var reader = new PacketReader(data);
        if (!value.TryRead(ref reader) || !reader.TryReadExact(8, out var suffix) || reader.RemainingLength != 0)
            return false;

        value.UnknownData = suffix.ToArray();
        return true;
    }
}
