using System;

namespace Sanctuary.Database.Entities;

/// <summary>
/// Per-character cleared-obstacle state for the private Wilds farm prototype.
/// Absence of a row means uncleared; a row means that obstacle was cleared.
/// </summary>
public class DbFarmObstacle
{
    public int Id { get; set; }

    /// <summary>
    /// Farm identity (e.g. wilds-private-farm-1). Unique with CharacterId + ObstacleKey.
    /// </summary>
    public required string FarmKey { get; set; }

    /// <summary>
    /// Stable obstacle id (e.g. wilds-weed-test-1). Unique with CharacterId + FarmKey.
    /// </summary>
    public required string ObstacleKey { get; set; }

    /// <summary>UTC time this obstacle was cleared.</summary>
    public DateTimeOffset ClearedAtUtc { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;
}
