using System.Numerics;
using NEREA.Core.Content;
using NEREA.Core.Models;
using NEREA.Engine.Rendering;

namespace NEREA.Engine.Simulation;

/// <summary>
/// Procedural fish behavior: species-faithful wandering, schooling
/// (alignment / cohesion / separation), boundary and obstacle avoidance,
/// depth preference, fear responses to pointer ripples, curiosity toward the
/// viewer and resting cycles. Zero per-frame allocations.
/// </summary>
public sealed class FishSimulation
{
    private struct Agent
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public int Species;
        public float Phase;
        public float SpeedMul;
        public float RestTimer;
        public float Seed;
    }

    private Agent[] _agents = new Agent[EngineLimits.MaxFish];
    private int _agentCount;
    private SpeciesDefinition[] _species = Array.Empty<SpeciesDefinition>();

    // Precomputed GPU-side per-species parameters.
    private readonly FishInstance[] _gpuSpecies = new FishInstance[128];

    // Spatial hash grid (linked lists over the agent array; no allocations).
    private const float CellSize = 3.2f;
    private int[] _cellHeads = new int[0];
    private int[] _agentNext = new int[EngineLimits.MaxFish];
    private int _gridDim;

    private Vector3 _cameraPos = new(0, -6, 12);
    private float _boundsRadius = 26f;
    private float _depth = 12f;
    private float _time;

    private readonly record struct Impulse(Vector3 Point, float Radius, float Strength);
    private readonly List<Impulse> _impulses = new();

    public int AgentCount => _agentCount;
    public int SpeciesCount => _species.Length;

    /// <summary>Rebuilds the population from world bounds and species counts.</summary>
    public void Configure(WorldDefinition world, IReadOnlyList<SpeciesCount> speciesCounts, int maxFish)
    {
        _boundsRadius = world.BoundsRadius;
        _depth = world.Depth;
        _gridDim = Math.Max(4, (int)(world.BoundsRadius * 2f / CellSize) + 1);
        _cellHeads = new int[_gridDim * _gridDim * 12];

        var active = speciesCounts
            .Where(s => s.Enabled && s.Count > 0)
            .Select(s => (def: SpeciesCatalog.ById(s.SpeciesId), count: s.Count))
            .Where(x => x.def is not null)
            .ToList();

        // Scale to the fish budget while preserving ratios.
        int total = active.Sum(x => x.count);
        float scale = total > maxFish ? maxFish / (float)total : 1f;

        _species = active.Select(x => x.def!).Take(32).ToArray();
        _agentCount = 0;
        var rng = new Random(97531);
        for (int i = 0; i < _species.Length; i++)
        {
            var def = _species[i];
            int count = Math.Max(1, (int)MathF.Round(active[i].count * scale));
            for (int c = 0; c < count && _agentCount < EngineLimits.MaxFish; c++)
            {
                float ang = rng.NextSingle() * MathF.PI * 2f;
                float rad = rng.NextSingle() * (_boundsRadius - 3f);
                float depthFrac = def.DepthLo + rng.NextSingle() * MathF.Max(0.02f, def.DepthHi - def.DepthLo);
                _agents[_agentCount] = new Agent
                {
                    Position = new Vector3(MathF.Cos(ang) * rad, -world.Depth * depthFrac, MathF.Sin(ang) * rad),
                    Velocity = RandomDirection(rng) * def.Speed * 0.6f,
                    Species = i,
                    Phase = rng.NextSingle(),
                    SpeedMul = 0.8f + rng.NextSingle() * 0.4f,
                    RestTimer = rng.NextSingle() * 20f,
                    Seed = rng.NextSingle() * 100f
                };
                _agentCount++;
            }
            CacheGpuSpecies(i, def);
        }
    }

    private static Vector3 RandomDirection(Random rng)
    {
        float a = rng.NextSingle() * MathF.PI * 2f;
        float y = rng.NextSingle() * 2f - 1f;
        return Vector3.Normalize(new Vector3(MathF.Cos(a), y * 0.4f, MathF.Sin(a)));
    }

    private void CacheGpuSpecies(int index, SpeciesDefinition def)
    {
        _gpuSpecies[index] = new FishInstance
        {
            PosScale = new Vector4(0, 0, 0, def.SizeM),
            Orient = Vector4.Zero,
            ColorA = new Vector4(ToRgb(def.ColorDeep), (int)def.Pattern),
            ColorB = new Vector4(ToRgb(def.ColorBright), def.Glow),
            ShapeA = new Vector4(def.BodyHeight, def.BodyWidth, def.HeadBlunt, def.TailFork),
            ShapeB = new Vector4(MathF.Max(0.05f, def.DorsalHeight), def.TailLength, def.PectoralSize, MathF.Max(1f, def.StripeCount)),
            Extras = new Vector4(ToRgb(def.ColorFin), (int)def.Form)
        };
    }

    private static System.Numerics.Vector3 ToRgb(string hex)
    {
        var s = hex.TrimStart('#');
        byte r = Convert.ToByte(s[..2], 16), g = Convert.ToByte(s[2..4], 16), b = Convert.ToByte(s[4..6], 16);
        return new Vector3(r / 255f, g / 255f, b / 255f);
    }

    public void SetCamera(Vector3 cameraPos) => _cameraPos = cameraPos;

    public void ApplyImpulse(Vector3 point, float radius, float strength) =>
        _impulses.Add(new Impulse(point, radius, strength));

    public void Update(float dt, MusicEnergy music, float reactionStrength)
    {
        _time += dt;
        BuildGrid();
        ConsumeImpulses(reactionStrength, music.Beat);

        for (int i = 0; i < _agentCount; i++)
        {
            ref var a = ref _agents[i];
            var def = _species[a.Species];

            // ── Resting cycles ──
            a.RestTimer -= dt;
            float restFactor = 1f;
            if (def.Restfulness > 0.01f)
            {
                float cycle = 30f + a.Seed * 2.5f;
                float restWindow = def.Restfulness * 10f;
                float phaseT = (cycle - a.RestTimer) % cycle;
                if (a.RestTimer <= 0) a.RestTimer = cycle;
                if (phaseT > cycle - restWindow) restFactor = 0.32f + 0.18f * MathF.Sin(_time * 0.7f + a.Seed);
            }

            // ── Neighbors via the spatial hash (same-species flocking) ──
            Vector3 alignment = Vector3.Zero, cohesion = Vector3.Zero, separation = Vector3.Zero;
            int sameCount = 0;
            int ax = Math.Clamp((int)((a.Position.X + _boundsRadius) / CellSize), 1, _gridDim - 2);
            int ay = Math.Clamp((int)((-a.Position.Y) / (_depth + 1f) * 12f), 1, 10);
            int az = Math.Clamp((int)((a.Position.Z + _boundsRadius) / CellSize), 1, _gridDim - 2);
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int cell = CellIndexOf(ax + dx, ay + dy, az + dz);
                for (int j = _cellHeads[cell]; j >= 0; j = _agentNext[j])
                {
                    if (j == i) continue;
                    ref var other = ref _agents[j];
                    Vector3 delta = a.Position - other.Position;
                    float d2 = delta.LengthSquared();
                    if (d2 < 2.56f) separation += delta / (d2 + 0.04f);
                    if (other.Species == a.Species && d2 < 100f)
                    {
                        sameCount++;
                        alignment += other.Velocity;
                        cohesion += other.Position;
                    }
                }
            }
            if (sameCount > 0) { alignment /= sameCount; cohesion = cohesion / sameCount - a.Position; }

            Vector3 accel = Vector3.Zero;
            accel += alignment * def.School * 1.4f;
            accel += cohesion * def.School * 0.9f;
            accel += separation * def.Separation * 3.2f;

            // ── Wander: smooth species-flavored direction change ──
            float wobble = MathF.Sin(_time * (0.25f + a.Seed * 0.01f) + a.Seed * 7.1f);
            Vector3 wanderDir = Vector3.Normalize(new Vector3(
                MathF.Sin(a.Seed + _time * 0.23f) + wobble * 0.4f,
                MathF.Sin(a.Seed * 2.3f + _time * 0.19f) * 0.35f,
                MathF.Cos(a.Seed * 1.7f + _time * 0.21f)));
            accel += wanderDir * 0.55f * (1f - def.School * 0.6f);

            // ── Depth preference ──
            float depthFrac = -a.Position.Y / MathF.Max(_depth, 1f);
            float lo = def.DepthLo, hi = def.DepthHi;
            if (depthFrac < lo) accel += new Vector3(0, -1, 0) * 1.1f * (lo - depthFrac) * 6f;
            if (depthFrac > hi) accel += new Vector3(0, 1, 0) * 1.1f * (depthFrac - hi) * 6f;

            // ── Boundary (soft cylinder) ──
            float radial = new Vector2(a.Position.X, a.Position.Z).Length();
            if (radial > _boundsRadius - 4f)
            {
                float push = (radial - (_boundsRadius - 4f)) / 4f;
                Vector3 inward = new(-a.Position.X, 0, -a.Position.Z);
                accel += Vector3.Normalize(inward) * push * 3.5f;
            }
            if (a.Position.Y > -1.2f) accel += new Vector3(0, -2.4f, 0);
            if (a.Position.Y < -_depth + 0.8f) accel += new Vector3(0, 2.4f, 0);

            // ── Curiosity toward the viewer (calm moments only) ──
            if (def.Curiosity > 0.02f && music.Level < 0.28f)
            {
                Vector3 toCam = _cameraPos - a.Position;
                float camDist = toCam.Length();
                if (camDist < 11f && camDist > 3f)
                {
                    float wanderBias = MathF.Sin(_time * 0.13f + a.Seed * 3.3f) * 0.5f + 0.5f;
                    accel += Vector3.Normalize(toCam) * def.Curiosity * 0.9f * wanderBias;
                }
            }

            // ── Music lifts activity; fear impulses were applied directly ──
            float targetSpeed = def.Speed * a.SpeedMul * restFactor * music.Activity;
            Vector3 desired = a.Velocity + accel * dt;
            float speed = desired.Length();
            if (speed > targetSpeed * 1.35f) desired *= targetSpeed * 1.35f / speed;
            if (speed < targetSpeed * 0.35f)
                desired += Vector3.Normalize(desired + new Vector3(0.001f, 0, 0.001f)) * dt * 1.5f;

            // Limit turn rate for believable inertia.
            float maxTurn = def.TurnRate * dt * 2.2f;
            Vector3 oldDir = Vector3.Normalize(a.Velocity);
            Vector3 newDir = Vector3.Normalize(desired);
            float dot = Math.Clamp(Vector3.Dot(oldDir, newDir), -1f, 1f);
            float angle = MathF.Acos(dot);
            if (angle > maxTurn)
            {
                Vector3 axis = Vector3.Cross(oldDir, newDir);
                if (axis.LengthSquared() > 1e-6f)
                    newDir = Vector3.Normalize(RotateAround(oldDir, Vector3.Normalize(axis), maxTurn));
                else
                    newDir = oldDir;
            }
            a.Velocity = newDir * Math.Clamp(desired.Length(), targetSpeed * 0.3f, targetSpeed * 1.6f);
            a.Position += a.Velocity * dt;

            // Phase advances with real speed: tail beat follows motion.
            float speedFrac = a.Velocity.Length() / MathF.Max(def.Speed, 0.05f);
            a.Phase = (a.Phase + dt * (0.6f + speedFrac * 1.9f)) % 1f;
        }
    }

    private void ConsumeImpulses(float reactionStrength, float beat)
    {
        if (_impulses.Count == 0) return;
        foreach (var impulse in _impulses)
        {
            for (int i = 0; i < _agentCount; i++)
            {
                ref var a = ref _agents[i];
                var def = _species[a.Species];
                Vector3 delta = a.Position - impulse.Point;
                float d = delta.Length();
                if (d > impulse.Radius) continue;
                float falloff = 1f - d / impulse.Radius;
                float fear = def.Fear * reactionStrength * falloff;
                if (fear > 0.01f)
                {
                    Vector3 away = Vector3.Normalize(delta + new Vector3(0.02f, 0.05f, 0.02f));
                    a.Velocity += away * fear * 4.5f;
                    a.RestTimer = 0f; // wake up startled fish
                }
            }
        }
        _impulses.Clear();
    }

    // ── Spatial hash ─────────────────────────────────────────────────────────
    private void BuildGrid()
    {
        Array.Fill(_cellHeads, -1);
        for (int i = 0; i < _agentCount; i++)
        {
            int cell = CellOf(_agents[i].Position);
            _agentNext[i] = _cellHeads[cell];
            _cellHeads[cell] = i;
        }
    }

    private int CellIndexOf(int x, int y, int z)
    {
        x = Math.Clamp(x, 0, _gridDim - 1);
        y = Math.Clamp(y, 0, 11);
        z = Math.Clamp(z, 0, _gridDim - 1);
        return (y * _gridDim + z) * _gridDim + x;
    }

    private int CellOf(Vector3 p)
    {
        int dim = _gridDim;
        int x = Math.Clamp((int)((p.X + _boundsRadius) / CellSize), 0, dim - 1);
        int z = Math.Clamp((int)((p.Z + _boundsRadius) / CellSize), 0, dim - 1);
        int y = Math.Clamp((int)((-p.Y) / (_depth + 1f) * 12f), 0, 11);
        return (y * dim + z) * dim + x;
    }

    private static Vector3 RotateAround(Vector3 v, Vector3 axis, float angle)
    {
        var q = Quaternion.CreateFromAxisAngle(axis, angle);
        return Vector3.Transform(v, q);
    }

    /// <summary>Fills GPU instance buffers. Jelly-form agents route to the translucent jelly pipeline.</summary>
    public int FillInstances(Span<FishInstance> fish, Span<JellyInstance> jelly, out int jellyCount)
    {
        int nf = 0, nj = 0;
        for (int i = 0; i < _agentCount; i++)
        {
            ref var a = ref _agents[i];
            ref readonly var sp = ref _gpuSpecies[a.Species];

            var vel = a.Velocity;
            float speed = vel.Length();
            var dir = speed > 0.01f ? vel / speed : new Vector3(1, 0, 0);
            float yaw = MathF.Atan2(-dir.Z, dir.X);
            float pitch = Math.Clamp(MathF.Asin(Math.Clamp(dir.Y, -1f, 1f)), -0.7f, 0.7f);

            bool isJelly = sp.Extras.W > 1.5f && sp.Extras.W < 2.5f; // SpeciesForm.Jelly
            if (isJelly && nj < jelly.Length)
            {
                jelly[nj++] = new JellyInstance
                {
                    PosScale = new Vector4(a.Position, sp.PosScale.W),
                    OrientPhase = new Vector4(yaw, a.Phase, 0.5f + sp.ColorB.W * 0.4f, sp.ColorB.W),
                    Color = new Vector4(sp.ColorB.X, sp.ColorB.Y, sp.ColorB.Z, 0.85f)
                };
                continue;
            }

            if (nf >= fish.Length) continue;

            // Bank into turns: lateral velocity relative to heading.
            Vector3 forward = new(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
            float lateral = Vector3.Dot(vel, new Vector3(-forward.Z, 0, forward.X));
            float roll = Math.Clamp(-lateral * 0.35f, -0.5f, 0.5f);

            fish[nf] = sp;
            fish[nf].PosScale = new Vector4(a.Position, sp.PosScale.W);
            fish[nf].Orient = new Vector4(yaw, pitch, roll, a.Phase);
            nf++;
        }
        jellyCount = nj;
        return nf;
    }

    /// <summary>Writes soft shadows for a subset of larger fish onto the floor.</summary>
    public int FillShadows(Span<ShadowFish> shadows)
    {
        int count = 0;
        for (int i = 0; i < _agentCount && count < shadows.Length; i++)
        {
            ref var a = ref _agents[i];
            var def = _species[a.Species];
            float strength = Math.Clamp(def.SizeM * 1.6f - 0.3f, 0f, 1.4f) * 0.5f;
            if (strength < 0.06f) continue;
            shadows[count++] = new ShadowFish
            {
                Data = new Vector4(a.Position.X, a.Position.Z, MathF.Max(0.3f, def.SizeM * 0.8f), strength)
            };
        }
        return count;
    }
}
