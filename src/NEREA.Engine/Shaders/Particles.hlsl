// NEREA - Particle pass: drifting motes (additive) and bubbles (alpha).
// Billboarded instanced quads; the simulation animates positions on the CPU.

#include "Common.hlsli"

struct VSIn
{
    float2 corner : POSITION;   // -1..1 quad corner
};

struct VSOut
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    nointerpolation float4 tint : COLOR0;  // alpha, seed, type, twinkle
};

VSOut VSParticles(VSIn input, uint inst : SV_InstanceID)
{
    ParticleInstance p = ParticleBuffer[inst];
    float3 world = p.PosSize.xyz + CameraRight.xyz * input.corner.x * p.PosSize.w
                               + CameraUp.xyz * input.corner.y * p.PosSize.w;

    VSOut o;
    o.pos = mul(float4(world, 1.0), ViewProjection);
    o.uv = input.corner;
    o.tint = p.Params;   // x alpha, y seed, z type, w twinkle
    return o;
}

float4 PSParticles(VSOut input) : SV_Target
{
    float r = length(input.uv);
    float type = input.tint.z;
    float seed = input.tint.y;
    float t = TimeMusic.x;

    if (type < 0.5)
    {
        // Drifting mote: soft round speck with slow twinkle.
        float tw = 0.55 + 0.45 * sin(t * (0.6 + frac(seed) * 1.2) + seed * 17.0);
        float a = smoothstep(1.0, 0.15, r) * input.tint.x * tw * input.tint.w;
        float3 col = lerp(LightColor.rgb, WaterShallow.rgb * 2.2, 0.35) * a * 0.5;
        return float4(max(col, 0.0), 1.0);
    }
    else
    {
        // Bubble: bright thin rim, faint fill, top highlight.
        float rim = smoothstep(0.62, 0.94, r) * (1.0 - smoothstep(0.96, 1.0, r));
        float fill = (1.0 - r) * 0.10;
        float2 hl = float2(-0.32, -0.38);
        float highlight = smoothstep(0.30, 0.02, length(input.uv - hl));
        float a = saturate(rim * 0.9 + fill + highlight * 0.85) * input.tint.x;
        float3 col = (LightColor.rgb * (rim * 1.4 + highlight * 1.8) + WaterShallow.rgb * fill * 3.0) * a;
        return float4(max(col, 0.0), 1.0);
    }
}
