using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// FactoryPacketEquipToolRequest (188/7). EXPERIMENTAL / debug-only.
/// LIVE-PROVEN: header + int32 ToolId only → BC00070004000000 for Shovel ToolId=4.
/// Packet ends immediately after ToolId (8 bytes total).
/// </summary>
public class FactoryPacketEquipToolRequest : BaseFactoryPacket, IDeserializable<FactoryPacketEquipToolRequest>
{
    public new const short OpCode = 7;

    /// <summary>ToolId from ListTools / SelectTool (Shovel = 4).</summary>
    public int ToolId;

    public FactoryPacketEquipToolRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out FactoryPacketEquipToolRequest value)
    {
        value = new FactoryPacketEquipToolRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.ToolId))
            return false;

        return reader.RemainingLength == 0;
    }
}
