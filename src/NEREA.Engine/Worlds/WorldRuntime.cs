using System.Numerics;
using NEREA.Core.Content;
using NEREA.Core.Models;
using NEREA.Core.Utilities;
using NEREA.Engine.Rendering;
using NEREA.Engine.Simulation;

namespace NEREA.Engine.Worlds;

/// <summary>
/// Binds a <see cref="WorldDefinition"/> to the live simulation systems and
/// fills the per-frame GPU constants. Changing worlds rebuilds populations -
/// no renderer code is involved.
/// </summary>
public sealed class WorldRuntime
{
    public WorldDefinition World { get; private set; } = WorldCatalog.Default;

    public FishSimulation Fish { get; } = new();
    public ParticleSystem Particles { get; } = new();
    public RippleSystem Ripples { get; } = new();
    public CameraRig Camera { get; } = new();
    public string AnnouncedName => World.Name;

    public float AtmosphereScale { get; set; } = 1f; // Calm 0.6 / Balanced 1 / Vivid 1.5

    private VegInstance[] _veg = new VegInstance[EngineLimits.MaxVegetation];
    private int _vegCount;

    // Parsed world colors.
    private Vector4 _shallow, _deep, _floor, _light, _vegA, _vegB;
    private Vector3 _lightDir;

    public event EventHandler<string>? WorldChanged;

    public void ApplyWorld(WorldDefinition world, IReadOnlyList<SpeciesCount> speciesCounts,
        QualitySettings quality, float fishPopulationScale, float particleDensity)
    {
        World = world;
        _shallow = ColorHex.Parse(world.WaterShallow, new Vector4(0.2f, 0.4f, 0.5f, 1f));
        _deep = ColorHex.Parse(world.WaterDeep, new Vector4(0.02f, 0.1f, 0.16f, 1f));
        _floor = ColorHex.Parse(world.FloorColor, new Vector4(0.6f, 0.55f, 0.42f, 1f));
        _light = ColorHex.Parse(world.LightColor, new Vector4(0.8f, 0.9f, 0.9f, 1f));
        _vegA = ColorHex.Parse(world.VegColorA, new Vector4(0.2f, 0.4f, 0.25f, 1f));
        _vegB = ColorHex.Parse(world.VegColorB, new Vector4(0.45f, 0.6f, 0.35f, 1f));

        float yaw = world.LightYaw * MathF.PI / 180f;
        float pitch = world.LightPitch * MathF.PI / 180f;
        _lightDir = Vector3.Normalize(new Vector3(
            MathF.Cos(pitch) * MathF.Cos(yaw),
            MathF.Sin(pitch),
            MathF.Cos(pitch) * MathF.Sin(yaw)));

        int fishBudget = Math.Max(8, (int)(quality.MaxFish * fishPopulationScale));
        Fish.Configure(world, speciesCounts, fishBudget);

        int motes = (int)(world.Motes * quality.MoteCount * particleDensity);
        Particles.Configure(world, motes, quality.BubbleScale);

        BuildVegetation(world, quality.VegetationScale);

        Ripples.SetDepth(world.Depth);
        WorldChanged?.Invoke(this, world.Name);
    }

    private void BuildVegetation(WorldDefinition world, float scale)
    {
        int count = (int)(world.VegDensity * EngineLimits.MaxVegetation * scale);
        count = Math.Min(count, EngineLimits.MaxVegetation);
        var rng = new Random(13579 + world.Id.Length);
        _vegCount = count;
        for (int i = 0; i < count; i++)
        {
            float ang = rng.NextSingle() * MathF.PI * 2f;
            float rad = (float)Math.Sqrt(rng.NextSingle()) * world.BoundsRadius * 0.94f;
            float h = world.VegHeightLo + rng.NextSingle() * (world.VegHeightHi - world.VegHeightLo);
            _veg[i] = new VegInstance
            {
                PosHeight = new Vector4(MathF.Cos(ang) * rad, -world.Depth, MathF.Sin(ang) * rad, h),
                Params = new Vector4(rng.NextSingle(), 0.4f + rng.NextSingle() * 0.6f, 0.7f + rng.NextSingle() * 0.7f, rng.NextSingle())
            };
        }
    }

    /// <summary>Fills the per-frame constants for this frame.</summary>
    public void FillConstants(ref PerFrameConstants c, float time, MusicEnergy energy, QualitySettings quality,
        bool reflections, bool shadows, Vector2 mouse, bool reducedMotion, float aspect)
    {
        float response = World.MusicResponse * AtmosphereScale;
        var eff = energy; // already scaled by the engine

        Camera.Update(time, World, mouse, reducedMotion, aspect);
        var vp = Camera.ViewProjection();
        var inv = Matrix4x4.Invert(vp, out var inverted) ? inverted : Matrix4x4.Identity;

        c.ViewProjection = vp;
        c.InvViewProjection = inv;
        c.CameraPos = new Vector4(Camera.Position, 1f);
        c.CameraRight = new Vector4(Camera.Right, 0f);
        c.CameraUp = new Vector4(Camera.Up, 0f);
        c.TimeMusic = new Vector4(time, eff.Level * response, eff.Bass * response, eff.Mid * response);
        c.MusicMisc = new Vector4(eff.Treble * response, eff.Beat * response, eff.Level * 0.5f, eff.Activity);
        c.LightDir = new Vector4(_lightDir, 0f);
        c.LightColor = new Vector4(_light.X, _light.Y, _light.Z, World.LightIntensity);
        c.WaterShallow = new Vector4(_shallow.X, _shallow.Y, _shallow.Z, 1f);
        c.WaterDeep = new Vector4(_deep.X, _deep.Y, _deep.Z, 1f);
        c.FogParams = new Vector4(World.FogDensity, -World.Depth, World.Depth, reflections ? World.GodRays : World.GodRays * 0.25f);
        c.FloorParams = new Vector4(_floor.X, _floor.Y, _floor.Z, World.CausticsScale);
        c.EnvMisc = new Vector4(World.Vignette, World.Grain, World.Shimmer, World.Caustics);
        c.QualityMisc = new Vector4(
            quality.RenderScale < 1f ? 0.8f : 1f,
            reducedMotion ? 0.4f : 1f,
            shadows ? World.ShadowStrength : 0f,
            quality.CausticsOctaves);
    }

    public int VegetationCount => _vegCount;

    public void CopyVegetation(Span<VegInstance> target) =>
        _veg.AsSpan(0, Math.Min(_vegCount, target.Length)).CopyTo(target);
}
