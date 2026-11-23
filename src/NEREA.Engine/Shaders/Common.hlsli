// NEREA - Common shader definitions. Included by every pass.
#pragma pack_matrix(row_major)

// Mirrors NEREA.Engine.Rendering.PerFrameConstants (layout must match exactly).
cbuffer PerFrame : register(b0)
{
    float4x4 ViewProjection;
    float4x4 InvViewProjection;
    float4   CameraPos;      // xyz camera position
    float4   CameraRight;    // xyz camera right vector
    float4   CameraUp;       // xyz camera up vector
    float4   TimeMusic;      // time, music level, music bass, music mid
    float4   MusicMisc;      // music treble, beat pulse, glowBoost, ecosystem activity
    float4   LightDir;       // xyz normalized direction toward the light
    float4   LightColor;     // rgb, intensity
    float4   WaterShallow;   // rgb
    float4   WaterDeep;      // rgb
    float4   FogParams;      // density, floorY (negative), waterDepth, godRays
    float4   FloorParams;    // sand color rgb, caustics scale
    float4   EnvMisc;        // vignette, grain, shimmer, caustics strength
    float4   QualityMisc;    // particleFade, vegSway, shadowStrength, causticOctaves
    float4   Ripples0;       // x, z, age (0..1), strength
    float4   Ripples1;
    float4   Ripples2;
    float4   Ripples3;
    float4   Ripples4;
    float4   Ripples5;
};

// ── Instance data ────────────────────────────────────────────────────────────
struct FishInstance
{
    float4 PosScale;   // xyz world position, w length (meters)
    float4 Orient;     // yaw, pitch, roll, swim phase
    float4 ColorA;     // dorsal color rgb, pattern id
    float4 ColorB;     // belly / bright color rgb, glow
    float4 ShapeA;     // bodyHeight, bodyWidth, headBlunt, tailFork
    float4 ShapeB;     // dorsalHeight, tailLength, pectoralSize, stripes
    float4 Extras;     // fin color rgb, form id (0 fish, 1 drifter, 2 ray)
};
StructuredBuffer<FishInstance> FishBuffer : register(t0);

struct ParticleInstance
{
    float4 PosSize;    // xyz, size
    float4 Params;     // alpha, seed, type (0 mote, 1 bubble), twinkle
};
StructuredBuffer<ParticleInstance> ParticleBuffer : register(t1);

struct VegInstance
{
    float4 PosHeight;  // xyz, height
    float4 Params;     // phase, sway, width, hueMix
};
StructuredBuffer<VegInstance> VegBuffer : register(t2);

struct ShadowFish
{
    float4 Data;       // x, z, radius, strength
};
StructuredBuffer<ShadowFish> ShadowBuffer : register(t3);

struct JellyInstance
{
    float4 PosScale;   // xyz, scale
    float4 OrientPhase;// yaw, pulse phase, pulse rate, glow
    float4 Color;      // rgb, alpha
};
StructuredBuffer<JellyInstance> JellyBuffer : register(t4);

// ── Utility ──────────────────────────────────────────────────────────────────
float Hash11(float n) { return frac(sin(n) * 43758.5453123); }

float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = Hash21(i);
    float b = Hash21(i + float2(1.0, 0.0));
    float c = Hash21(i + float2(0.0, 1.0));
    float d = Hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Procedural underwater caustics: layered interference, sharp crests.
float CausticPattern(float2 p, float t, float scale, int octaves)
{
    float acc = 0.0;
    float amp = 1.0;
    float freq = scale;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        if (i >= octaves) break;
        float2 q = p * freq + float2(t * (0.14 + 0.06 * i), -t * (0.11 + 0.05 * i));
        float a = sin(q.x) * sin(q.y);
        float b = sin(q.x * 1.7 + 1.3) * sin(q.y * 1.3 - 0.7);
        acc += abs(a + b * 0.55) * amp;
        amp *= 0.62;
        freq *= 2.13;
    }
    return pow(saturate(acc * 0.60), 2.4);
}

// Depth-graded fog toward the water color.
float3 WaterFog(float dist, float depthFraction, float3 base)
{
    float3 target = lerp(WaterShallow.rgb, WaterDeep.rgb, saturate(depthFraction));
    float f = exp(-FogParams.x * dist * 1.25);
    return lerp(target, base, saturate(f));
}

float3 ACESFilm(float3 x)
{
    const float a = 2.51, b = 0.03, c = 2.43, d = 0.59, e = 0.14;
    return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
}

float2 RippleAt(int idx)
{
    float4 r = idx == 0 ? Ripples0 : idx == 1 ? Ripples1 : idx == 2 ? Ripples2
             : idx == 3 ? Ripples3 : idx == 4 ? Ripples4 : Ripples5;
    return r.xy;
}

// Accumulated floor ripple influence (light band + caustic perturbation weight).
float RippleBands(float2 worldXz, float dist)
{
    float bands = 0.0;
    [unroll]
    for (int i = 0; i < 6; i++)
    {
        float4 r = i == 0 ? Ripples0 : i == 1 ? Ripples1 : i == 2 ? Ripples2
                 : i == 3 ? Ripples3 : i == 4 ? Ripples4 : Ripples5;
        if (r.w <= 0.001) continue;
        float d = length(worldXz - r.xy);
        float radius = 0.2 + r.z * 5.0;
        float ring = smoothstep(0.55, 0.0, abs(d - radius)) * r.w * (1.0 - r.z);
        bands += ring;
    }
    return bands;
}
