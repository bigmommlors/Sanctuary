using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// The 106/107/108/114 capture headers contain two Int32 values after family/message.
// Keep the existing BaseEncounterPacket (used by overworld combat) unchanged.
// These values are opaque here: the capture does not establish an ActivityId/instance mapping.
public abstract class BaseEncounterInstancePacket
{
    public const short OpCode = 41;
    private readonly short _message;

    public int HeaderValue1 { get; protected set; }
    public int HeaderValue2 { get; protected set; }

    protected BaseEncounterInstancePacket(short message, int headerValue1, int headerValue2)
    {
        _message = message;
        HeaderValue1 = headerValue1;
        HeaderValue2 = headerValue2;
    }

    protected void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(_message);
        writer.Write(HeaderValue1);
        writer.Write(HeaderValue2);
    }

    protected bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short family) || family != OpCode ||
            !reader.TryRead(out short message) || message != _message ||
            !reader.TryRead(out int headerValue1) || !reader.TryRead(out int headerValue2))
            return false;

        HeaderValue1 = headerValue1;
        HeaderValue2 = headerValue2;
        return true;
    }
}
