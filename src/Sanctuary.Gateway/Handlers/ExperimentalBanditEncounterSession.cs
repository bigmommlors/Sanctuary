using System.Collections.Concurrent;
using System.Numerics;

namespace Sanctuary.Gateway.Handlers;

// Local correlation store for the controlled Activity 29 offer experiments.
// Not a retail encounter-instance registry.
internal static class ExperimentalBanditEncounterSession
{
    private static readonly ConcurrentDictionary<GatewayConnection, PendingOffer> PendingByConnection = new();
    private static readonly ConcurrentDictionary<GatewayConnection, PendingOffer> AwaitingState6AfterZoning = new();
    private static readonly ConcurrentDictionary<GatewayConnection, ActiveBanditSession> ActiveByConnection = new();
    private static readonly ConcurrentDictionary<GatewayConnection, AwaitingReturn> AwaitingReturnFinishedLoading = new();

    /// <summary>Last Bandit instance Zone.Id from GetOrCreate — used to log reuse vs new on re-entry.</summary>
    public static int? LastBanditInstanceZoneId { get; private set; }

    public readonly record struct PendingOffer(int HeaderValue1, int HeaderValue2, int ActivityId);

    public readonly record struct ActiveBanditSession(
        int HeaderValue1,
        int HeaderValue2,
        int ActivityId,
        string ReturnZoneName,
        int ReturnZoneId,
        Vector4 ReturnPosition,
        Quaternion ReturnRotation);

    public readonly record struct AwaitingReturn(ActiveBanditSession Session, string LogPrefix);

    public readonly record struct SessionSnapshot(
        bool HasPending,
        PendingOffer Pending,
        bool HasAwaitingState6,
        PendingOffer AwaitingState6,
        bool HasActive,
        ActiveBanditSession Active,
        bool HasAwaitingReturn,
        AwaitingReturn AwaitingReturn,
        int? LastBanditInstanceZoneId);

    public static void Register(GatewayConnection connection, int headerValue1, int headerValue2, int activityId)
    {
        PendingByConnection[connection] = new PendingOffer(headerValue1, headerValue2, activityId);
    }

    public static bool TryTake(GatewayConnection connection, out PendingOffer pending)
    {
        return PendingByConnection.TryRemove(connection, out pending);
    }

    public static bool TryPeek(GatewayConnection connection, out PendingOffer pending)
    {
        return PendingByConnection.TryGetValue(connection, out pending);
    }

    public static void RegisterAwaitingState6AfterZoning(GatewayConnection connection, PendingOffer pending)
    {
        AwaitingState6AfterZoning[connection] = pending;
    }

    public static bool TryPeekAwaitingState6AfterZoning(GatewayConnection connection, out PendingOffer pending)
    {
        return AwaitingState6AfterZoning.TryGetValue(connection, out pending);
    }

    public static bool TryTakeAwaitingState6AfterZoning(GatewayConnection connection, out PendingOffer pending)
    {
        return AwaitingState6AfterZoning.TryRemove(connection, out pending);
    }

    public static void RegisterActive(GatewayConnection connection, ActiveBanditSession session)
    {
        ActiveByConnection[connection] = session;
    }

    public static bool TryPeekActive(GatewayConnection connection, out ActiveBanditSession session)
    {
        return ActiveByConnection.TryGetValue(connection, out session);
    }

    public static bool TryTakeActive(GatewayConnection connection, out ActiveBanditSession session)
    {
        return ActiveByConnection.TryRemove(connection, out session);
    }

    public static void RegisterAwaitingReturnFinishedLoading(
        GatewayConnection connection,
        ActiveBanditSession session,
        string logPrefix)
    {
        AwaitingReturnFinishedLoading[connection] = new AwaitingReturn(session, logPrefix);
    }

    public static bool TryPeekAwaitingReturnFinishedLoading(GatewayConnection connection, out AwaitingReturn awaiting)
    {
        return AwaitingReturnFinishedLoading.TryGetValue(connection, out awaiting);
    }

    public static bool TryTakeAwaitingReturnFinishedLoading(GatewayConnection connection, out AwaitingReturn awaiting)
    {
        return AwaitingReturnFinishedLoading.TryRemove(connection, out awaiting);
    }

    public static void RememberBanditInstanceZoneId(int zoneId)
    {
        LastBanditInstanceZoneId = zoneId;
    }

    public static SessionSnapshot Snapshot(GatewayConnection connection)
    {
        var hasPending = PendingByConnection.TryGetValue(connection, out var pending);
        var hasAwaitingState6 = AwaitingState6AfterZoning.TryGetValue(connection, out var awaitingState6);
        var hasActive = ActiveByConnection.TryGetValue(connection, out var active);
        var hasAwaitingReturn = AwaitingReturnFinishedLoading.TryGetValue(connection, out var awaitingReturn);

        return new SessionSnapshot(
            hasPending, pending,
            hasAwaitingState6, awaitingState6,
            hasActive, active,
            hasAwaitingReturn, awaitingReturn,
            LastBanditInstanceZoneId);
    }

    /// <summary>
    /// Clears Bandit-only transient maps for this connection after overworld return is confirmed.
    /// Does not touch Player.StartingZonePosition/Rotation or dispose instance zones.
    /// </summary>
    public static void ClearBanditTransientAfterReturn(GatewayConnection connection)
    {
        PendingByConnection.TryRemove(connection, out _);
        AwaitingState6AfterZoning.TryRemove(connection, out _);
        ActiveByConnection.TryRemove(connection, out _);
        AwaitingReturnFinishedLoading.TryRemove(connection, out _);
    }

    public static void ClearAll(GatewayConnection connection)
    {
        ClearBanditTransientAfterReturn(connection);
    }
}
