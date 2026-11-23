namespace NEREA.Core.Models;

public enum QualityLevel
{
    Battery,
    Performance,
    Balanced,
    High,
    Ultra
}

public enum FpsLimit
{
    MatchDisplay,
    Fps120,
    Fps60,
    Fps30
}

/// <summary>Concrete engine knobs derived from a <see cref="QualityLevel"/> preset.</summary>
public sealed class QualitySettings
{
    public float RenderScale { get; set; } = 1.0f;
    public int MaxFish { get; set; } = 512;
    public int MoteCount { get; set; } = 1600;
    public float VegetationScale { get; set; } = 1.0f;
    public float BubbleScale { get; set; } = 1.0f;
    public int CausticsOctaves { get; set; } = 3;
    public bool Bloom { get; set; } = true;
    public bool Shadows { get; set; } = true;
    public bool GodRays { get; set; } = true;
    public int SimHz { get; set; } = 60;
    public int TargetFps { get; set; } = 0; // 0 = match display
    public float BloomStrength { get; set; } = 1.0f;
}

public static class QualityPresets
{
    public static QualitySettings For(QualityLevel level) => level switch
    {
        QualityLevel.Battery => new QualitySettings
        {
            RenderScale = 0.75f, MaxFish = 96, MoteCount = 420, VegetationScale = 0.6f, BubbleScale = 0.5f,
            CausticsOctaves = 1, Bloom = false, Shadows = false, GodRays = false, SimHz = 30, TargetFps = 30, BloomStrength = 0f
        },
        QualityLevel.Performance => new QualitySettings
        {
            RenderScale = 0.85f, MaxFish = 192, MoteCount = 800, VegetationScale = 0.75f, BubbleScale = 0.7f,
            CausticsOctaves = 2, Bloom = true, Shadows = false, GodRays = true, SimHz = 30, TargetFps = 30, BloomStrength = 0.8f
        },
        QualityLevel.Balanced => new QualitySettings
        {
            RenderScale = 1.0f, MaxFish = 384, MoteCount = 1500, VegetationScale = 1.0f, BubbleScale = 1.0f,
            CausticsOctaves = 3, Bloom = true, Shadows = true, GodRays = true, SimHz = 60, TargetFps = 0, BloomStrength = 1.0f
        },
        QualityLevel.High => new QualitySettings
        {
            RenderScale = 1.25f, MaxFish = 768, MoteCount = 2600, VegetationScale = 1.3f, BubbleScale = 1.2f,
            CausticsOctaves = 3, Bloom = true, Shadows = true, GodRays = true, SimHz = 60, TargetFps = 0, BloomStrength = 1.0f
        },
        QualityLevel.Ultra => new QualitySettings
        {
            RenderScale = 1.5f, MaxFish = 1536, MoteCount = 4200, VegetationScale = 1.6f, BubbleScale = 1.5f,
            CausticsOctaves = 4, Bloom = true, Shadows = true, GodRays = true, SimHz = 60, TargetFps = 0, BloomStrength = 1.15f
        },
        _ => For(QualityLevel.Balanced)
    };
}
