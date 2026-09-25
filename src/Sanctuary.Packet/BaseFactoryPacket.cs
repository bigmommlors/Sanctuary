using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Factory packet family (opcode 188). EXPERIMENTAL farming toolshed wiring uses this base.
/// </summary>
public class BaseFactoryPacket
{
    public const short OpCode = 188;

    private short SubOpCode;

    public BaseFactoryPacket(short subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) || subOpCode != SubOpCode)
            return false;

        return true;
    }
}
