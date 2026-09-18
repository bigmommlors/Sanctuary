using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// C2S Leave/Exit on the encounter offer/instance UI.
// Live Bandit capture (Gateway 2026-09-17 18:59:21): family=41 message=109,
// payload 29006D0000000000E9030000 = H1=0, H2=1001, no 8-byte suffix (unlike 108).
// Named EncounterParticipantRequestExitPacket in PacketReaderExtensions.
public sealed class EncounterParticipantRequestExitPacket
    : BaseEncounterInstancePacket, IDeserializable<EncounterParticipantRequestExitPacket>
{
    public new const short OpCode = 109;

    private EncounterParticipantRequestExitPacket() : base(OpCode, 0, 0) { }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantRequestExitPacket value)
    {
        value = new EncounterParticipantRequestExitPacket();
        var reader = new PacketReader(data);
        return value.TryRead(ref reader) && reader.RemainingLength == 0;
    }
}
