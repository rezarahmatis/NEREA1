using System.Diagnostics;
using NAudio.Dsp;
using NEREA.Core.Models;

namespace NEREA.Audio;

/// <summary>
/// Live music analysis: ring buffer fed from the playback pipeline, FFT on a
/// low-rate background thread, band energies with musical attack/release
/// smoothing, beat detection and loudness-normalization peak tracking.
/// Runs at ~25 Hz - the ecosystem only needs an organic, smoothed envelope.
/// </summary>
public sealed class SpectrumAnalyzer : IDisposable
{
    public const int FftSize = 2048;
    private const int RingSize = 8192;
    private const float SampleRate = 48000f;

    private readonly float[] _ring = new float[RingSize];
    private int _ringWrite;
    private readonly Complex[] _fft = new Complex[FftSize];
    private readonly float[] _window = new float[FftSize];

    private readonly Thread _thread;
    private readonly CancellationTokenSource _cts = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private readonly object _gate = new();
    private MusicEnergy _smoothed = MusicEnergy.Silence;
    private float _prevBass;
    private float _beatDecay;
    private float _bassFluxAvg = 0.02f;
    private float _peak = 0.25f;

    private volatile bool _normalize;

    public SpectrumAnalyzer()
    {
        for (int i = 0; i < FftSize; i++)
            _window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (FftSize - 1)));

        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "NEREA.SpectrumAnalyzer",
            Priority = ThreadPriority.BelowNormal
        };
        _thread.Start();
    }

    public bool NormalizationEnabled
    {
        get => _normalize;
        set => _normalize = value;
    }

    /// <summary>Current smoothed energy snapshot. Safe from any thread.</summary>
    public MusicEnergy Current
    {
        get { lock (_gate) return _smoothed; }
    }

    /// <summary>Suggested loudness gain (only meaningful when normalization is enabled).</summary>
    public float NormalizationGain
    {
        get
        {
            lock (_gate)
            {
                if (!_normalize) return 1f;
                return Math.Clamp(0.85f / MathF.Max(_peak, 0.05f), 0.6f, 2.4f);
            }
        }
    }

    /// <summary>Called from the audio render thread with interleaved float samples.</summary>
    public void Push(float[] buffer, int offset, int count, int channels)
    {
        if (channels <= 0) return;
        int frames = count / channels;
        lock (_ring)
        {
            for (int i = 0; i < frames; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += buffer[offset + i * channels + c];
                _ring[_ringWrite] = sum / channels;
                _ringWrite = (_ringWrite + 1) & (RingSize - 1);
            }
        }
        // Loudness peak tracking (slow decay, for optional normalization).
        float framePeak = 0f;
        for (int i = 0; i < count; i++)
        {
            float a = MathF.Abs(buffer[offset + i]);
            if (a > framePeak) framePeak = a;
        }
        lock (_gate)
        {
            if (framePeak > _peak) _peak = framePeak;
            else _peak = MathF.Max(_peak * 0.9995f, framePeak);
        }
    }

    private void Loop()
    {
        var next = TimeSpan.Zero;
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                next = _clock.Elapsed + TimeSpan.FromMilliseconds(40);
                Analyze();
                var remaining = next - _clock.Elapsed;
                if (remaining > TimeSpan.Zero) Thread.Sleep(remaining);
            }
            catch
            {
                Thread.Sleep(100);
            }
        }
    }

    private void Analyze()
    {
        lock (_ring)
        {
            int start = (_ringWrite - FftSize) & (RingSize - 1);
            for (int i = 0; i < FftSize; i++)
            {
                float s = _ring[(start + i) & (RingSize - 1)];
                _fft[i].X = s * _window[i];
                _fft[i].Y = 0f;
            }
        }

        FastFourierTransform.FFT(true, 11, _fft);

        float binHz = SampleRate / FftSize;
        float bass = BandEnergy(20f, 160f, binHz);
        float mid = BandEnergy(160f, 2000f, binHz);
        float treble = BandEnergy(2000f, 12000f, binHz);

        // Compress dynamic range for organic visuals.
        bass = MathF.Sqrt(bass);
        mid = MathF.Sqrt(mid);
        treble = MathF.Sqrt(treble);
        float level = (bass + mid + treble) / 3f;

        lock (_gate)
        {
            float attack = 0.38f, release = 0.055f;
            _smoothed.Bass = Smooth(_smoothed.Bass, bass, attack, release);
            _smoothed.Mid = Smooth(_smoothed.Mid, mid, attack, release);
            _smoothed.Treble = Smooth(_smoothed.Treble, treble, attack, release);
            _smoothed.Level = Smooth(_smoothed.Level, level, attack, release);

            // Beat: onset detection on bass flux.
            float flux = MathF.Max(0f, _smoothed.Bass - _prevBass);
            _bassFluxAvg = _bassFluxAvg * 0.92f + flux * 0.08f;
            float threshold = MathF.Max(_bassFluxAvg * 2.6f, 0.035f);
            if (flux > threshold)
                _beatDecay = 1f;
            _prevBass = _smoothed.Bass;
            _beatDecay = MathF.Max(0f, _beatDecay - 0.055f);
            _smoothed.Beat = _beatDecay;

            float activity = 0.85f + _smoothed.Level * 0.9f + _smoothed.Beat * 0.12f;
            _smoothed.Activity = Math.Clamp(activity, 0.6f, 1.7f);
        }
    }

    private float BandEnergy(float loHz, float hiHz, float binHz)
    {
        int lo = Math.Max(1, (int)(loHz / binHz));
        int hi = Math.Min(FftSize / 2 - 1, (int)(hiHz / binHz));
        float sum = 0f;
        for (int i = lo; i <= hi; i++)
        {
            float re = _fft[i].X, im = _fft[i].Y;
            float mag = MathF.Sqrt(re * re + im * im) / FftSize;
            sum += mag;
        }
        return sum / Math.Max(1, hi - lo + 1) * 28f;
    }

    private static float Smooth(float current, float target, float attack, float release) =>
        target > current
            ? current + (target - current) * attack
            : current + (target - current) * release;

    public void Dispose()
    {
        _cts.Cancel();
        try { _thread.Join(500); } catch { /* shutting down */ }
        _cts.Dispose();
    }
}
