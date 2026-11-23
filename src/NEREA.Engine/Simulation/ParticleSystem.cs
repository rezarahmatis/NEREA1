using System.Numerics;
using NEREA.Core.Models;
using NEREA.Engine.Rendering;

namespace NEREA.Engine.Simulation;

/// <summary>
/// Ambient particulates: drifting motes and rising bubbles. Populations are
/// preallocated; music (treble) brightens motes, bubbles ride the beat.
/// </summary>
public sealed class ParticleSystem
{
    private struct Mote
    {
        public Vector3 Position;
        public float Seed;
    }

    private struct Bubble
    {
        public Vector3 Position;
        public float Size;
        public float Wobble;
        public float Speed;
    }

    private Mote[] _motes = Array.Empty<Mote>();
    private Bubble[] _bubbles = new Bubble[256];
    private int _bubbleCount;
    private int _moteTarget;
    private float _moteScale = 1f;

    private float _boundsRadius = 26f;
    private float _depth = 12f;
    private float _bubbleRate = 0.3f;
    private float _bubbleAccum;
    private readonly Random _rng = new(24680);
    private float _time;

    public void Configure(WorldDefinition world, int moteCount, float bubbleScale)
    {
        _boundsRadius = world.BoundsRadius;
        _depth = world.Depth;
        _bubbleRate = world.Bubbles * bubbleScale;
        _moteTarget = Math.Min(moteCount, EngineLimits.MaxParticles - 256);
        if (_motes.Length != _moteTarget)
        {
            _motes = new Mote[_moteTarget];
            for (int i = 0; i < _moteTarget; i++) _motes[i] = SpawnMote(_rng);
        }
        _bubbleCount = 0;
    }

    private Mote SpawnMote(Random rng)
    {
        float ang = rng.NextSingle() * MathF.PI * 2f;
        float rad = rng.NextSingle() * _boundsRadius;
        return new Mote
        {
            Position = new Vector3(MathF.Cos(ang) * rad, -rng.NextSingle() * _depth, MathF.Sin(ang) * rad),
            Seed = rng.NextSingle() * 100f
        };
    }

    public void Update(float dt, MusicEnergy music, float treble)
    {
        _time += dt;

        // Motes ride a gentle, breathing current.
        float current = 0.12f + music.Level * 0.18f;
        for (int i = 0; i < _motes.Length; i++)
        {
            ref var m = ref _motes[i];
            m.Position.X += MathF.Sin(_time * 0.14f + m.Seed) * current * dt * 1.6f;
            m.Position.Y += MathF.Cos(_time * 0.11f + m.Seed * 1.7f) * current * dt * 0.5f;
            m.Position.Z += MathF.Cos(_time * 0.09f + m.Seed * 2.3f) * current * dt * 1.6f;
            m.Position.Y -= dt * 0.012f;

            // Wrap softly inside the water column.
            float radial = new Vector2(m.Position.X, m.Position.Z).Length();
            if (radial > _boundsRadius)
            {
                m.Position.X *= 0.96f;
                m.Position.Z *= 0.96f;
            }
            if (m.Position.Y > -0.3f) m.Position.Y = -_depth + 0.4f;
            if (m.Position.Y < -_depth + 0.2f) m.Position.Y = -0.4f;
        }

        // Bubbles vent from the floor; the beat releases small bursts.
        _bubbleAccum += dt * (_bubbleRate + music.Beat * _bubbleRate * 0.8f);
        while (_bubbleAccum >= 1f)
        {
            _bubbleAccum -= 1f;
            if (_bubbleCount >= _bubbles.Length) break;
            float ang = _rng.NextSingle() * MathF.PI * 2f;
            float rad = _rng.NextSingle() * _boundsRadius * 0.9f;
            _bubbles[_bubbleCount++] = new Bubble
            {
                Position = new Vector3(MathF.Cos(ang) * rad, -_depth + 0.3f, MathF.Sin(ang) * rad),
                Size = 0.03f + _rng.NextSingle() * 0.06f,
                Wobble = _rng.NextSingle() * MathF.PI * 2f,
                Speed = 0.35f + _rng.NextSingle() * 0.5f
            };
        }

        for (int i = _bubbleCount - 1; i >= 0; i--)
        {
            ref var b = ref _bubbles[i];
            b.Position.Y += b.Speed * dt * (1f + music.Level * 0.4f);
            b.Position.X += MathF.Sin(_time * 2.2f + b.Wobble) * dt * 0.08f;
            b.Position.Z += MathF.Cos(_time * 1.8f + b.Wobble) * dt * 0.08f;
            if (b.Position.Y > -0.35f)
            {
                // Pop at the surface: swap with last and remove.
                _bubbles[i] = _bubbles[--_bubbleCount];
            }
        }
    }

    /// <summary>Fills the shared particle instance buffer. Motes occupy [0, moteCount), bubbles follow.</summary>
    public int FillInstances(Span<ParticleInstance> instances, float treble)
    {
        int n = 0;
        float moteAlpha = Math.Clamp(0.35f + treble * 0.8f, 0.25f, 0.85f);
        for (int i = 0; i < _motes.Length && n < instances.Length; i++)
        {
            ref var m = ref _motes[i];
            instances[n++] = new ParticleInstance
            {
                PosSize = new Vector4(m.Position, 0.014f + (m.Seed % 1f) * 0.012f),
                Params = new Vector4(moteAlpha, m.Seed, 0f, 1f)
            };
        }
        for (int i = 0; i < _bubbleCount && n < instances.Length; i++)
        {
            ref var b = ref _bubbles[i];
            instances[n++] = new ParticleInstance
            {
                PosSize = new Vector4(b.Position, b.Size),
                Params = new Vector4(0.8f, b.Wobble, 1f, 1f)
            };
        }
        return n;
    }

    public int MoteCount => _motes.Length;
    public int BubbleCount => _bubbleCount;
}
