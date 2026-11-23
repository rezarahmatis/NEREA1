namespace NEREA.Core.Models;

public enum SpeciesForm
{
    /// <summary>Standard streamlined swimmer.</summary>
    Fish,
    /// <summary>Flattened winged swimmer (rays and skates).</summary>
    Ray,
    /// <summary>Translucent pulsing invertebrate (jellyfish, squid mantle).</summary>
    Jelly,
    /// <summary>Slow hovering swimmer with reduced undulation (turtles, seahorses, shrimp-like).</summary>
    Drifter
}

public enum BodyPattern
{
    None,
    /// <summary>Longitudinal stripes along the body.</summary>
    Stripes,
    /// <summary>Vertical bars.</summary>
    Bars,
    /// <summary>Rounded spots.</summary>
    Spots,
    /// <summary>Strong dorsal/belly two-tone split.</summary>
    TwoTone,
    /// <summary>Koi-like flowing patches.</summary>
    Koi,
    /// <summary>Photophore dots along the flank.</summary>
    GlowDots
}

/// <summary>
/// Data-driven definition of one species. Rendering reads the visual fields,
/// the simulation reads the behavior fields - adding a species never touches the renderer.
/// </summary>
public sealed class SpeciesDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Category { get; init; } = "Freshwater";
    public SpeciesForm Form { get; init; } = SpeciesForm.Fish;
    public BodyPattern Pattern { get; init; } = BodyPattern.None;

    /// <summary>Typical adult length in meters.</summary>
    public float SizeM { get; init; } = 0.18f;
    /// <summary>Preferred cruise speed, meters per second.</summary>
    public float Speed { get; init; } = 0.55f;
    public float TurnRate { get; init; } = 1.6f;

    // Behavior (0..1 scales)
    public float School { get; init; } = 0f;
    public float Separation { get; init; } = 0.55f;
    public float Fear { get; init; } = 0.5f;
    public float Curiosity { get; init; } = 0.15f;
    public float Restfulness { get; init; } = 0.2f;
    /// <summary>Preferred depth band as fractions of world depth (0 = surface, 1 = floor).</summary>
    public float DepthLo { get; init; } = 0.15f;
    public float DepthHi { get; init; } = 0.75f;

    // Visual
    public string ColorDeep { get; init; } = "#7A97A0";
    public string ColorBright { get; init; } = "#E8F1EE";
    public string ColorFin { get; init; } = "#B7C9C2";
    public int StripeCount { get; init; } = 0;
    /// <summary>Emissive strength (0 = none). Deep-sea and bioluminescent species glow.</summary>
    public float Glow { get; init; } = 0f;

    // Proportions
    public float BodyHeight { get; init; } = 1.0f;
    public float BodyWidth { get; init; } = 1.0f;
    public float HeadBlunt { get; init; } = 0.5f;
    public float TailFork { get; init; } = 0.5f;
    public float DorsalHeight { get; init; } = 0.16f;
    public float TailLength { get; init; } = 0.28f;
    public float PectoralSize { get; init; } = 0.5f;
}
