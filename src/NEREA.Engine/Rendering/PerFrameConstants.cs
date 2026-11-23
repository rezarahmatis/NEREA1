using System.Numerics;
using System.Runtime.InteropServices;

namespace NEREA.Engine.Rendering;

/// <summary>
/// Per-frame GPU constants. Layout must match cbuffer PerFrame in Common.hlsli.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PerFrameConstants
{
    public Matrix4x4 ViewProjection;    // 0
    public Matrix4x4 InvViewProjection; // 64
    public Vector4 CameraPos;           // 128
    public Vector4 CameraRight;         // 144
    public Vector4 CameraUp;            // 160
    public Vector4 TimeMusic;           // 176: time, level, bass, mid
    public Vector4 MusicMisc;           // 192: treble, beat, glowBoost, activity
    public Vector4 LightDir;            // 208
    public Vector4 LightColor;          // 224: rgb, intensity
    public Vector4 WaterShallow;        // 240
    public Vector4 WaterDeep;           // 256
    public Vector4 FogParams;           // 272: density, floorY, waterDepth, godRays
    public Vector4 FloorParams;         // 288: sand rgb, caustics scale
    public Vector4 EnvMisc;             // 304: vignette, grain, shimmer, causticsStrength
    public Vector4 QualityMisc;         // 320: particleFade, vegSway, shadowStrength, causticOctaves
    public Vector4 Ripples0;            // 336
    public Vector4 Ripples1;            // 352
    public Vector4 Ripples2;            // 368
    public Vector4 Ripples3;            // 384
    public Vector4 Ripples4;            // 400
    public Vector4 Ripples5;            // 416

    public const int SizeInBytes = 432;
}

[StructLayout(LayoutKind.Sequential)]
public struct FishInstance
{
    public Vector4 PosScale;    // xyz, length
    public Vector4 Orient;      // yaw, pitch, roll, phase
    public Vector4 ColorA;      // dorsal rgb, pattern
    public Vector4 ColorB;      // belly rgb, glow
    public Vector4 ShapeA;      // bodyH, bodyW, headBlunt, tailFork
    public Vector4 ShapeB;      // dorsalH, tailLen, pectoral, stripes
    public Vector4 Extras;      // fin rgb, form id

    public const int SizeInBytes = 112;
}

[StructLayout(LayoutKind.Sequential)]
public struct ParticleInstance
{
    public Vector4 PosSize;
    public Vector4 Params;

    public const int SizeInBytes = 32;
}

[StructLayout(LayoutKind.Sequential)]
public struct VegInstance
{
    public Vector4 PosHeight;
    public Vector4 Params;

    public const int SizeInBytes = 32;
}

[StructLayout(LayoutKind.Sequential)]
public struct JellyInstance
{
    public Vector4 PosScale;
    public Vector4 OrientPhase;
    public Vector4 Color;

    public const int SizeInBytes = 48;
}

[StructLayout(LayoutKind.Sequential)]
public struct ShadowFish
{
    public Vector4 Data;    // x, z, radius, strength

    public const int SizeInBytes = 16;
}

/// <summary>Static capacity limits. Population scales inside these bounds.</summary>
public static class EngineLimits
{
    public const int MaxFish = 2048;
    public const int MaxParticles = 4096;
    public const int MaxVegetation = 1600;
    public const int MaxJelly = 128;
    public const int MaxShadows = 48;
    public const int MaxRipples = 12;
    public const int FrameCount = 2;
}
