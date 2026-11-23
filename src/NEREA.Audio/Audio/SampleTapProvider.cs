using NAudio.Wave;

namespace NEREA.Audio;

/// <summary>
/// Transparent sample-provider tap: every rendered audio buffer is forwarded
/// to the spectrum analyzer before playback. Zero allocation per read.
/// </summary>
public sealed class SampleTapProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly Action<float[], int, int> _tap;

    public SampleTapProvider(ISampleProvider source, Action<float[], int, int> tap)
    {
        _source = source;
        _tap = tap;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (read > 0) _tap(buffer, offset, read);
        return read;
    }
}

/// <summary>
/// Adapts any source to a target channel count (1->2 duplicate, N->2 downmix).
/// Keeps the playback chain at the device mix format without Media Foundation.
/// </summary>
public sealed class ChannelAdapterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _targetChannels;

    public ChannelAdapterSampleProvider(ISampleProvider source, int targetChannels)
    {
        _source = source;
        _targetChannels = Math.Clamp(targetChannels, 1, 2);
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int frames = count / _targetChannels;
        int read = _source.Read(buffer, offset, frames * _source.WaveFormat.Channels);
        int sourceChannels = _source.WaveFormat.Channels;
        if (sourceChannels == _targetChannels) return read;

        int readFrames = read / sourceChannels;
        if (_targetChannels == 2 && sourceChannels == 1)
        {
            for (int i = readFrames - 1; i >= 0; i--)
            {
                float s = buffer[offset + i];
                buffer[offset + i * 2] = s;
                buffer[offset + i * 2 + 1] = s;
            }
            return readFrames * 2;
        }
        if (_targetChannels == 2 && sourceChannels > 2)
        {
            for (int i = 0; i < readFrames; i++)
            {
                int baseIdx = offset + i * sourceChannels;
                float l = 0f, r = 0f;
                int half = sourceChannels / 2;
                for (int c = 0; c < half; c++) l += buffer[baseIdx + c];
                for (int c = half; c < sourceChannels; c++) r += buffer[baseIdx + c];
                l /= half;
                r /= (sourceChannels - half);
                buffer[offset + i * 2] = l;
                buffer[offset + i * 2 + 1] = r;
            }
            return readFrames * 2;
        }
        if (_targetChannels == 1 && sourceChannels > 1)
        {
            for (int i = 0; i < readFrames; i++)
            {
                int baseIdx = offset + i * sourceChannels;
                float sum = 0f;
                for (int c = 0; c < sourceChannels; c++) sum += buffer[baseIdx + c];
                buffer[offset + i] = sum / sourceChannels;
            }
            return readFrames;
        }
        return read;
    }
}
