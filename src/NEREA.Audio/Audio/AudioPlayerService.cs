using NAudio.CoreAudioApi;
using NAudio.Wave;
using NEREA.Core.Models;
using NEREA.Core.Services;

namespace NEREA.Audio;

/// <summary>
/// The NEREA audio engine. WASAPI shared-mode output through NAudio, Media
/// Foundation + NVorbis decoding, an in-pipeline spectrum tap for the
/// ecosystem, optional loudness normalization and runtime output switching.
/// Fully independent from rendering - the user can pause, change worlds,
/// open panels or minimize without interrupting playback.
/// </summary>
public sealed class AudioPlayerService : IMusicPlayer
{
    private readonly SpectrumAnalyzer _analyzer = new();
    private readonly object _sync = new();

    private MMDeviceEnumerator _enumerator = new();
    private WasapiOut? _output;
    private PlaybackChain? _chain;
    private Track? _current;
    private bool _playing;
    private bool _muted;
    private bool _suppressEnded;
    private float _volume = 0.8f;
    private string? _deviceId;
    private System.Threading.Timer? _gainTimer;

    public event EventHandler? TrackEnded;
    public event EventHandler? StateChanged;

    public AudioPlayerService()
    {
        _gainTimer = new System.Threading.Timer(_ =>
        {
            try { ApplyVolume(); } catch { /* device gone mid-switch */ }
        }, null, 500, 500);
    }

    public bool IsPlaying => _playing;
    public MusicEnergy Energy => _analyzer.Current;

    public TimeSpan Position => _chain?.Stream.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan Duration => _chain?.Stream.TotalTime ?? TimeSpan.Zero;

    private MMDevice ResolveDevice(string? deviceId)
    {
        if (!string.IsNullOrEmpty(deviceId))
        {
            try
            {
                foreach (var d in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                    if (d.ID == deviceId) return d;
            }
            catch { /* fall through to default */ }
        }
        return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
    }

    private WasapiOut CreateOutput()
    {
        var device = ResolveDevice(_deviceId);
        return new WasapiOut(device, AudioClientShareMode.Shared, true, 180);
    }

    public void Load(Track track)
    {
        lock (_sync)
        {
            var wasPlaying = _playing;
            _suppressEnded = true;
            try { _output?.Stop(); } catch { }
            _suppressEnded = false;

            _chain?.Dispose();
            _chain = null;

            try
            {
                var device = ResolveDevice(_deviceId);
                var mix = device.AudioClient.MixFormat;
                var channels = mix.Channels;
                var chain = new PlaybackChain(track, mix.SampleRate, channels, (buf, off, cnt) => _analyzer.Push(buf, off, cnt, channels));
                _chain = chain;

                _output?.Dispose();
                _output = CreateOutput();
                _output.PlaybackStopped += OnPlaybackStopped;
                _output.Init(chain.Output);
                _current = track;
                ApplyVolume();

                if (wasPlaying) Play();
            }
            catch (Exception)
            {
                // Unsupported or unreadable file: keep the player alive.
                _playing = false;
                _current = null;
                StateChanged?.Invoke(this, EventArgs.Empty);
                throw;
            }
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_suppressEnded) return;
        if (e.Exception is null && _chain is not null)
        {
            _playing = false;
            TrackEnded?.Invoke(this, EventArgs.Empty);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Play()
    {
        lock (_sync)
        {
            if (_output is null || _chain is null) return;
            _output.Play();
            _playing = true;
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        lock (_sync)
        {
            if (_output is null) return;
            _output.Pause();
            _playing = false;
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        lock (_sync)
        {
            _suppressEnded = true;
            try { _output?.Stop(); } catch { }
            _suppressEnded = false;
            _playing = false;
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(TimeSpan position)
    {
        lock (_sync)
        {
            if (_chain is null) return;
            var stream = _chain.Stream;
            if (stream.Length <= 0) return;
            var clamped = position;
            if (clamped < TimeSpan.Zero) clamped = TimeSpan.Zero;
            if (clamped > stream.TotalTime) clamped = stream.TotalTime;
            long bytes = (long)(clamped.TotalSeconds * stream.WaveFormat.AverageBytesPerSecond);
            stream.Seek(bytes - bytes % stream.WaveFormat.BlockAlign, SeekOrigin.Begin);
        }
    }

    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0f, 1f);
        ApplyVolume();
    }

    public void SetMuted(bool muted)
    {
        _muted = muted;
        ApplyVolume();
    }

    public void SetNormalization(bool enabled)
    {
        _analyzer.NormalizationEnabled = enabled;
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        var chain = _chain;
        if (chain is null) return;
        float gain = _analyzer.NormalizationGain;
        float v = (_muted ? 0f : _volume) * gain;
        chain.Volume.Volume = Math.Clamp(v * v * 1.6f, 0f, 1.4f);
    }

    public void SetOutputDevice(string? deviceId)
    {
        lock (_sync)
        {
            var wasPlaying = _playing;
            _suppressEnded = true;
            try { _output?.Stop(); } catch { }
            _suppressEnded = false;
            try
            {
                _deviceId = deviceId;
                _output?.Dispose();
                _output = CreateOutput();
                _output.PlaybackStopped += OnPlaybackStopped;
                if (_chain is not null) _output.Init(_chain.Output);
                if (wasPlaying && _chain is not null) _output.Play();
            }
            catch
            {
                // Invalid device: keep current output.
            }
        }
    }

    public IReadOnlyList<AudioDeviceInfo> EnumerateOutputDevices()
    {
        var result = new List<AudioDeviceInfo>();
        try
        {
            MMDevice? def = null;
            try { def = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console); } catch { }
            foreach (var d in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                result.Add(new AudioDeviceInfo(d.ID, d.FriendlyName, def is not null && d.ID == def.ID));
        }
        catch
        {
            // No audio endpoints: empty list.
        }
        return result;
    }

    public void Dispose()
    {
        _gainTimer?.Dispose();
        _gainTimer = null;
        lock (_sync)
        {
            _suppressEnded = true;
            try { _output?.Stop(); } catch { }
            _output?.Dispose();
            _output = null;
            _chain?.Dispose();
            _chain = null;
        }
        _analyzer.Dispose();
    }
}
