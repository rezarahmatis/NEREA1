using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using NEREA.App.Services;
using NEREA.Audio;
using NEREA.Core.Data;
using NEREA.Core.Models;
using NEREA.Core.Services;
using System.Collections.ObjectModel;

namespace NEREA.App.ViewModels;

/// <summary>
/// Settings view model. Every property maps to a real setting and is applied
/// to the engine / audio system immediately, then persisted.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly IMusicPlayer _player;
    private readonly EngineHost _engine;
    private readonly AutostartService _autostart;
    private readonly MainViewModel _main;

    private bool _loading;

    // Audio
    [ObservableProperty] private ObservableCollection<AudioDeviceInfo> _outputDevices = new();
    [ObservableProperty] private AudioDeviceInfo? _selectedOutputDevice;
    [ObservableProperty] private bool _normalizeLoudness;

    // Visual
    public IReadOnlyList<string> QualityOptions { get; } = new[]
        { "Battery", "Performance", "Balanced", "High", "Ultra" };
    [ObservableProperty] private int _qualityIndex = 2;
    public IReadOnlyList<string> FpsOptions { get; } = new[]
        { "Match display", "120 FPS", "60 FPS", "30 FPS" };
    [ObservableProperty] private int _fpsIndex;
    [ObservableProperty] private double _particleDensity = 1.0;
    [ObservableProperty] private double _fishPopulation = 1.0;
    [ObservableProperty] private bool _reflections = true;
    [ObservableProperty] private bool _shadows = true;
    [ObservableProperty] private double _waterQuality = 3; // caustics octaves 1..4

    // Interaction
    [ObservableProperty] private double _mouseSensitivity = 1.0;
    [ObservableProperty] private double _rippleStrength = 1.0;
    [ObservableProperty] private double _fishReaction = 1.0;

    // Performance
    [ObservableProperty] private bool _adaptiveQuality = true;
    [ObservableProperty] private bool _batteryOptimization;
    [ObservableProperty] private bool _pauseWhenMinimized = true;

    // Application
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _startWithWindows;
    public IReadOnlyList<string> ThemeOptions { get; } = new[] { "Dark", "Light" };
    [ObservableProperty] private int _themeIndex;
    [ObservableProperty] private bool _reducedMotion;
    [ObservableProperty] private bool _reduceTransparency;

    public string AppVersion { get; } = "1.0.0";

    public MainViewModel AppMain => _main;

    public SettingsViewModel(SettingsStore store, IMusicPlayer player, EngineHost engine,
        AutostartService autostart, MainViewModel main)
    {
        _store = store;
        _player = player;
        _engine = engine;
        _autostart = autostart;
        _main = main;
        LoadDevices();
    }

    public void LoadFromSettings()
    {
        _loading = true;
        try
        {
            var s = _store.Settings;
            QualityIndex = (int)s.Quality;
            FpsIndex = FpsToIndex(s.FpsLimit);
            ParticleDensity = s.ParticleDensity;
            FishPopulation = s.FishPopulation;
            Reflections = s.Reflections;
            Shadows = s.Shadows;
            WaterQuality = s.WaterQuality;
            MouseSensitivity = s.MouseSensitivity;
            RippleStrength = s.RippleStrength;
            FishReaction = s.FishReaction;
            AdaptiveQuality = s.AdaptiveQuality;
            BatteryOptimization = s.BatteryOptimization;
            PauseWhenMinimized = s.PauseRenderWhenMinimized;
            MinimizeToTray = s.MinimizeToTray;
            StartWithWindows = _autostart.IsEnabled;
            ThemeIndex = (int)s.Theme;
            ReducedMotion = s.ReducedMotion;
            ReduceTransparency = s.ReduceTransparency;
            NormalizeLoudness = s.NormalizeLoudness;
            LoadDevices();
        }
        finally
        {
            _loading = false;
        }
    }

    public void LoadDevices()
    {
        var devices = _player.EnumerateOutputDevices();
        OutputDevices = new ObservableCollection<AudioDeviceInfo>(devices);
        var current = _store.Settings.OutputDeviceId;
        SelectedOutputDevice = devices.FirstOrDefault(d => d.Id == current) ?? devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
    }

    private void PersistAndApply()
    {
        if (_loading) return;
        _store.Update(s =>
        {
            s.Quality = (QualityLevel)QualityIndex;
            s.FpsLimit = IndexToFps(FpsIndex);
            s.ParticleDensity = (float)ParticleDensity;
            s.FishPopulation = (float)FishPopulation;
            s.Reflections = Reflections;
            s.Shadows = Shadows;
            s.WaterQuality = (int)WaterQuality;
            s.MouseSensitivity = (float)MouseSensitivity;
            s.RippleStrength = (float)RippleStrength;
            s.FishReaction = (float)FishReaction;
            s.AdaptiveQuality = AdaptiveQuality;
            s.BatteryOptimization = BatteryOptimization;
            s.PauseRenderWhenMinimized = PauseWhenMinimized;
            s.MinimizeToTray = MinimizeToTray;
            s.Theme = (AppTheme)ThemeIndex;
            s.ReducedMotion = ReducedMotion;
            s.ReduceTransparency = ReduceTransparency;
            s.NormalizeLoudness = NormalizeLoudness;
        });
        _engine.ApplySettings(_store.Settings);
        _player.SetNormalization(NormalizeLoudness);
        _autostart.SetEnabled(StartWithWindows);
    }

    private static FpsLimit IndexToFps(int i) => i switch
    {
        1 => FpsLimit.Fps120,
        2 => FpsLimit.Fps60,
        3 => FpsLimit.Fps30,
        _ => FpsLimit.MatchDisplay
    };

    private static int FpsToIndex(FpsLimit f) => f switch
    {
        FpsLimit.Fps120 => 1,
        FpsLimit.Fps60 => 2,
        FpsLimit.Fps30 => 3,
        _ => 0
    };

    partial void OnQualityIndexChanged(int value) => PersistAndApply();
    partial void OnFpsIndexChanged(int value) => PersistAndApply();
    partial void OnParticleDensityChanged(double value) => PersistAndApply();
    partial void OnFishPopulationChanged(double value) => PersistAndApply();
    partial void OnReflectionsChanged(bool value) => PersistAndApply();
    partial void OnShadowsChanged(bool value) => PersistAndApply();
    partial void OnWaterQualityChanged(double value) => PersistAndApply();
    partial void OnMouseSensitivityChanged(double value) => PersistAndApply();
    partial void OnRippleStrengthChanged(double value) => PersistAndApply();
    partial void OnFishReactionChanged(double value) => PersistAndApply();
    partial void OnAdaptiveQualityChanged(bool value) => PersistAndApply();
    partial void OnBatteryOptimizationChanged(bool value) => PersistAndApply();
    partial void OnPauseWhenMinimizedChanged(bool value) => PersistAndApply();
    partial void OnMinimizeToTrayChanged(bool value) => PersistAndApply();
    partial void OnStartWithWindowsChanged(bool value) => PersistAndApply();
    partial void OnThemeIndexChanged(int value) => PersistAndApply();
    partial void OnReducedMotionChanged(bool value) => PersistAndApply();
    partial void OnReduceTransparencyChanged(bool value) => PersistAndApply();
    partial void OnNormalizeLoudnessChanged(bool value) => PersistAndApply();

    partial void OnSelectedOutputDeviceChanged(AudioDeviceInfo? value)
    {
        if (_loading || value is null) return;
        _store.Update(s => s.OutputDeviceId = value.Id);
        _player.SetOutputDevice(value.Id);
    }

    [RelayCommand]
    public void RescanLibrary()
    {
        _main.BusyImporting = true;
        Task.Run(() =>
        {
            try
            {
                var progress = new Progress<ImportProgress>(p =>
                {
                    _main.ShowInfo($"Rescanning library… {p.Processed}/{p.Total}");
                });
                var library = App.Services.GetRequiredService<MusicLibraryService>();
                library.RescanFolders(progress);
                _main.ShowInfo("Library rescan complete.");
            }
            catch (Exception ex)
            {
                _main.ShowInfo($"Rescan failed: {ex.Message}", true);
            }
            finally
            {
                _main.BusyImporting = false;
            }
        });
    }
}
