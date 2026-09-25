using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// FactoryPacketOpenToolshed (188/26). EXPERIMENTAL / debug-only.
/// Proven empty-type layout: i16 188, i16 26, empty string (i32 length 0) → BC001A0000000000.
/// </summary>
public class FactoryPacketOpenToolshed : BaseFactoryPacket, ISerializablePacket
{
    public new const short OpCode = 26;

    /// <summary>Type/filter string. Empty/null serializes as int32 length 0.</summary>
    public string? Type;

    public FactoryPacketOpenToolshed() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Type);

        return writer.Buffer;
    }
}
