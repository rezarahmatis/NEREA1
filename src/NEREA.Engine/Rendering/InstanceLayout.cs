namespace NEREA.Engine.Rendering;

/// <summary>
/// Byte offsets of every section inside the shared instance buffer. The CPU
/// writes slices into the upload ring; the frame renderer reads them by
/// address. Single source of truth for both sides.
/// </summary>
public static class InstanceLayout
{
    public const int CbStride = 512; // per-frame constants, 256-byte aligned

    public static readonly int Fish = CbStride;
    public static readonly int Particles = Fish + EngineLimits.MaxFish * FishInstance.SizeInBytes;
    public static readonly int Vegetation = Particles + EngineLimits.MaxParticles * ParticleInstance.SizeInBytes;
    public static readonly int Jelly = Vegetation + EngineLimits.MaxVegetation * VegInstance.SizeInBytes;
    public static readonly int Shadows = Jelly + EngineLimits.MaxJelly * JellyInstance.SizeInBytes;
    public static readonly int Ripples = Shadows + EngineLimits.MaxShadows * ShadowFish.SizeInBytes;
    public static readonly int TotalBytes = Ripples + EngineLimits.MaxRipples * ParticleInstance.SizeInBytes;
}
