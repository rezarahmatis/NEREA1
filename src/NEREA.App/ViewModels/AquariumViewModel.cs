using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEREA.App.Services;
using NEREA.Core.Content;
using NEREA.Core.Data;
using NEREA.Core.Models;
using NEREA.Core.Services;
using System.Collections.ObjectModel;

namespace NEREA.App.ViewModels;

/// <summary>One row in the aquarium population editor.</summary>
public sealed class SpeciesRow : ObservableObject
{
    public string SpeciesId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";

    private int _count;
    private bool _enabled = true;

    public int Count { get => _count; set { if (SetProperty(ref _count, value)) OnPropertyChanged(nameof(CountLabel)); } }
    public bool Enabled { get => _enabled; set => SetProperty(ref _enabled, value); }

    public string CountLabel => Enabled ? $"{Count} in the world" : "paused";
}

/// <summary>
/// The aquarium configuration view model: worlds, multi-species populations,
/// saved custom aquariums and atmosphere.
/// </summary>
public partial class AquariumViewModel : ObservableObject
{
    private readonly AquariumService _aquarium;
    private readonly EngineHost _engine;
    private MainViewModel? _main;

    [ObservableProperty] private ObservableCollection<WorldDefinition> _worlds = new(WorldCatalog.All);
    [ObservableProperty] private WorldDefinition? _selectedWorld;
    [ObservableProperty] private ObservableCollection<SpeciesRow> _species = new();
    [ObservableProperty] private ObservableCollection<AquariumPreset> _presets = new();
    [ObservableProperty] private int _atmosphereIndex; // 0 calm, 1 balanced, 2 vivid
    [ObservableProperty] private string _worldCategoryFilter = "All";

    public IReadOnlyList<string> Categories { get; } = new[]
        { "All", "Freshwater", "Ocean", "Exotic" };

    public IReadOnlyList<string> Atmospheres { get; } = new[] { "Calm", "Balanced", "Vivid" };

    public AquariumViewModel(AquariumService aquarium, EngineHost engine)
    {
        _aquarium = aquarium;
        _engine = engine;
        _aquarium.ConfigurationChanged += (_, _) => _main?.SyncUi(ReloadFromService);
        ReloadFromService();
    }

    /// <summary>
    /// Attached by MainViewModel after construction; taking MainViewModel in
    /// the constructor created a circular dependency that crashed the app at
    /// startup.
    /// </summary>
    public void AttachMain(MainViewModel main) => _main = main;

    public void ReloadFromService()
    {
        SelectedWorld = _aquarium.ActiveWorld;
        Species = new ObservableCollection<SpeciesRow>(_aquarium.ActiveSpecies
            .Select(s =>
            {
                var def = SpeciesCatalog.ById(s.SpeciesId);
                return new SpeciesRow
                {
                    SpeciesId = s.SpeciesId,
                    Name = def?.Name ?? s.SpeciesId,
                    Category = def?.Category ?? "",
                    Count = s.Count,
                    Enabled = s.Enabled
                };
            })
            .OrderBy(r => r.Category).ThenBy(r => r.Name));
        Presets = new ObservableCollection<AquariumPreset>(_aquarium.Saved);
        AtmosphereIndex = (int)_aquarium.Atmosphere;
        OnPropertyChanged(nameof(FilteredWorlds));
    }

    public IEnumerable<WorldDefinition> FilteredWorlds =>
        WorldCategoryFilter == "All"
            ? Worlds
            : Worlds.Where(w => w.Category == WorldCategoryFilter);

    partial void OnWorldCategoryFilterChanged(string value) => OnPropertyChanged(nameof(FilteredWorlds));

    // ── World commands ───────────────────────────────────────────────────

    [RelayCommand]
    public void SelectWorld(WorldDefinition? world)
    {
        if (world is null) return;
        _aquarium.LoadWorld(world.Id);
    }

    // ── Population commands ──────────────────────────────────────────────────

    private void PushSpecies()
    {
        var list = Species.Select(r => new SpeciesCount
        {
            SpeciesId = r.SpeciesId, Count = r.Count, Enabled = r.Enabled
        }).ToList();
        _aquarium.SetSpecies(list);
    }

    [RelayCommand]
    public void IncrementSpecies(SpeciesRow? row)
    {
        if (row is null) return;
        row.Count = Math.Min(96, row.Count + 1);
        PushSpecies();
    }

    [RelayCommand]
    public void DecrementSpecies(SpeciesRow? row)
    {
        if (row is null) return;
        row.Count = Math.Max(1, row.Count - 1);
        PushSpecies();
    }

    [RelayCommand]
    public void ToggleSpecies(SpeciesRow? row)
    {
        if (row is null) return;
        row.Enabled = !row.Enabled;
        PushSpecies();
    }

    [RelayCommand]
    public void RemoveSpecies(SpeciesRow? row)
    {
        if (row is null) return;
        var item = Species.FirstOrDefault(r => r.SpeciesId == row.SpeciesId);
        if (item is not null) Species.Remove(item);
        PushSpecies();
    }

    public void AddSpecies(string speciesId)
    {
        if (Species.Any(r => r.SpeciesId == speciesId)) return;
        var def = SpeciesCatalog.ById(speciesId);
        if (def is null) return;
        Species.Add(new SpeciesRow
        {
            SpeciesId = speciesId,
            Name = def.Name,
            Category = def.Category,
            Count = 4,
            Enabled = true
        });
        PushSpecies();
    }

    public IEnumerable<SpeciesDefinition> AddableSpecies =>
        SpeciesCatalog.All.Where(s => Species.All(r => r.SpeciesId != s.Id))
            .OrderBy(s => s.Category).ThenBy(s => s.Name);

    // ── Atmosphere ───────────────────────────────────────────────────────────

    [RelayCommand]
    public void SetAtmosphere(int index)
    {
        AtmosphereIndex = index;
        _aquarium.SetAtmosphere((Atmosphere)index);
    }

    // ── Presets ──────────────────────────────────────────────────────────

    public void SavePreset(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        _aquarium.SavePreset(_aquarium.SnapshotAsPreset(name.Trim()));
        Presets = new ObservableCollection<AquariumPreset>(_aquarium.Saved);
        _main?.ShowInfo($"Saved aquarium “{name.Trim()}”.");
    }

    [RelayCommand]
    public void ApplyPreset(AquariumPreset? preset)
    {
        if (preset is null) return;
        _aquarium.ApplyPreset(preset);
    }

    [RelayCommand]
    public void DeletePreset(AquariumPreset? preset)
    {
        if (preset is null) return;
        _aquarium.DeletePreset(preset.Id);
        Presets = new ObservableCollection<AquariumPreset>(_aquarium.Saved);
    }
}

public static class UiSyncExtensions
{
    public static void SyncUi(this MainViewModel main, Action action)
    {
        var context = System.Threading.SynchronizationContext.Current;
        if (context is not null) context.Post(_ => action(), null);
        else action();
    }
}
