using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game;

public class ZoneManager : IZoneManager
{
    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;
    private readonly IServiceProvider _serviceProvider;

    private static int _uniqueId = 1;

    private readonly ConcurrentDictionary<int, IZone> _zones = new();

    private const int StartingZoneDefinitionId = 1;
    public StartingZone StartingZone { get; private set; } = null!;

    public ZoneManager(
        ILoggerFactory loggerFactory,
        IResourceManager resourceManager,
        IServiceProvider serviceProvider)
    {
        _logger = loggerFactory.CreateLogger<ZoneManager>();

        _resourceManager = resourceManager;
        _serviceProvider = serviceProvider;
    }

    public bool Load()
    {
        if (!TryCreateStartingZone(StartingZoneDefinitionId, out var startingZone))
            return false;

        StartingZone = startingZone;

        return true;
    }

    public bool TryGetPlayer(ulong guid, [MaybeNullWhen(false)] out Player player)
    {
        player = default;

        foreach (var zone in _zones)
        {
            if (zone.Value.TryGetPlayer(guid, out player))
                return true;
        }

        return false;
    }

    public bool TryGetPlayer(string name, [MaybeNullWhen(false)] out Player player)
    {
        player = default;

        foreach (var zone in _zones)
        {
            foreach (var zonePlayer in zone.Value.Players)
            {
                if (zonePlayer.Name.FullName == name)
                {
                    player = zonePlayer;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryCreateStartingZone(int definitionId, [MaybeNullWhen(false)] out StartingZone zone)
    {
        zone = default;

        if (!_resourceManager.Zones.TryGetValue(definitionId, out var zoneDefinition))
            return false;

        if (zoneDefinition is not StartingZoneDefinition startingZoneDefinition)
            return false;

        zone = new StartingZone(startingZoneDefinition, _serviceProvider)
        {
            Id = _uniqueId++
        };

        zone.OnStart();

        return _zones.TryAdd(zone.Id, zone);
    }

    public bool TryGetOrCreateInstanceZone(int definitionId, [MaybeNullWhen(false)] out IZone zone)
    {
        zone = default;

        foreach (var existing in _zones.Values)
        {
            if (existing.DefinitionId == definitionId && existing is InstanceZone)
            {
                zone = existing;
                return true;
            }
        }

        if (!_resourceManager.Zones.TryGetValue(definitionId, out var zoneDefinition))
        {
            _logger.LogError("No zone definition Id={DefinitionId} in Resources/Zones.", definitionId);
            return false;
        }

        if (zoneDefinition is not InstanceZoneDefinition instanceZoneDefinition)
        {
            _logger.LogError(
                "Zone definition Id={DefinitionId} Name={Name} is not an Instance zone ($type Instance).",
                definitionId, zoneDefinition.Name);
            return false;
        }

        var created = new InstanceZone(instanceZoneDefinition, _serviceProvider)
        {
            Id = _uniqueId++
        };

        created.OnStart();

        if (!_zones.TryAdd(created.Id, created))
        {
            _logger.LogError("Failed to register instance zone Id={ZoneId} DefinitionId={DefinitionId}.", created.Id, definitionId);
            return false;
        }

        _logger.LogInformation(
            "Created instance zone: ZoneId={ZoneId} DefinitionId={DefinitionId} Name={Name} Sky={Sky} Spawn={Spawn}",
            created.Id, definitionId, created.Name, created.Sky, created.SpawnPosition);

        zone = created;
        return true;
    }
}