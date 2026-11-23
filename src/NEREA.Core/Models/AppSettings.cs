namespace NEREA.Core.Models;

public enum AppTheme
{
    Dark,
    Light
}

public enum LaunchBehavior
{
    /// <summary>Open with the last used world and population.</summary>
    RestoreSession,
    /// <summary>Open with the default curated experience.</summary>
    Default
}

/// <summary>All persisted user preferences. Stored as JSON in the local app-data folder.</summary>
public sealed class AppSettings
{
    // Audio
    public float Volume { get; set; } = 0.8f;
    public bool Muted { get; set; }
    public string? OutputDeviceId { get; set; }
    public bool NormalizeLoudness { get; set; }

    // Visual
    public QualityLevel Quality { get; set; } = QualityLevel.Balanced;
    public FpsLimit FpsLimit { get; set; } = FpsLimit.MatchDisplay;
    public float ParticleDensity { get; set; } = 1.0f;
    public bool Reflections { get; set; } = true;      // god rays / light shafts
    public bool Shadows { get; set; } = true;          // soft ground shading
    public int WaterQuality { get; set; } = 3;         // caustics octaves 1..4
    public float FishPopulation { get; set; } = 1.0f;  // population multiplier

    // Interaction
    public float MouseSensitivity { get; set; } = 1.0f;
    public float RippleStrength { get; set; } = 1.0f;
    public float FishReaction { get; set; } = 1.0f;

    // Performance
    public bool AdaptiveQuality { get; set; } = true;
    public bool BatteryOptimization { get; set; }
    public bool PauseRenderWhenMinimized { get; set; } = true;

    // Application
    public LaunchBehavior LaunchBehavior { get; set; } = LaunchBehavior.RestoreSession;
    public bool MinimizeToTray { get; set; }
    public bool StartWithWindows { get; set; }
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public bool ReducedMotion { get; set; }
    public bool ReduceTransparency { get; set; }
    public bool StartInAmbientMode { get; set; }

    // Last session
    public string LastWorldId { get; set; } = "moonlit-pond";
    public List<SpeciesCount> LastSpecies { get; set; } = new();
    public string? LastPlaylistId { get; set; }
    public List<string> SessionQueue { get; set; } = new();
    public int SessionQueueIndex { get; set; } = -1;
    public double SessionPositionSeconds { get; set; }
    public bool SessionWasPlaying { get; set; }
    public string? LastPresetId { get; set; }
}
