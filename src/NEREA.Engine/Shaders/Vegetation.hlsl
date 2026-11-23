// NEREA - Vegetation pass: swaying grass, kelp ribbons and coral clusters.
// One crossed-ribbon mesh; height, sway and color come from instance data.

#include "Common.hlsli"

struct VSIn
{
    float3 pos : POSITION;   // x: ±width, y: 0..1 along the blade
    float2 uv : TEXCOORD0;   // u along height, v across
};

struct VSOut
{
    float4 pos : SV_POSITION;
    float3 world : WORLDPOS;
    float2 uv : TEXCOORD0;
    nointerpolation float2 colors : COLOR0; // hueMix, sway
};

VSOut VSVegetation(VSIn input, uint inst : SV_InstanceID)
{
    VegInstance veg = VegBuffer[inst];
    float h = veg.PosHeight.w;
    float phase = veg.Params.x;
    float sway = veg.Params.y;
    float width = veg.Params.z;

    float u = input.uv.x;
    float3 local = input.pos;
    local.x *= width * lerp(1.0, 0.15, u);   // blades taper toward the tip

    // Sway: growing bend along the blade; music (mid) livens it.
    float bend = sin(TimeMusic.x * (0.55 + frac(phase) * 0.3) + phase * 6.2831853 + u * 1.4)
               + 0.35 * sin(TimeMusic.x * 1.1 + phase * 11.0);
    bend *= (0.35 + sway) * (0.45 + TimeMusic.w * 0.5) * QualityMisc.y;
    float bendAmt = pow(u, 1.6);
    local.x += bend * bendAmt * 0.55;
    local.z += cos(phase * 6.2831853 + u * 1.2 + TimeMusic.x * 0.5) * bendAmt * 0.22;

    float3 world = local * h + veg.PosHeight.xyz;

    VSOut o;
    o.pos = mul(float4(world, 1.0), ViewProjection);
    o.world = world;
    o.uv = float2(u, input.uv.y);
    o.colors = float2(veg.Params.w, sway);
    return o;
}

float4 PSVegetation(VSOut input) : SV_Target
{
    float u = input.uv.x;
    float3 cam = CameraPos.xyz;
    float3 V = normalize(cam - input.world);

    float hue = input.colors.x;
    float3 base = lerp(VegColorA.rgb * 0.55, VegColorB.rgb, hue * 0.5 + u * 0.5);

    // Base darkens toward the root, tips catch the light.
    float ndl = saturate(dot(float3(0, 1, 0), normalize(LightDir.xyz)));
    float3 col = base * (0.5 + 0.5 * u) * LightColor.w * (0.7 + 0.3 * ndl);

    // Backlight glow through thin blades.
    float back = pow(saturate(dot(-V, normalize(LightDir.xyz))), 2.0);
    col += VegColorB.rgb * back * u * 0.35 * LightColor.w;

    float camDist = length(input.world - cam);
    col = WaterFog(camDist, saturate(-input.world.y / max(FogParams.z, 1.0)), col);

    return float4(max(col, 0.0), 1.0);
}
