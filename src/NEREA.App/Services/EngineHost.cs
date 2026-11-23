using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using NEREA.Core.Content;
using NEREA.Core.Data;
using NEREA.Core.Models;
using NEREA.Core.Services;
using NEREA.Engine;
using NEREA.Engine.Interop;

namespace NEREA.App.Services;

/// <summary>
/// Bridges the WinUI layer and the realtime engine. The UI thread never
/// touches D3D12; everything flows through the engine facade.
/// </summary>
public sealed class EngineHost : IDisposable
{
    private readonly NereaEngine _engine = new();
    private readonly AquariumService _aquarium;
    private readonly SettingsStore _settings;
    private readonly IMusicPlayer _player;

    private DispatcherQueue? _dispatcher;
    private ISwapChainPanelNative? _panelNative;

    public bool Degraded { get; private set; }
    public string DegradedReason { get; private set; } = "";
    public bool FirstFrameArrived { get; private set; }

    public event EventHandler? FirstFrame;
    public event EventHandler? DegradedChanged;
    public event EventHandler<string>? WorldAnnounced;

    public EngineHost(AquariumService aquarium, SettingsStore settings, IMusicPlayer player)
    {
        _aquarium = aquarium;
        _settings = settings;
        _player = player;
    }

    public NereaEngine Engine => _engine;

    /// <summary>Attaches the engine to a SwapChainPanel. UI thread only.</summary>
    public void Attach(SwapChainPanel panel)
    {
        try
        {
            _panelNative = panel.As<ISwapChainPanelNative>();
        }
        catch (Exception ex)
        {
            SetDegraded($"SwapChainPanel interop unavailable: {ex.Message}");
            return;
        }

        _dispatcher = DispatcherQueue.GetForCurrentThread();

        var s = _settings.Settings;
        var worldId = _aquarium.ActiveWorld.Id;
        var species = _aquarium.ActiveSpecies.Select(x => new SpeciesCount
        {
            SpeciesId = x.SpeciesId, Count = x.Count, Enabled = x.Enabled
        }).ToList();

        var config = new EngineConfig
        {
            WorldId = worldId,
            Species = species,
            Quality = QualityPresets.For(s.Quality),
            ReducedMotion = s.ReducedMotion,
            Reflections = s.Reflections,
            Shadows = s.Shadows,
            AdaptiveQuality = s.AdaptiveQuality,
            FishPopulationScale = s.FishPopulation,
            ParticleDensity = s.ParticleDensity,
            MouseSensitivity = s.MouseSensitivity,
            RippleStrength = s.RippleStrength,
            FishReaction = s.FishReaction,
            FpsLimit = FpsToValue(s.FpsLimit),
            AtmosphereScale = AtmosphereToScale(_aquarium.Atmosphere),
            EnergyProvider = () => _player.Energy
        };

        _engine.FirstFramePresented += (_, __) =>
        {
            FirstFrameArrived = true;
            _dispatcher?.TryEnqueue(() => FirstFrame?.Invoke(this, EventArgs.Empty));
        };
        _engine.Degraded += (_, reason) => _dispatcher?.TryEnqueue(() => SetDegraded(reason));
        _engine.WorldAnnounced += (_, name) => _dispatcher?.TryEnqueue(() => WorldAnnounced?.Invoke(this, name));
        _aquarium.ConfigurationChanged += OnAquariumConfigurationChanged;
        _engine.Start(_panelNative!, action => _dispatcher?.TryEnqueue(() => action()),
            config, (int)panel.ActualWidth, (int)panel.ActualHeight);
    }

    private void OnAquariumConfigurationChanged(object? sender, EventArgs e)
    {
        // Same-world species/atmosphere edits arrive here; SetWorld rebuilds the
        // population and only announces a toast when the world actually changed.
        _engine.SetWorld(_aquarium.ActiveWorld.Id, _aquarium.ActiveSpecies.Select(x =>
            new SpeciesCount { SpeciesId = x.SpeciesId, Count = x.Count, Enabled = x.Enabled }).ToList());
        _engine.SetAtmosphere(AtmosphereToScale(_aquarium.Atmosphere));
    }

    public void Resize(double width, double height) => _engine.Resize((int)width, (int)height);

    public void SetPaused(bool paused) => _engine.SetPaused(paused);
    public void SetAmbient(bool ambient) => _engine.SetAmbientMode(ambient);

    public void ApplyWorld(string worldId, List<SpeciesCount> species) =>
        _engine.SetWorld(worldId, species.Select(x => new SpeciesCount
        {
            SpeciesId = x.SpeciesId, Count = x.Count, Enabled = x.Enabled
        }).ToList());

    public void ApplySpecies(List<SpeciesCount> species) =>
        _engine.SetSpecies(species);

    public void ApplySettings(AppSettings s)
    {
        _engine.UpdateQuality(QualityPresets.For(s.Quality), s.Reflections, s.Shadows,
            s.AdaptiveQuality, s.FishPopulation, s.ParticleDensity, FpsToValue(s.FpsLimit));
        _engine.UpdateInteraction(s.MouseSensitivity, s.RippleStrength, s.FishReaction);
        _engine.SetReducedMotion(s.ReducedMotion);
        _engine.SetAtmosphere(AtmosphereToScale(_aquarium.Atmosphere));
    }

    public static int FpsToValue(FpsLimit limit) => limit switch
    {
        FpsLimit.Fps30 => 30,
        FpsLimit.Fps60 => 60,
        FpsLimit.Fps120 => 120,
        _ => 0
    };

    public static float AtmosphereToScale(Atmosphere atmosphere) => atmosphere switch
    {
        Atmosphere.Calm => 0.6f,
        Atmosphere.Vivid => 1.5f,
        _ => 1f
    };

    private void SetDegraded(string reason)
    {
        Degraded = true;
        DegradedReason = reason;
        DegradedChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _engine.Dispose();
}
