// NEREA - Jellyfish pass: translucent pulsing bells with trailing ribbons.
// Additive blending, depth-tested, no depth write.

#include "Common.hlsli"

struct VSIn
{
    float3 pos : POSITION;   // y up; bell: hemisphere radius 1; tentacles: y 0..-1.4
    float2 uv : TEXCOORD0;   // bell: (lat 0 top .. 1 rim); tentacle: (u along, ribbon id)
    float part : TEXCOORD1;  // 0 bell, 1 tentacle
};

struct VSOut
{
    float4 pos : SV_POSITION;
    float3 world : WORLDPOS;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float part : TEXCOORD1;
    nointerpolation float4 color : COLOR0;   // rgb, alpha
    nointerpolation float glow : TEXCOORD2;
};

VSOut VSJelly(VSIn input, uint inst : SV_InstanceID)
{
    JellyInstance j = JellyBuffer[inst];
    float scale = j.PosScale.w;
    float pulse = j.OrientPhase.y;
    float rate = j.OrientPhase.z;

    float3 local = input.pos;
    if (input.part < 0.5)
    {
        // Bell: pulsing radial contraction traveling from apex to rim.
        float lat = input.uv.x;                    // 0 apex .. 1 rim
        float ring = sqrt(saturate(local.x * local.x + local.z * local.z));
        float squeeze = 1.0 + 0.14 * sin(pulse * 6.2831853 - lat * 2.4) * saturate(ring * 1.4);
        local.xz *= squeeze;
        local.y *= 1.0 - 0.10 * sin(pulse * 6.2831853 - lat * 2.4);
        // Drift upward rhythm with each pulse.
        local.y += sin(pulse * 6.2831853) * 0.06;
    }
    else
    {
        // Tentacles trail with a lazy wave.
        float u = input.uv.x;
        float ribbon = input.uv.y;
        float sway = sin(u * 3.2 + pulse * 4.4 + ribbon * 1.7 + TimeMusic.x * 0.4);
        local.x += sway * u * 0.28;
        local.z += cos(u * 2.6 + pulse * 3.6 + ribbon * 2.2) * u * 0.2;
        local.y += sin(pulse * 6.2831853) * 0.06 * u;
    }

    float yaw = j.OrientPhase.x;
    float c = cos(yaw), s = sin(yaw);
    local.xz = float2(local.x * c + local.z * s, -local.x * s + local.z * c);

    float3 world = local * scale + j.PosScale.xyz;

    // Normal for the bell approximates the radial sphere direction.
    float3 n;
    if (input.part < 0.5)
    {
        float ring = sqrt(saturate(local.x * local.x + local.z * local.z));
        n = normalize(float3(local.x, -0.35 * ring, local.z));
    }
    else
    {
        n = normalize(float3(input.pos.x, 0.2, input.pos.z));
    }

    VSOut o;
    o.pos = mul(float4(world, 1.0), ViewProjection);
    o.world = world;
    o.normal = n;
    o.uv = input.uv;
    o.part = input.part;
    o.color = j.Color;
    o.glow = j.OrientPhase.w;
    return o;
}

float4 PSJelly(VSOut input) : SV_Target
{
    float3 N = normalize(input.normal);
    float3 V = normalize(CameraPos.xyz - input.world);
    float3 L = normalize(LightDir.xyz);

    float fres = pow(1.0 - saturate(abs(dot(N, V))), 2.1);
    float ndl = saturate(dot(N, L)) * 0.5 + 0.5;

    float beat = MusicMisc.y;
    float glow = input.glow * (0.8 + 0.3 * beat);

    float3 col = input.color.rgb * (0.30 + 0.5 * fres + 0.25 * ndl) * LightColor.w;
    col += input.color.rgb * glow * (0.35 + fres);
    col += LightColor.rgb * fres * 0.35 * LightColor.w;

    float alpha = saturate((0.16 + fres * 0.7 + glow * 0.15) * input.color.a);
    col *= alpha;

    float camDist = length(input.world - CameraPos.xyz);
    float fog = exp(-FogParams.x * camDist * 1.1);
    col *= fog;

    return float4(max(col, 0.0), 1.0);
}
