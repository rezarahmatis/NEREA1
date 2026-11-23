using NEREA.Core.Models;
using NEREA.Core.Services;

namespace NEREA.Audio;

/// <summary>
/// High-level playback orchestration: queue, shuffle, repeat, transport
/// commands, library play statistics. All commands are safe from any thread;
/// UI view-models subscribe to the events.
/// </summary>
public sealed class PlaybackController : IDisposable
{
    private readonly IMusicPlayer _player;
    private readonly MusicLibraryService _library;

    private readonly List<Track> _queue = new();
    private readonly List<int> _order = new();
    private int _position = -1;
    private bool _shuffle;
    private RepeatMode _repeat = RepeatMode.Off;
    private readonly Random _random = new();
    private readonly object _sync = new();

    public PlaybackController(IMusicPlayer player, MusicLibraryService library)
    {
        _player = player;
        _library = library;
        _player.TrackEnded += OnTrackEnded;
    }

    public event EventHandler<Track?>? TrackChanged;
    public event EventHandler<bool>? PlayingChanged;
    public event EventHandler? QueueChanged;

    public Track? Current { get; private set; }
    public bool IsPlaying => _player.IsPlaying;
    public TimeSpan Position => _player.Position;
    public TimeSpan Duration => _player.Duration;
    public MusicEnergy Energy => _player.Energy;

    public bool Shuffle
    {
        get => _shuffle;
        set
        {
            _shuffle = value;
            lock (_sync) RebuildOrder();
            QueueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public RepeatMode Repeat
    {
        get => _repeat;
        set
        {
            _repeat = value;
            QueueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyList<Track> Queue
    {
        get { lock (_sync) return _queue.ToList(); }
    }

    public int QueueIndex => _position;

    // ── Queue management ─────────────────────────────────────────────────

    public void PlayTracks(IEnumerable<Track> tracks, int startIndex)
    {
        lock (_sync)
        {
            _queue.Clear();
            _queue.AddRange(tracks);
            _position = _queue.Count > 0 ? Math.Clamp(startIndex, 0, _queue.Count - 1) : -1;
            RebuildOrder();
        }
        QueueChanged?.Invoke(this, EventArgs.Empty);
        PlayCurrent();
    }

    public void Enqueue(Track track)
    {
        lock (_sync)
        {
            _queue.Add(track);
            RebuildOrder();
            if (_position < 0 && Current is null)
            {
                _position = _queue.Count - 1;
                QueueChanged?.Invoke(this, EventArgs.Empty);
                PlayCurrent();
                return;
            }
        }
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public void EnqueueNext(Track track)
    {
        lock (_sync)
        {
            int insert = _position + 1;
            _queue.Insert(Math.Min(insert, _queue.Count), track);
            RebuildOrder();
        }
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveFromQueue(int index)
    {
        lock (_sync)
        {
            if (index < 0 || index >= _queue.Count) return;
            _queue.RemoveAt(index);
            if (index < _position) _position--;
            else if (index == _position) { /* current track removed: keep playing, index stays */ _position = Math.Min(_position, _queue.Count - 1); }
            RebuildOrder();
        }
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public void JumpTo(int index)
    {
        lock (_sync)
        {
            if (index < 0 || index >= _queue.Count) return;
            _position = index;
        }
        PlayCurrent();
    }

    public void ClearQueue()
    {
        lock (_sync)
        {
            _queue.Clear();
            _order.Clear();
            _position = -1;
        }
        Current = null;
        _player.Stop();
        TrackChanged?.Invoke(this, null);
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Transport ────────────────────────────────────────────────────────────

    public void PlayPause()
    {
        if (_player.IsPlaying) _player.Pause();
        else if (Current is not null) _player.Play();
        else if (_queue.Count > 0) PlayCurrent();
        PlayingChanged?.Invoke(this, _player.IsPlaying);
    }

    /// <summary>Pauses playback; safe to call when already paused or stopped.</summary>
    public void Pause()
    {
        if (_player.IsPlaying)
        {
            _player.Pause();
            PlayingChanged?.Invoke(this, _player.IsPlaying);
        }
    }

    public void Next()
    {
        if (_repeat == RepeatMode.One && Current is not null)
        {
            _player.Seek(TimeSpan.Zero);
            if (!_player.IsPlaying) _player.Play();
            return;
        }
        MoveTrack(+1, false);
    }

    public void NextAuto() => MoveTrack(+1, true);

    public void Previous()
    {
        if (_player.Position > TimeSpan.FromSeconds(3))
        {
            _player.Seek(TimeSpan.Zero);
            return;
        }
        MoveTrack(-1, false);
    }

    private void MoveTrack(int delta, bool auto)
    {
        bool playNext;
        lock (_sync)
        {
            if (_queue.Count == 0) return;
            playNext = ComputeNextIndex(delta, auto);
            if (!playNext) return;
        }
        PlayCurrent();
    }

    /// <summary>Advances <see cref="_position"/>; returns false when the queue is exhausted.</summary>
    private bool ComputeNextIndex(int delta, bool auto)
    {
        if (_queue.Count == 0) return false;
        if (_shuffle && _order.Count > 0)
        {
            int orderPos = _order.IndexOf(_position);
            int next = orderPos + delta;
            if (next >= _order.Count)
            {
                if (_repeat == RepeatMode.All || !auto)
                {
                    ShuffleOrder();
                    next = 0;
                }
                else return false;
            }
            if (next < 0) next = _order.Count - 1;
            _position = _order[next];
            return true;
        }
        int idx = _position + delta;
        if (idx >= _queue.Count)
        {
            if (_repeat == RepeatMode.All || !auto) idx = 0;
            else return false;
        }
        if (idx < 0) idx = 0;
        _position = idx;
        return true;
    }

    private void RebuildOrder()
    {
        _order.Clear();
        for (int i = 0; i < _queue.Count; i++) _order.Add(i);
        if (_shuffle) ShuffleOrder();
    }

    private void ShuffleOrder()
    {
        for (int i = _order.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (_order[i], _order[j]) = (_order[j], _order[i]);
        }
        if (_position >= 0 && _order.Count > 0)
        {
            // Keep the current track first for a natural listening flow.
            int pos = _order.IndexOf(_position);
            if (pos > 0) (_order[0], _order[pos]) = (_order[pos], _order[0]);
        }
    }

    private void PlayCurrent()
    {
        Track? track;
        lock (_sync)
        {
            if (_position < 0 || _position >= _queue.Count)
            {
                Current = null;
                _player.Stop();
                TrackChanged?.Invoke(this, null);
                return;
            }
            track = _queue[_position];
        }
        try
        {
            _player.Load(track);
            _player.Play();
            Current = track;
            _library.RecordPlayback(track);
            TrackChanged?.Invoke(this, track);
        }
        catch
        {
            // Unreadable track: skip forward rather than stalling the session.
            Current = null;
            TrackChanged?.Invoke(this, null);
            MoveTrack(+1, true);
        }
    }

    private void OnTrackEnded(object? sender, EventArgs e) => NextAuto();

    // ── Volume passthrough ───────────────────────────────────────────────

    public void SetVolume(float v) => _player.SetVolume(v);
    public void SetMuted(bool muted) => _player.SetMuted(muted);
    public void Seek(TimeSpan pos) => _player.Seek(pos);

    public void Dispose() => _player.TrackEnded -= OnTrackEnded;
}
