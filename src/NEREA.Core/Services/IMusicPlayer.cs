using NEREA.Core.Models;

namespace NEREA.Core.Services;

/// <summary>
/// The audio playback abstraction. Implementations must be fully independent
/// from rendering: pausing playback, changing worlds or opening UI panels
/// never interrupts or depends on the graphics loop.
/// </summary>
public interface IMusicPlayer : IDisposable
{
    bool IsPlaying { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }

    /// <summary>Raised when a track reaches its natural end.</summary>
    event EventHandler? TrackEnded;
    /// <summary>Raised when play/pause state changes.</summary>
    event EventHandler? StateChanged;

    void Load(Track track);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);

    void SetVolume(float volume);
    void SetMuted(bool muted);
    void SetNormalization(bool enabled);

    /// <summary>Switches the output endpoint; playback continues where it was.</summary>
    void SetOutputDevice(string? deviceId);

    IReadOnlyList<AudioDeviceInfo> EnumerateOutputDevices();

    /// <summary>Live spectrum snapshot for the ecosystem. Always safe to read from any thread.</summary>
    MusicEnergy Energy { get; }
}
