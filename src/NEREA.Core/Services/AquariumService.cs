using NEREA.Core.Content;
using NEREA.Core.Data;
using NEREA.Core.Models;

namespace NEREA.Core.Services;

/// <summary>
/// Manages saved custom aquariums and the currently active ecosystem
/// configuration (world + species counts + atmosphere).
/// </summary>
public sealed class AquariumService
{
    private readonly AquariumStore _store;
    private readonly SettingsStore _settings;

    public AquariumService(AquariumStore store, SettingsStore settings)
    {
        _store = store;
        _settings = settings;
    }

    public IReadOnlyList<AquariumPreset> Saved => _store.Presets;

    public event EventHandler? PresetsChanged;

    public WorldDefinition ActiveWorld { get; private set; } = WorldCatalog.Default;

    public List<SpeciesCount> ActiveSpecies { get; private set; } = new();

    public Atmosphere Atmosphere { get; private set; } = Atmosphere.Balanced;

    /// <summary>Raised whenever the ecosystem configuration changed (world, population, atmosphere).</summary>
    public event EventHandler? ConfigurationChanged;

    public static List<SpeciesCount> DefaultSpeciesFor(WorldDefinition world) =>
        world.DefaultSpecies.Select(s => new SpeciesCount { SpeciesId = s.SpeciesId, Count = s.Count }).ToList();

    /// <summary>Loads the last session's configuration from settings.</summary>
    public void RestoreSession()
    {
        var s = _settings.Settings;
        if (s.LaunchBehavior == LaunchBehavior.Default)
        {
            LoadWorld(WorldCatalog.Default.Id, WorldCatalog.Default.DefaultSpecies
                .Select(x => new SpeciesCount { SpeciesId = x.SpeciesId, Count = x.Count }).ToList());
            return;
        }
        var world = WorldCatalog.ById(s.LastWorldId) ?? WorldCatalog.Default;
        var species = s.LastSpecies.Count > 0
            ? s.LastSpecies.Select(x => new SpeciesCount { SpeciesId = x.SpeciesId, Count = x.Count, Enabled = x.Enabled }).ToList()
            : DefaultSpeciesFor(world);
        LoadWorld(world.Id, species);
    }

    /// <summary>Applies a world (keeping or resetting the population).</summary>
    public void LoadWorld(string worldId, List<SpeciesCount>? species = null)
    {
        var world = WorldCatalog.ById(worldId);
        if (world is null) return;
        ActiveWorld = world;
        ActiveSpecies = species ?? DefaultSpeciesFor(world);
        PersistSession();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetSpecies(List<SpeciesCount> species)
    {
        ActiveSpecies = species;
        PersistSession();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetAtmosphere(Atmosphere atmosphere)
    {
        Atmosphere = atmosphere;
        PersistSession();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyPreset(AquariumPreset preset)
    {
        var world = WorldCatalog.ById(preset.WorldId);
        if (world is null) return;
        ActiveWorld = world;
        ActiveSpecies = preset.Species.Select(s => new SpeciesCount
        {
            SpeciesId = s.SpeciesId,
            Count = s.Count,
            Enabled = s.Enabled
        }).ToList();
        Atmosphere = preset.Atmosphere;
        _settings.Update(s => s.LastPresetId = preset.Id);
        PersistSession();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SavePreset(AquariumPreset preset)
    {
        _store.Save(preset);
        PresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public AquariumPreset SnapshotAsPreset(string name)
    {
        return new AquariumPreset
        {
            Name = name,
            WorldId = ActiveWorld.Id,
            Species = ActiveSpecies.Select(s => new SpeciesCount
            {
                SpeciesId = s.SpeciesId, Count = s.Count, Enabled = s.Enabled
            }).ToList(),
            Atmosphere = Atmosphere
        };
    }

    public void DeletePreset(string id)
    {
        _store.Delete(id);
        PresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PersistSession()
    {
        _settings.Update(s =>
        {
            s.LastWorldId = ActiveWorld.Id;
            s.LastSpecies = ActiveSpecies.Select(x => new SpeciesCount
            {
                SpeciesId = x.SpeciesId, Count = x.Count, Enabled = x.Enabled
            }).ToList();
        });
    }
}
