using System.Diagnostics.CodeAnalysis;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game;

public interface IZoneManager
{
    StartingZone StartingZone { get; }

    bool Load();

    /// <summary>
    /// Gets an existing instance zone for <paramref name="definitionId"/>, or creates one from Resources/Zones.
    /// </summary>
    bool TryGetOrCreateInstanceZone(int definitionId, [MaybeNullWhen(false)] out IZone zone);

    /// <summary>
    /// Always creates a new instance zone from Resources/Zones (private / per-enter instances).
    /// Does not reuse an existing instance of the same definition id.
    /// </summary>
    bool TryCreateInstanceZone(int definitionId, [MaybeNullWhen(false)] out IZone zone);

    /// <summary>
    /// Removes and disposes an empty instance zone previously created via the instance APIs.
    /// </summary>
    bool TryRemoveInstanceZone(int zoneId);

    bool TryGetPlayer(ulong guid, [MaybeNullWhen(false)] out Player player);
    bool TryGetPlayer(string name, [MaybeNullWhen(false)] out Player player);
}