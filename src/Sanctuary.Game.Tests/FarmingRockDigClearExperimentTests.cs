using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Farming;
using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

/// <summary>
/// EXPERIMENTAL delayed shovel dig → rock clear policy (not retail Factory protocol).
/// </summary>
[TestClass]
public sealed class FarmingRockDigClearExperimentTests
{
    [TestMethod]
    public void RockWithoutShovel_RejectsBegin_NoAnimateClearPersist()
    {
        Assert.IsFalse(FarmingRockDigClearExperiment.TryBegin(
            selectedFarmToolId: null,
            alreadyPending: false,
            out var rejectMessage));

        Assert.AreEqual(FarmingToolSelection.RockRequiresShovelMessage, rejectMessage);
        Assert.IsFalse(FarmingToolSelection.CanClearRock(null));
    }

    [TestMethod]
    public void RockWithShovel_AllowsBegin()
    {
        Assert.IsTrue(FarmingRockDigClearExperiment.TryBegin(
            FarmingToolSelection.ShovelToolId,
            alreadyPending: false,
            out var rejectMessage));

        Assert.IsNull(rejectMessage);
    }

    [TestMethod]
    public void AnimationPacket_OnlyUsesVerifiedFarmDigPath()
    {
        // Valid interactions reuse FarmingDigAnimExperiment.CreatePlayNowPacket (same as !farmtest diganim).
        const ulong guid = 0xAABBCCDDEEFF0011UL;
        var packet = FarmingDigAnimExperiment.CreatePlayNowPacket(guid);

        Assert.AreEqual(guid, packet.Guid);
        Assert.AreEqual(FarmingDigAnimExperiment.FarmDigAnimationId, packet.AnimationId);
        Assert.AreEqual(3900003, packet.AnimationId);
        Assert.AreEqual(0, packet.Unknown);
        Assert.AreEqual(FarmingDigAnimExperiment.PlayNowFlags, packet.Flags);

        var bytes = packet.Serialize();
        Assert.AreEqual(21, bytes.Length);
        Assert.AreEqual(PlayerUpdatePacketSetAnimation.OpCode, BitConverter.ToInt16(bytes, 2));
        Assert.AreEqual(3900003, BitConverter.ToInt32(bytes, 12));
    }

    [TestMethod]
    public void ExperimentalDelay_IsDueOnlyAfterOnePointFiveSeconds()
    {
        Assert.AreEqual(1500, FarmingRockDigClearExperiment.ExperimentalClearDelayMs);

        var started = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        var due = FarmingRockDigClearExperiment.ComputeDueAtUtc(started);

        Assert.AreEqual(started.AddMilliseconds(1500), due);
        Assert.IsFalse(FarmingRockDigClearExperiment.IsDue(started, due));
        Assert.IsFalse(FarmingRockDigClearExperiment.IsDue(started.AddMilliseconds(1499), due));
        Assert.IsTrue(FarmingRockDigClearExperiment.IsDue(due, due));
        Assert.IsTrue(FarmingRockDigClearExperiment.IsDue(started.AddMilliseconds(1500), due));
        Assert.IsTrue(FarmingRockDigClearExperiment.IsDue(started.AddSeconds(2), due));
    }

    [TestMethod]
    public void DuplicatePending_RejectsSecondBegin()
    {
        Assert.IsFalse(FarmingRockDigClearExperiment.TryBegin(
            FarmingToolSelection.ShovelToolId,
            alreadyPending: true,
            out var rejectMessage));

        Assert.AreEqual(FarmingRockDigClearExperiment.AlreadyDiggingMessage, rejectMessage);
    }

    [TestMethod]
    public void PersistenceGate_RequiresMatchingRockGuid()
    {
        const ulong pendingGuid = 100_000_000_001UL;

        Assert.IsTrue(FarmingRockDigClearExperiment.MatchesTrackedRock(pendingGuid, pendingGuid));
        Assert.IsFalse(FarmingRockDigClearExperiment.MatchesTrackedRock(pendingGuid, pendingGuid + 1));
        Assert.IsFalse(FarmingRockDigClearExperiment.MatchesTrackedRock(pendingGuid, null));
    }

    [TestMethod]
    public void SafeExitOrReset_MismatchedOrMissingRockBlocksCommit()
    {
        // After farm leave / rock remove / reset cancel: pending Guid no longer matches tracked rock.
        const ulong pendingGuid = 100_000_000_042UL;

        Assert.IsFalse(FarmingRockDigClearExperiment.MatchesTrackedRock(pendingGuid, null));
        Assert.IsFalse(FarmingRockDigClearExperiment.MatchesTrackedRock(pendingGuid, 100_000_000_099UL));
    }

    [TestMethod]
    public void ShovelGating_StillRequiredForBeginAndImpliesPersistenceOnlyAfterDue()
    {
        // Contract: without shovel never begin; with shovel begin does not imply immediate due.
        Assert.IsFalse(FarmingRockDigClearExperiment.TryBegin(null, false, out _));
        Assert.IsTrue(FarmingRockDigClearExperiment.TryBegin(FarmingToolSelection.ShovelToolId, false, out _));

        var started = DateTimeOffset.UtcNow;
        var due = FarmingRockDigClearExperiment.ComputeDueAtUtc(started);
        Assert.IsFalse(FarmingRockDigClearExperiment.IsDue(started, due));
    }
}
