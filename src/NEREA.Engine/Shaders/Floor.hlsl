// NEREA - Floor pass: sandbed with procedural caustics, soft fish shadows,
// ripple bands and depth fog.

#include "Common.hlsli"

struct VSIn
{
    float3 pos : POSITION;
};

struct VSOut
{
    float4 pos : SV_POSITION;
    float3 world : WORLDPOS;
};

VSOut VSFloor(VSIn input)
{
    VSOut o;
    float3 world = float3(input.pos.x, FogParams.y + 0.01, input.pos.z);
    o.world = world;
    o.pos = mul(float4(world, 1.0), ViewProjection);
    return o;
}

float4 PSFloor(VSOut input) : SV_Target
{
    float t = TimeMusic.x;
    float bass = TimeMusic.z;
    float3 cam = CameraPos.xyz;

    float2 xz = input.world.xz;
    float dist = length(xz - cam.xz);

    // Sand: color + grain + soft dune shading.
    float grain = ValueNoise(xz * 2.3) * 0.16 + ValueNoise(xz * 7.1) * 0.07;
    float dune = ValueNoise(xz * 0.22);
    float dune2 = ValueNoise(xz * 0.22 + float2(0.18, 0.11));
    float3 n = normalize(float3((dune2 - dune) * 2.4, 1.0, (ValueNoise(xz * 0.22 + float2(0.11, 0.21)) - dune) * 2.4));
    float ndl = saturate(dot(n, normalize(LightDir.xyz))) * 0.55 + 0.45;

    float3 col = FloorParams.rgb * (0.55 + grain) * ndl;

    // Caustics dance on the floor, breathing with the bass.
    int oct = (int)clamp(QualityMisc.w, 1.0, 4.0);
    float caustic = CausticPattern(xz, t, FloorParams.w, oct);
    caustic *= EnvMisc.w * (1.0 + bass * 0.18);
    col += LightColor.rgb * LightColor.w * caustic * 0.85;

    // Soft contact shadows cast by larger swimmers.
    float shadow = 0.0;
    [unroll]
    for (int i = 0; i < 48; i++)
    {
        float4 s = ShadowBuffer[i].Data;
        if (s.w <= 0.001) continue;
        float d = length(xz - s.xy);
        shadow = max(shadow, smoothstep(s.z * 2.1, s.z * 0.5, d) * s.w);
    }
    shadow *= QualityMisc.z;
    col *= 1.0 - shadow * 0.55;

    // Ripple light bands from pointer interaction.
    float ripple = RippleBands(xz, dist);
    col += LightColor.rgb * ripple * 0.22;

    // Depth fog toward the water color.
    float camDist = length(input.world - cam);
    col = WaterFog(camDist, saturate(-input.world.y / max(FogParams.z, 1.0)), col);

    return float4(max(col, 0.0), 1.0);
}
