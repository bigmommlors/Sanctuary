using System;

namespace Sanctuary.Game.Farming;

/// <summary>
/// EXPERIMENTAL delayed rock clear after farm_dig on private Wilds prototype rocks.
/// Not a reconstruction of retail Factory misfortune / rock-clearing protocol.
/// Delay is driven by existing Npc.UpdateEverySecondAction (zone second timer); do not block Gateway.
/// </summary>
public static class FarmingRockDigClearExperiment
{
    /// <summary>
    /// EXPERIMENTAL: brief wait so farm_dig (3900003) can play before persist+despawn.
    /// Zone UpdateEverySecondAction polls due-at; completion may land slightly after this ms.
    /// </summary>
    public const int ExperimentalClearDelayMs = 1500;

    public const string AlreadyDiggingMessage = "Already clearing that rock.";

    public static DateTimeOffset ComputeDueAtUtc(DateTimeOffset startedAtUtc) =>
        startedAtUtc.AddMilliseconds(ExperimentalClearDelayMs);

    public static bool IsDue(DateTimeOffset nowUtc, DateTimeOffset dueAtUtc) =>
        nowUtc >= dueAtUtc;

    /// <summary>
    /// Gate for starting dig+delayed clear. Without Shovel: reject message.
    /// Duplicate pending: reject without animate/clear.
    /// </summary>
    public static bool TryBegin(
        int? selectedFarmToolId,
        bool alreadyPending,
        out string? rejectMessage)
    {
        if (!FarmingToolSelection.CanClearRock(selectedFarmToolId))
        {
            rejectMessage = FarmingToolSelection.RockRequiresShovelMessage;
            return false;
        }

        if (alreadyPending)
        {
            rejectMessage = AlreadyDiggingMessage;
            return false;
        }

        rejectMessage = null;
        return true;
    }

    /// <summary>
    /// Pending clear may commit only against the same rock Guid that started the dig.
    /// </summary>
    public static bool MatchesTrackedRock(ulong pendingRockGuid, ulong? trackedRockGuid) =>
        trackedRockGuid.HasValue && trackedRockGuid.Value == pendingRockGuid;
}
