using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public sealed class EncounterZoneIsReadyPacket(int headerValue1, int headerValue2)
    : BaseEncounterInstancePacket(OpCode, headerValue1, headerValue2), ISerializablePacket
{
    public new const short OpCode = 107;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        return writer.Buffer;
    }
}
