namespace NEREA.Core.Models;

public enum VegType
{
    None,
    /// <summary>Short swaying grass / seagrass carpet.</summary>
    Grass,
    /// <summary>Tall kelp ribbons reaching for the light.</summary>
    Kelp,
    /// <summary>Short colorful coral clusters.</summary>
    Reef
}

/// <summary>
/// Data-driven definition of one aquatic world. Everything the engine needs to
/// look, light and populate a distinct environment - adding a world never touches
/// application or rendering code.
/// </summary>
public sealed class WorldDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Category { get; init; } = "Ocean";

    // Geometry / atmosphere
    public float Depth { get; init; } = 12f;
    public float BoundsRadius { get; init; } = 26f;
    public string WaterShallow { get; init; } = "#2E7F8F";
    public string WaterDeep { get; init; } = "#03222E";
    public string FloorColor { get; init; } = "#C2B28A";

    // Lighting
    public string LightColor { get; init; } = "#BFE7E2";
    public float LightIntensity { get; init; } = 1.0f;
    public float LightYaw { get; init; } = 35f;
    public float LightPitch { get; init; } = 58f;
    public float FogDensity { get; init; } = 0.05f;
    public float GodRays { get; init; } = 0.5f;
    public float Caustics { get; init; } = 0.6f;
    public float CausticsScale { get; init; } = 0.15f;

    // Camera
    public float CameraRadius { get; init; } = 17f;
    public float CameraHeightFrac { get; init; } = 0.42f;
    public float CameraFov { get; init; } = 55f;

    // Life & set dressing
    public VegType Vegetation { get; init; } = VegType.None;
    public float VegDensity { get; init; } = 0.3f;
    public string VegColorA { get; init; } = "#3E6B4A";
    public string VegColorB { get; init; } = "#7BA35F";
    public float VegHeightLo { get; init; } = 0.8f;
    public float VegHeightHi { get; init; } = 2.4f;
    public float Motes { get; init; } = 0.5f;
    public float Bubbles { get; init; } = 0.3f;

    // Post & response
    public float Shimmer { get; init; } = 0.5f;
    public float Bloom { get; init; } = 0.35f;
    public float Grain { get; init; } = 0.45f;
    public float Vignette { get; init; } = 0.45f;
    /// <summary>Multiplier for how strongly music moves this world.</summary>
    public float MusicResponse { get; init; } = 1.0f;
    public float ShadowStrength { get; init; } = 0.5f;

    public IReadOnlyList<(string SpeciesId, int Count)> DefaultSpecies { get; init; } =
        Array.Empty<(string, int)>();
}
