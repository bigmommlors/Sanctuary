using System;
using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Wall-of-data keyboard telemetry (family 194 / subopcode 2).
/// Name from PacketReaderExtensions. Layout from captures:
/// Int32 entryCount, then entryCount × 8 raw bytes.
/// Field meanings inside each 8-byte entry are not fully documented; do not invent them.
/// </summary>
public class WallOfDataPlayerKeyboardPacket : WallOfDataBasePacket, IDeserializable<WallOfDataPlayerKeyboardPacket>
{
    public new const byte OpCode = 2;

    public int EntryCount;
    public List<ulong> RawEntries { get; } = [];

    public WallOfDataPlayerKeyboardPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out WallOfDataPlayerKeyboardPacket value)
    {
        value = new WallOfDataPlayerKeyboardPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.EntryCount))
            return false;

        if (value.EntryCount < 0)
            return false;

        for (var i = 0; i < value.EntryCount; i++)
        {
            if (!reader.TryReadExact(8, out var entrySpan))
                return false;

            value.RawEntries.Add(BitConverter.ToUInt64(entrySpan));
        }

        return reader.RemainingLength == 0;
    }

    public override string ToString()
    {
        return $"EntryCount: {EntryCount}, RawEntries: {RawEntries.Count}";
    }
}
