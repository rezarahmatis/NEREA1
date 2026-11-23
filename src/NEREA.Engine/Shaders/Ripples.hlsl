// NEREA - Ripple pass: expanding interaction rings skimming the floor.

#include "Common.hlsli"

struct VSIn
{
    float2 corner : POSITION;
};

struct VSOut
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    nointerpolation float4 params : COLOR0; // age, strength, unused, unused
};

VSOut VSRipples(VSIn input, uint inst : SV_InstanceID)
{
    // Instance buffer reused: PosSize=(x,z,scale,unused), Params=(age,strength,...)
    ParticleInstance r = ParticleBuffer[inst];
    float radius = 0.25 + r.PosSize.z;
    float3 world = float3(r.PosSize.x + input.corner.x * radius, FogParams.y + 0.03, r.PosSize.y + input.corner.y * radius);

    VSOut o;
    o.pos = mul(float4(world, 1.0), ViewProjection);
    o.uv = input.corner;
    o.params = float4(r.Params.x, r.Params.y, 0, 0);
    return o;
}

float4 PSRipples(VSOut input) : SV_Target
{
    float d = length(input.uv);
    float age = input.params.x;
    float strength = input.params.y;

    float ring = smoothstep(0.30, 0.02, abs(d - 0.82)) * (1.0 - age) * strength;
    float inner = (1.0 - smoothstep(0.0, 0.82, d)) * 0.10 * (1.0 - age) * strength;
    float a = saturate(ring + inner);
    float3 col = LightColor.rgb * LightColor.w * a * 0.5;
    return float4(max(col, 0.0), 1.0);
}
