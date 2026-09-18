using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// S2C family 39 / sub 19 — server-driven minigame-state removal.
/// Client RE (and live Bandit return logs): without this, Minigame Type stays stuck after leave
/// and Activity Join never fires again. Defaults (StateId=0, GroupId=-1, GameId=-1) target the
/// first MiniGameState for full local teardown.
/// </summary>
public class MiniGameStateRemovePacket : BaseMiniGamePacket, ISerializablePacket
{
    public new const byte OpCode = 19;

    public MiniGameStateRemovePacket(int stateId = 0, int groupId = -1, int gameId = -1)
        : base(OpCode, stateId, groupId, gameId)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        return writer.Buffer;
    }
}
