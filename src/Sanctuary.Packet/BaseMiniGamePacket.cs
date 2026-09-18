using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Family 39 minigame lifecycle header: [i16 39][u8 sub][i32 StateId][i32 GroupId][i32 GameId].
/// Wire layout matches fork/client RE (sub-opcode is a byte, unlike encounter family 41).
/// </summary>
public class BaseMiniGamePacket
{
    public const short OpCode = 39;

    private readonly byte _subOpCode;

    public int StateId { get; }
    public int GroupId { get; }
    public int GameId { get; }

    public BaseMiniGamePacket(byte subOpCode, int stateId, int groupId, int gameId)
    {
        _subOpCode = subOpCode;
        StateId = stateId;
        GroupId = groupId;
        GameId = gameId;
    }

    protected void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(_subOpCode);
        writer.Write(StateId);
        writer.Write(GroupId);
        writer.Write(GameId);
    }
}
