using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEREA.App.Services;
using NEREA.Audio;
using NEREA.Core.Data;
using NEREA.Core.Models;
using NEREA.Core.Services;

namespace NEREA.App.ViewModels;

/// <summary>
/// The immersive shell view model: playback state, now-playing metadata,
/// overlay visibility, idle-driven fade, world announcements.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly PlaybackController _playback;
    private readonly AquariumService _aquarium;
    private readonly SettingsStore _settings;
    private readonly EngineHost _engine;
    private readonly SystemMediaControlsService _smtc;

    private readonly System.Timers.Timer _uiTimer;
    private DateTime _lastActivity = DateTime.UtcNow;

    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private string _nowTitle = "";
    [ObservableProperty] private string _nowArtist = "";
    [ObservableProperty] private double _positionFraction;
    [ObservableProperty] private string _positionLabel = "";
    [ObservableProperty] private string _durationLabel = "";
    [ObservableProperty] private double _volume = 0.8;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private bool _isShuffle;
    [ObservableProperty] private int _repeatIndex; // 0 off, 1 all, 2 one
    [ObservableProperty] private bool _showLibrary;
    [ObservableProperty] private bool _showAquarium;
    [ObservableProperty] private bool _showSettings;
    [ObservableProperty] private bool _anyPanelOpen;
    [ObservableProperty] private bool _cinematic;
    [ObservableProperty] private bool _ambient;
    [ObservableProperty] private bool _splashVisible = true;
    [ObservableProperty] private bool _degraded;
    [ObservableProperty] private string _degradedReason = "";
    [ObservableProperty] private bool _controlsVisible = true;
    [ObservableProperty] private double _overlayOpacity = 1.0;
    [ObservableProperty] private bool _busyImporting;
    [ObservableProperty] private string _infoMessage = "";
    [ObservableProperty] private bool _infoOpen;
    [ObservableProperty] private bool _infoError;
    [ObservableProperty] private Microsoft.UI.Xaml.Controls.InfoBarSeverity _infoSeverity =
        Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;

    public bool IsIdle => (DateTime.UtcNow - _lastActivity).TotalSeconds > 3.5;

    public PlaybackController Playback => _playback;
    public LibraryViewModel LibraryVm { get; }
    public AquariumViewModel AquariumVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainViewModel(
        PlaybackController playback,
        AquariumService aquarium,
        SettingsStore settings,
        EngineHost engine,
        SystemMediaControlsService smtc,
        LibraryViewModel libraryVm,
        AquariumViewModel aquariumVm,
        SettingsViewModel settingsVm)
    {
        _playback = playback;
        _aquarium = aquarium;
        _settings = settings;
        _engine = engine;
        _smtc = smtc;

        LibraryVm = libraryVm;
        AquariumVm = aquariumVm;
        SettingsVm = settingsVm;

        _playback.TrackChanged += OnTrackChanged;
        _playback.PlayingChanged += OnPlayingChanged;
        _engine.FirstFrame += OnEngineFirstFrame;
        _engine.DegradedChanged += OnEngineDegraded;
        _engine.WorldAnnounced += OnWorldAnnounced;

        var s = _settings.Settings;
        Volume = s.Volume;
        IsMuted = s.Muted;
        IsShuffle = _playback.Shuffle;
        RepeatIndex = (int)_playback.Repeat;

        ApplyVolume();

        _uiTimer = new System.Timers.Timer(250) { SynchronizingObject = null };
        _uiTimer.Elapsed += (_, _) => OnTick();
        _uiTimer.Start();
    }

    // ── Session restore ──────────────────────────────────────────────────────

    public void RestoreSession()
    {
        _aquarium.RestoreSession();
        LibraryVm.RefreshAll();
        AquariumVm.ReloadFromService();
        SettingsVm.LoadFromSettings();

        var s = _settings.Settings;
        if (s.SessionQueue.Count > 0)
        {
            var tracks = s.SessionQueue
                .Select(LibraryVm.FindTrackByPath)
                .Where(t => t is not null)
                .Select(t => t!)
                .ToList();
            if (tracks.Count > 0)
            {
                int index = Math.Clamp(s.SessionQueueIndex, 0, tracks.Count - 1);
                _playback.PlayTracks(tracks, index);
                _playback.Seek(TimeSpan.FromSeconds(s.SessionPositionSeconds));
                if (!s.SessionWasPlaying) _playback.Pause();
            }
        }
        if (s.StartInAmbientMode) EnterAmbient();
    }

    public void PersistSession()
    {
        _settings.Update(s =>
        {
            var queue = _playback.Queue;
            s.SessionQueue = queue.Select(t => t.Path).ToList();
            s.SessionQueueIndex = _playback.QueueIndex;
            s.SessionPositionSeconds = _playback.Position.TotalSeconds;
            s.SessionWasPlaying = _playback.IsPlaying;
            s.Volume = Volume;
            s.Muted = IsMuted;
        });
    }

    // ── UI timer ─────────────────────────────────────────────────────────────

    private double _lastFrac;

    private void OnTick()
    {
        try
        {
            var pos = _playback.Position;
            var dur = _playback.Duration;
            double frac = dur.TotalSeconds > 0 ? Math.Clamp(pos.TotalSeconds / dur.TotalSeconds, 0, 1) : 0;
            if (Math.Abs(frac - _lastFrac) > 0.0005)
            {
                _lastFrac = frac;
                _dispatcherSet(() =>
                {
                    PositionFraction = frac;
                    PositionLabel = FormatTime(pos);
                    DurationLabel = FormatTime(dur);
                });
            }

            // Idle fade: controls dim after 3.5 s of inactivity, return near the bottom.
            double idleSeconds = (DateTime.UtcNow - _lastActivity).TotalSeconds;
            bool panels = ShowLibrary || ShowAquarium || ShowSettings;
            bool shouldShow = panels || idleSeconds < 3.5 || _pointerNearBottom;
            _dispatcherSet(() =>
            {
                ControlsVisible = shouldShow || IsPlaying is false;
                OverlayOpacity = shouldShow ? 1.0 : 0.0;
            });
            _smtc.TickTimeline();
        }
        catch
        {
            // Ticking must never break.
        }
    }

    private readonly System.Threading.SynchronizationContext? _sync = System.Threading.SynchronizationContext.Current;
    private bool _pointerNearBottom;

    private void _dispatcherSet(Action action)
    {
        if (_sync is not null) _sync.Post(_ => action(), null);
        else action();
    }

    public void NotifyActivity(bool nearBottom)
    {
        _lastActivity = DateTime.UtcNow;
        _pointerNearBottom = nearBottom;
    }

    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes}:{t.Seconds:00}";

    // ── Events from services ─────────────────────────────────────────────────

    private void OnTrackChanged(object? sender, Track? track)
    {
        _dispatcherSet(() =>
        {
            NowTitle = track?.Title ?? "";
            NowArtist = track?.DisplayArtist ?? "";
            PositionFraction = 0;
            PositionLabel = "";
            DurationLabel = FormatTime(track?.Duration ?? TimeSpan.Zero);
        });
    }

    private void OnPlayingChanged(object? sender, bool playing)
    {
        _dispatcherSet(() => IsPlaying = playing);
    }

    private void OnEngineFirstFrame(object? sender, EventArgs e)
    {
        _dispatcherSet(() =>
        {
            SplashVisible = false;
            DegradeKeepHidden();
        });
    }

    private void DegradeKeepHidden() { /* reserved */ }

    private void OnEngineDegraded(object? sender, EventArgs e)
    {
        _dispatcherSet(() =>
        {
            SplashVisible = false;
            Degraded = true;
            DegradedReason = "The aquatic engine could not start on this machine. " +
                             "Music playback and the library remain fully available.";
        });
    }

    private void OnWorldAnnounced(object? sender, string worldName)
    {
        _dispatcherSet(() =>
        {
            WorldToast = worldName;
            WorldToastVisible = true;
            // Auto-hide handled by the view with a storyboard timer.
        });
    }

    [ObservableProperty] private string _worldToast = "";
    [ObservableProperty] private bool _worldToastVisible;

    public void DismissWorldToast() => WorldToastVisible = false;

    public void ShowInfo(string message, bool error = false)
    {
        InfoMessage = message;
        InfoError = error;
        InfoSeverity = error
            ? Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error
            : Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;
        InfoOpen = true;
    }

    // ── Transport commands ───────────────────────────────────────────────────

    [RelayCommand]
    public void PlayPause() => _playback.PlayPause();

    [RelayCommand]
    public void Next() => _playback.Next();

    [RelayCommand]
    public void Previous() => _playback.Previous();

    [RelayCommand]
    private void ToggleShuffle()
    {
        IsShuffle = !IsShuffle;
        _playback.Shuffle = IsShuffle;
    }

    [RelayCommand]
    private void CycleRepeat()
    {
        RepeatIndex = (RepeatIndex + 1) % 3;
        _playback.Repeat = (RepeatMode)RepeatIndex;
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        _playback.SetMuted(IsMuted);
        PersistVolume();
    }

    partial void OnVolumeChanged(double value)
    {
        ApplyVolume();
        PersistVolume();
    }

    private void ApplyVolume()
    {
        _playback.SetVolume((float)Volume);
        _playback.SetMuted(IsMuted);
    }

    private void PersistVolume() =>
        _settings.Update(s => { s.Volume = Volume; s.Muted = IsMuted; });

    public void SeekFraction(double fraction)
    {
        var dur = _playback.Duration;
        if (dur.TotalSeconds <= 0) return;
        _playback.Seek(TimeSpan.FromSeconds(dur.TotalSeconds * Math.Clamp(fraction, 0, 1)));
    }

    public void SeekRelative(double seconds)
    {
        var pos = _playback.Position + TimeSpan.FromSeconds(seconds);
        if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
        if (pos > _playback.Duration) pos = _playback.Duration - TimeSpan.FromSeconds(1);
        _playback.Seek(pos);
    }

    public void SetVolumeRelative(double delta) => Volume = Math.Clamp(Volume + delta, 0, 1);

    // ── Panels & modes ───────────────────────────────────────────────────────

    private void CloseAllPanels()
    {
        ShowLibrary = false;
        ShowAquarium = false;
        ShowSettings = false;
        AnyPanelOpen = false;
    }

    private void UpdateAnyPanel() => AnyPanelOpen = ShowLibrary || ShowAquarium || ShowSettings;

    partial void OnShowLibraryChanged(bool value) { UpdateAnyPanel(); if (value) LibraryVm.RefreshAll(); }
    partial void OnShowAquariumChanged(bool value) { UpdateAnyPanel(); if (value) AquariumVm.ReloadFromService(); }
    partial void OnShowSettingsChanged(bool value) { UpdateAnyPanel(); if (value) SettingsVm.LoadFromSettings(); }

    [RelayCommand]
    private void OpenLibrary() { CloseAllPanels(); ShowLibrary = true; }

    [RelayCommand]
    private void OpenAquarium() { CloseAllPanels(); ShowAquarium = true; }

    [RelayCommand]
    private void OpenSettings() { CloseAllPanels(); ShowSettings = true; }

    [RelayCommand]
    private void ClosePanels() { CloseAllPanels(); }

    public void ClosePanelsPublic() => CloseAllPanels();

    [RelayCommand]
    private void ToggleCinematic() => Cinematic = !Cinematic;

    [RelayCommand]
    private void EnterAmbient()
    {
        Ambient = true;
        CloseAllPanels();
        _engine.SetAmbient(true);
        _settings.Update(s => s.StartInAmbientMode = true);
    }

    [RelayCommand]
    private void ExitAmbient()
    {
        Ambient = false;
        _engine.SetAmbient(false);
        _settings.Update(s => s.StartInAmbientMode = false);
    }

    [RelayCommand]
    private void ExitCinematic() => Cinematic = false;

    public void DismissSplash() => SplashVisible = false;
}
