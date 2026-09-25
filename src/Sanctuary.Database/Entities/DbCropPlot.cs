using System;

namespace Sanctuary.Database.Entities;

/// <summary>
/// Per-character farming plot state for the Free Realms farming prototype.
/// </summary>
public class DbCropPlot
{
    public int Id { get; set; }

    /// <summary>
    /// Stable logical plot id (e.g. fabled-prototype-1). Unique with CharacterId.
    /// </summary>
    public required string PlotKey { get; set; }

    /// <summary>
    /// Seed item definition planted in this plot, when growing/harvestable.
    /// </summary>
    public int? SeedDefinitionId { get; set; }

    /// <summary>
    /// UTC plant time. Null means Empty. Growth uses PlantedAtUtc + configured seconds.
    /// </summary>
    public DateTimeOffset? PlantedAtUtc { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;
}
