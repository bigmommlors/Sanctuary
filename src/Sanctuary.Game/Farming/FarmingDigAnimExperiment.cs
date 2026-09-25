using Sanctuary.Packet;

namespace Sanctuary.Game.Farming;

/// <summary>
/// EXPERIMENTAL / debug-only: play original Free Realms farm_dig on the player's character.
/// Uses existing PlayerUpdatePacketSetAnimation (op 8). Not retail Factory rock/tool protocol.
/// Does not attach shovel mesh, clear obstacles, or write farming DB.
/// </summary>
public static class FarmingDigAnimExperiment
{
    /// <summary>
    /// AnimationTypes.xml Farm branch slot farm_dig (PROVEN static catalog id).
    /// </summary>
    public const int FarmDigAnimationId = 3900003;

    /// <summary>
    /// Flags bit 0 clear = play animation now (not set as base/idle).
    /// Documented on PlayerUpdatePacketSetAnimation; existing call sites use Flags=1 only for idle restore.
    /// </summary>
    public const byte PlayNowFlags = 0;

    public const string NotInWildsFarmMessage =
        "Debug diganim only works in private Wilds Farm (!farmtest enter).";

    public const string SuccessMessage =
        "EXPERIMENTAL: sent farm_dig animation (3900003) via PlayerUpdatePacketSetAnimation.";

    /// <summary>
    /// Build the verified SetAnimation packet for play-now farm_dig on the given player Guid.
    /// Unknown left at 0 (same default as Boombox/SillyString idle restores).
    /// </summary>
    public static PlayerUpdatePacketSetAnimation CreatePlayNowPacket(ulong playerGuid) =>
        new()
        {
            Guid = playerGuid,
            AnimationId = FarmDigAnimationId,
            Unknown = 0,
            Flags = PlayNowFlags
        };
}
