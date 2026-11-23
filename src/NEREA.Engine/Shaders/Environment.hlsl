// NEREA - Environment pass: the water volume itself.
// Fullscreen background: depth gradient, volumetric god rays, sun glow,
// near-surface shimmer. Music breathes through every layer.

#include "Common.hlsli"

struct VSOut
{
    float4 pos : SV_POSITION;
    float3 ray : RAY;
};

VSOut VSEnvironment(uint id : SV_VertexID)
{
    VSOut o;
    float2 uv = float2((id << 1) & 2, id & 2);
    o.pos = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 1.0, 1.0);
    float4 far = mul(float4(o.pos.xy, 1.0, 1.0), InvViewProjection);
    o.ray = far.xyz / far.w - CameraPos.xyz;
    return o;
}

float4 PSEnvironment(VSOut input) : SV_Target
{
    float3 ray = normalize(input.ray);
    float3 cam = CameraPos.xyz;
    float t = TimeMusic.x;
    float level = TimeMusic.y;
    float bass = TimeMusic.z;

    float3 lightDir = normalize(LightDir.xyz);
    float3 lightCol = LightColor.rgb * LightColor.w;

    // Clamp the ray against the water surface (y = 0).
    float travel = 260.0;
    if (ray.y < -0.002)
    {
        travel = min(travel, (FogParams.y - cam.y) / ray.y);
    }
    else if (ray.y > 0.002 && cam.y < 0.0)
    {
        float surfDist = min((0.0 - cam.y) / ray.y, 240.0);
        travel = min(travel, surfDist);
    }

    float3 hit = cam + ray * travel;
    float depthFrac = saturate(-hit.y / max(FogParams.z, 1.0));

    // Base water gradient.
    float3 col = lerp(WaterShallow.rgb, WaterDeep.rgb, saturate(depthFrac * 1.15 + 0.06));

    // Volumetric god rays: layered shafts sampled along the view ray.
    float rays = 0.0;
    const int SAMPLES = 3;
    [unroll]
    for (int i = 0; i < SAMPLES; i++)
    {
        float fi = (i + 1.0) / SAMPLES;
        float3 sp = cam + ray * (travel * fi);
        float side = dot(normalize(sp.xz + 0.001), lightDir.xz);
        float shaft = pow(max(0.0, sin(side * 7.0 + t * 0.14 + sin(sp.x * 0.24 + t * 0.05) * 1.4)), 3.0);
        shaft += pow(max(0.0, sin(side * 13.0 - t * 0.09 + sp.z * 0.18)), 4.0) * 0.45;
        float fade = exp(-max(0.0, -sp.y) * 0.14);
        rays += shaft * fade;
    }
    rays /= SAMPLES;

    float rayIntensity = FogParams.w * (1.0 + bass * 0.22 + level * 0.1);
    col += lightCol * rays * rayIntensity * 0.16;

    // Sun glow through the surface.
    float sunGlow = pow(max(dot(ray, lightDir), 0.0), 42.0);
    float shimmer = 0.75 + 0.25 * sin(t * 1.7 + sin(t * 0.9) * 2.0);
    col += lightCol * sunGlow * EnvMisc.z * shimmer * (1.0 + bass * 0.25) * 1.4;

    // Subtle near-surface brightness (looking up).
    float upGlow = smoothstep(-0.35, 0.05, ray.y);
    col += lerp(WaterShallow.rgb, float3(1.0, 1.0, 1.0), 0.4) * upGlow * EnvMisc.z * 0.35 * lightCol.g;

    return float4(max(col, 0.0), 1.0);
}
