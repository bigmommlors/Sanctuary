using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// FactoryPacketListToolsRequest (188/6). EXPERIMENTAL / debug-only.
/// Proven empty payload after header: BC000600.
/// </summary>
public class FactoryPacketListToolsRequest : BaseFactoryPacket, IDeserializable<FactoryPacketListToolsRequest>
{
    public new const short OpCode = 6;

    public FactoryPacketListToolsRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out FactoryPacketListToolsRequest value)
    {
        value = new FactoryPacketListToolsRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
