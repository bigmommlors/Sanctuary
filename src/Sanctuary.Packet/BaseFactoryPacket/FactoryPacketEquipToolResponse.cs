using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// FactoryPacketEquipToolResponse (188/23). EXPERIMENTAL / debug-only.
/// PROVEN: i16 188, i16 23, uint8 Success, int32 ToolId → 9 bytes.
/// Exact Shovel Success: BC0017000104000000 (Success=1, ToolId=4).
/// Success=0 (e.g. unsupported ToolId=5): BC0017000005000000 — skips client equip.
/// Nonzero Success normalizes to success path — use Success=true (wire 01) for equip.
/// </summary>
public class FactoryPacketEquipToolResponse : BaseFactoryPacket, ISerializablePacket
{
    public new const short OpCode = 23;

    /// <summary>Wire uint8; false=0 skips equip, true=1 enters success path.</summary>
    public bool Success;

    /// <summary>Echoed ToolId stored as client CurrentToolId on success.</summary>
    public int ToolId;

    public FactoryPacketEquipToolResponse() : base(OpCode)
    {
    }

    /// <summary>
    /// EXPERIMENTAL: Success=1 and echo ToolId (supported Shovel ToolId=4).
    /// </summary>
    public static FactoryPacketEquipToolResponse CreateExperimentalSuccess(int toolId)
    {
        return new FactoryPacketEquipToolResponse
        {
            Success = true,
            ToolId = toolId,
        };
    }

    /// <summary>
    /// EXPERIMENTAL: Success=0 and echo requested ToolId (unsupported tools).
    /// </summary>
    public static FactoryPacketEquipToolResponse CreateExperimentalFailure(int toolId)
    {
        return new FactoryPacketEquipToolResponse
        {
            Success = false,
            ToolId = toolId,
        };
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        // Proven body: uint8 Success then int32 ToolId. No padding / extra fields.
        writer.Write(Success);
        writer.Write(ToolId);

        return writer.Buffer;
    }
}
