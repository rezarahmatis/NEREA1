using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NEREA.Core.Models;

namespace NEREA.Audio;

/// <summary>
/// Builds a seekable WaveStream for a track. Media Foundation decodes
/// MP3/WAV/FLAC/M4A/AAC/WMA; NVorbis decodes OGG Vorbis. Seeking operates
/// directly on the WaveStream, so it survives every pipeline rebuild.
/// </summary>
public static class TrackStreamFactory
{
    public static WaveStream Create(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".ogg" or ".oga" => new VorbisWaveReader(path),
            _ => new MediaFoundationReader(path)
        };
    }

    public static bool IsSupported(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".mp3" or ".wav" or ".flac" or ".ogg" or ".oga" or ".m4a" or ".aac" or ".wma";
    }
}

/// <summary>
/// A paused playback chain held while the output device is being switched,
/// so playback resumes seamlessly at the current position.
/// </summary>
public sealed class PlaybackChain : IDisposable
{
    public WaveStream Stream { get; }
    public ISampleProvider Output { get; }

    private readonly List<IDisposable> _owned;

    public PlaybackChain(Track track, int sampleRate, int channels, Action<float[], int, int> tap)
    {
        Stream = TrackStreamFactory.Create(track.Path);
        _owned = new List<IDisposable> { Stream };

        ISampleProvider sp = Stream.ToSampleProvider();
        sp = new WdlResamplingSampleProvider(sp, sampleRate);
        sp = new ChannelAdapterSampleProvider(sp, channels);
        var volume = new VolumeSampleProvider(sp);
        Output = new SampleTapProvider(volume, tap);
        Volume = volume;
    }

    public VolumeSampleProvider Volume { get; }

    public void Dispose()
    {
        foreach (var d in _owned) d.Dispose();
        _owned.Clear();
    }
}
