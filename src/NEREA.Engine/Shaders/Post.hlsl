// NEREA - Post processing: bloom extract, separable blur, final composite.

#include "Common.hlsli"

cbuffer PostParams : register(b1)
{
    float4 Params;   // per-pass: extract (tw, th, threshold, -), blur (tw, th, dirX, dirY), composite (tw, th, bloomStrength, exposure)
};

Texture2D SceneTexture : register(t0);
Texture2D BloomTexture : register(t1);
SamplerState LinearSampler : register(s0);

struct VSPostOut
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
};

float Luminance(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

// ── Bloom extract ────────────────────────────────────────────────────────────
float4 PSBloomExtract(VSPostOut input) : SV_Target
{
    float3 c = SceneTexture.Sample(LinearSampler, input.uv).rgb;
    float threshold = Params.z;
    float l = Luminance(c);
    float knee = threshold * 0.5;
    float soft = clamp(l - threshold + knee, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee + 1e-4);
    float contrib = max(soft, l - threshold) / max(l, 1e-4);
    return float4(c * contrib, 1.0);
}

// ── Separable gaussian blur (9 taps along Params.zw direction) ───────────────
float4 PSBlur(VSPostOut input) : SV_Target
{
    float2 dir = Params.zw * Params.xy;
    float3 acc = SceneTexture.Sample(LinearSampler, input.uv).rgb * 0.2270270270;
    acc += (SceneTexture.Sample(LinearSampler, input.uv + dir * 1.3846153846).rgb
          + SceneTexture.Sample(LinearSampler, input.uv - dir * 1.3846153846).rgb) * 0.3162162162;
    acc += (SceneTexture.Sample(LinearSampler, input.uv + dir * 3.2307692308).rgb
          + SceneTexture.Sample(LinearSampler, input.uv - dir * 3.2307692308).rgb) * 0.0702702703;
    return float4(acc, 1.0);
}

// ── Composite: bloom + ACES tonemap + vignette + grain + dither ──────────────
float4 PSComposite(VSPostOut input) : SV_Target
{
    float t = TimeMusic.x;
    float3 scene = SceneTexture.Sample(LinearSampler, input.uv).rgb;
    float3 bloom = BloomTexture.Sample(LinearSampler, input.uv).rgb;

    float3 col = scene + bloom * Params.z * (1.0 + MusicMisc.z * 0.15);
    col *= Params.w;

    col = ACESFilm(col);
    col = pow(col, 1.0 / 2.2);

    // Vignette.
    float2 d = input.uv - 0.5;
    float vig = 1.0 - dot(d, d) * EnvMisc.x * 1.7;
    col *= saturate(vig);

    // Film grain.
    float g = Hash21(input.uv * float2(1621.0, 911.0) + frac(t) * 41.7) - 0.5;
    col += g * EnvMisc.y * 0.028;

    // Dither to prevent banding.
    col += (Hash21(input.uv * 977.0 + t) - 0.5) / 255.0;

    return float4(saturate(col), 1.0);
}
