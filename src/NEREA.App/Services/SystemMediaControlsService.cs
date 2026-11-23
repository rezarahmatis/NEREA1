using System.Runtime.InteropServices;
using NEREA.Core.Models;
using NEREA.Audio;
using Windows.Media;

namespace NEREA.App.Services;

/// <summary>
/// System Media Transport Controls integration (lock screen / media overlay /
/// hardware keys). Uses ISystemMediaTransportControlsInterop::GetForWindow,
/// activated through RoGetActivationFactory - a plain, compilable Win32 path.
/// Fully guarded: if the interop is unavailable the app runs without SMTC.
/// </summary>
public sealed class SystemMediaControlsService : IDisposable
{
    [ComImport]
    [Guid("ddb5f00f-6a72-4abc-9b8e-6a93a96bdd6a")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISystemMediaTransportControlsInterop
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object GetForWindow(IntPtr appWindow, ref Guid riid);
    }

    // IID_ISystemMediaTransportControls (the default interface of the runtime class).
    private static readonly Guid SmtcIid = new("2BF7B1A6-9EEB-4FBE-B1C2-E8C1C9C6B2A8");
    private static readonly Guid InteropIid = new("ddb5f00f-6a72-4abc-9b8e-6a93a96bdd6a");

    private SystemMediaTransportControls? _smtc;
    private readonly PlaybackController _playback;
    private bool _available;

    public SystemMediaControlsService(PlaybackController playback)
    {
        _playback = playback;
        _playback.TrackChanged += OnTrackChanged;
        _playback.PlayingChanged += OnPlayingChanged;
    }

    public bool Available => _available;

    public void Attach(IntPtr hwnd)
    {
        try
        {
            IntPtr hstr = IntPtr.Zero;
            var className = "Windows.Media.SystemMediaTransportControls";
            WindowsCreateString(className, (uint)className.Length, out hstr);
            var iid = InteropIid;
            RoGetActivationFactory(hstr, ref iid, out IntPtr factoryPtr);
            var interop = (ISystemMediaTransportControlsInterop)
                Marshal.GetTypedObjectForIUnknown(factoryPtr, typeof(ISystemMediaTransportControlsInterop));
            Marshal.Release(factoryPtr);

            riidLocal = SmtcIid;
            var unknown = interop.GetForWindow(hwnd, ref riidLocal);
            IntPtr abi = Marshal.GetIUnknownForObject(unknown);
            _smtc = SystemMediaTransportControls.FromAbi(abi);
            Marshal.Release(abi);

            _smtc.IsPlayEnabled = true;
            _smtc.IsPauseEnabled = true;
            _smtc.IsNextEnabled = true;
            _smtc.IsPreviousEnabled = true;
            _smtc.ButtonPressed += OnButtonPressed;
            _available = true;
            UpdateAll();
        }
        catch
        {
            _smtc = null;
            _available = false;
        }
    }

    private Guid riidLocal;

    private void OnButtonPressed(SystemMediaTransportControls sender,
        SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        try
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                case SystemMediaTransportControlsButton.Pause:
                    _playback.PlayPause();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    _playback.Next();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    _playback.Previous();
                    break;
            }
        }
        catch
        {
            // SMTC callback failures must not reach the UI.
        }
    }

    private void OnTrackChanged(object? sender, Track? track)
    {
        try
        {
            var c = _smtc;
            if (c is null) return;
            if (track is null)
            {
                c.DisplayProperties.ClearAll();
                c.PlaybackStatus = MediaPlaybackStatus.Stopped;
                return;
            }
            var props = c.DisplayProperties;
            props.Type = MediaPlaybackType.Music;
            props.MusicProperties.Title = track.Title;
            props.MusicProperties.Artist = track.DisplayArtist;
            props.MusicProperties.AlbumTitle = track.DisplayAlbum;
            props.ApplyDisplayProperties();
        }
        catch
        {
            // Metadata failures are cosmetic.
        }
    }

    private void OnPlayingChanged(object? sender, bool playing)
    {
        try
        {
            if (_smtc is not null)
                _smtc.PlaybackStatus = playing ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
        }
        catch { }
    }

    /// <summary>Called by the UI at ~1 Hz to keep the SMTC timeline fresh.</summary>
    public void TickTimeline()
    {
        try
        {
            var c = _smtc;
            if (c is null || !_playback.IsPlaying) return;
            var tl = c.TimelineProperties;
            tl.Position = _playback.Position;
            tl.EndTime = _playback.Duration;
            tl.PlaybackRate = 1.0;
            c.UpdateTimelineProperties();
        }
        catch { }
    }

    private void UpdateAll()
    {
        try
        {
            if (_smtc is not null)
                _smtc.PlaybackStatus = _playback.IsPlaying
                    ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
        }
        catch { }
    }

    public void Dispose()
    {
        _playback.TrackChanged -= OnTrackChanged;
        _playback.PlayingChanged -= OnPlayingChanged;
        try { _smtc?.Dispose(); } catch { }
    }

    // ── Win32 activation ─────────────────────────────────────────────────────
    [DllImport("combase.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void WindowsCreateString(string source, uint length, out IntPtr hstring);

    [DllImport("combase.dll", PreserveSig = false)]
    private static extern void RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);
}
