using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// FactoryPacketListToolsResponse (188/22). EXPERIMENTAL / debug-only.
/// Proven zero-tool layout: i16 188, i16 22, int32 count 0 → BC00160000000000.
/// One-tool HashList: count + (mapKey + node body + useable) per §§17/25 — revert by clearing Tools.
/// </summary>
public class FactoryPacketListToolsResponse : BaseFactoryPacket, ISerializablePacket
{
    public new const short OpCode = 22;

    /// <summary>
    /// HashList entries. Empty → count 0 (proven blank toolshed).
    /// Controlled one-Shovel experiment: add <see cref="FactoryToolDefinitionNode.CreateExperimentalShovel"/>.
    /// </summary>
    public List<FactoryToolDefinitionNode> Tools { get; } = new();

    public FactoryPacketListToolsResponse() : base(OpCode)
    {
    }

    /// <summary>
    /// EXPERIMENTAL controlled local test: one ToolId 4 Shovel (FactoryTools.txt-aligned).
    /// Not a historical retail claim. Revert by constructing with empty Tools.
    /// </summary>
    public static FactoryPacketListToolsResponse CreateExperimentalOneShovel()
    {
        var packet = new FactoryPacketListToolsResponse();
        packet.Tools.Add(FactoryToolDefinitionNode.CreateExperimentalShovel());
        return packet;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        // Proven HashList: int32 count, then count records (mapKey + body + useable).
        writer.Write(Tools.Count);

        foreach (var tool in Tools)
            tool.Serialize(writer);

        return writer.Buffer;
    }
}
