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

    bool TryGetPlayer(ulong guid, [MaybeNullWhen(false)] out Player player);
    bool TryGetPlayer(string name, [MaybeNullWhen(false)] out Player player);
}