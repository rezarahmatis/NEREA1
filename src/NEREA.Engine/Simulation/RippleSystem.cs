using System.Numerics;
using NEREA.Engine.Rendering;

namespace NEREA.Engine.Simulation;

/// <summary>
/// Pointer-driven water ripples: expanding rings on the floor that scatter
/// fish (via FishSimulation impulses) and modulate the water shader.
/// </summary>
public sealed class RippleSystem
{
    private struct Ripple
    {
        public Vector2 Center;
        public float Age;
        public float Strength;
        public float Life;
    }

    private readonly Ripple[] _ripples = new Ripple[EngineLimits.MaxRipples];
    private int _count;
    private float _depth = 12f;

    // Interaction throttle.
    private Vector2 _lastPointerWorld;
    private float _lastMoveRipple;
    private float _time;
    private float _strengthScale = 1f;

    public void SetStrength(float scale) => _strengthScale = scale;

    public void SetDepth(float depth) => _depth = depth;

    /// <summary>Pointer moved in normalized [0,1]^2 panel coordinates.</summary>
    public void OnPointerMove(Vector2 worldXz, float now, float sensitivity)
    {
        float moved = Vector2.Distance(worldXz, _lastPointerWorld);
        if (now - _lastMoveRipple < 0.28f || moved < 0.5f * sensitivity) return;
        _lastMoveRipple = now;
        _lastPointerWorld = worldXz;
        Add(worldXz, 2.4f, 0.35f * _strengthScale, 1.6f);
    }

    public void OnPointerPress(Vector2 worldXz)
    {
        Add(worldXz, 6.5f, 1.0f * _strengthScale, 2.0f);
    }

    public void OnPointerDrag(Vector2 worldXz, float now)
    {
        if (now - _lastMoveRipple < 0.12f) return;
        _lastMoveRipple = now;
        Add(worldXz, 3.2f, 0.5f * _strengthScale, 1.4f);
    }

    /// <summary>Gentle ambient ripple pulses on strong beats.</summary>
    public void OnBeat(System.Numerics.Vector2 hint)
    {
        Add(hint, 4.5f, 0.22f * _strengthScale, 2.6f);
    }

    private void Add(Vector2 center, float radius, float strength, float life)
    {
        int idx;
        if (_count < _ripples.Length)
        {
            idx = _count++;
        }
        else
        {
            // Recycle the weakest / oldest slot.
            idx = 0;
            float best = float.MaxValue;
            for (int i = 0; i < _count; i++)
            {
                float score = _ripples[i].Strength * (1f - _ripples[i].Age);
                if (score < best) { best = score; idx = i; }
            }
        }
        _ripples[idx] = new Ripple
        {
            Center = center,
            Age = 0f,
            Strength = strength,
            Life = Math.Clamp(radius * 0.22f, 1.0f, 2.6f)
        };
    }

    public void Update(float dt)
    {
        _time += dt;
        for (int i = _count - 1; i >= 0; i--)
        {
            _ripples[i].Age += dt / _ripples[i].Life;
            _ripples[i].Age = Math.Clamp(_ripples[i].Age, 0f, 1f);
            if (_ripples[i].Age >= 1f)
            {
                _ripples[i] = _ripples[--_count];
            }
        }
    }

    /// <summary>Copies up to 6 ripples into the per-frame constants.</summary>
    public void Fill(ref PerFrameConstants c)
    {
        Span<Vector4> slots = stackalloc Vector4[]
        {
            c.Ripples0, c.Ripples1, c.Ripples2, c.Ripples3, c.Ripples4, c.Ripples5
        };
        for (int i = 0; i < 6; i++)
        {
            if (i < _count)
                slots[i] = new Vector4(_ripples[i].Center.X, _ripples[i].Center.Y, _ripples[i].Age, _ripples[i].Strength);
            else
                slots[i] = Vector4.Zero;
        }
        c.Ripples0 = slots[0];
        c.Ripples1 = slots[1];
        c.Ripples2 = slots[2];
        c.Ripples3 = slots[3];
        c.Ripples4 = slots[4];
        c.Ripples5 = slots[5];
    }

    /// <summary>Appends strong ripple world positions (for fish scatter impulses) to a caller-owned list.</summary>
    public void CollectStrongRipples(List<(Vector2 center, float radius)> list)
    {
        for (int i = 0; i < _count; i++)
        {
            if (_ripples[i].Strength > 0.5f)
                list.Add((_ripples[i].Center, 5.5f * _ripples[i].Strength));
        }
    }

    /// <summary>Fills the ring quad instances for the ripple pass.</summary>
    public int FillInstances(Span<ParticleInstance> instances)
    {
        int n = Math.Min(_count, instances.Length);
        for (int i = 0; i < n; i++)
        {
            instances[i] = new ParticleInstance
            {
                PosSize = new Vector4(_ripples[i].Center.X, _ripples[i].Center.Y, _ripples[i].Age * 4.5f, 0),
                Params = new Vector4(_ripples[i].Age, _ripples[i].Strength, 0, 0)
            };
        }
        return n;
    }
}
