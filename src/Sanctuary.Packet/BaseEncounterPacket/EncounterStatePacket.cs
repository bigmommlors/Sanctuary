using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public sealed class EncounterStatePacket(int headerValue1, int headerValue2, int state)
    : BaseEncounterInstancePacket(OpCode, headerValue1, headerValue2), ISerializablePacket
{
    public new const short OpCode = 106;
    public int State { get; } = state;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(State);
        return writer.Buffer;
    }
}
