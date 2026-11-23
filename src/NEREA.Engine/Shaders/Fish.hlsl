// NEREA - Fish pass: instanced procedural swimmers.
// One generic mesh is reshaped per species by instance data (proportions,
// colors, pattern, undulation) - adding species never touches this shader.

#include "Common.hlsli"

struct VSIn
{
    float3 pos : POSITION;
    float2 uv : TEXCOORD0;      // u: 0 head .. 1 tail base; v: radial angle 0..1 (0 = top)
    float3 normal : NORMAL;
    float part : TEXCOORD1;     // 0 body, 1 dorsal, 2 pectoral, 3 tail fin
};

struct VSOut
{
    float4 pos : SV_POSITION;
    float3 world : WORLDPOS;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float part : TEXCOORD1;
    nointerpolation float4 colorA : COLOR0;
    nointerpolation float4 colorB : COLOR1;
    nointerpolation float4 fin : COLOR2;
    nointerpolation float2 misc : TEXCOORD2;  // x: glow, y: pattern id
    nointerpolation float stripes : TEXCOORD3;
};

float3x3 RotY(float a) { float c = cos(a), s = sin(a); return float3x3(c, 0, s, 0, 1, 0, -s, 0, c); }
float3x3 RotZ(float a) { float c = cos(a), s = sin(a); return float3x3(c, -s, 0, s, c, 0, 0, 0, 1); }
float3x3 RotX(float a) { float c = cos(a), s = sin(a); return float3x3(1, 0, 0, 0, c, -s, 0, s, c); }

VSOut VSFish(VSIn input, uint inst : SV_InstanceID)
{
    FishInstance f = FishBuffer[inst];

    float u = input.uv.x;
    float v = input.uv.y;
    uint part = (uint)(input.part + 0.5);

    float len = f.PosScale.w;
    float phase = f.Orient.w;

    float bodyH = f.ShapeA.x, bodyW = f.ShapeA.y;
    float headBlunt = f.ShapeA.z, tailFork = f.ShapeA.w;
    float dorsalH = f.ShapeB.x, tailLen = f.ShapeB.y, pectoral = f.ShapeB.z;
    float formId = f.Extras.w;

    bool isRay = formId > 1.5 && formId < 2.5;
    bool isDrifter = formId > 0.5 && formId < 1.5;

    float3 local = input.pos;

    // Head bluntness widens the forward body.
    if (part == 0)
    {
        float headBoost = 1.0 + headBlunt * 0.55 * pow(saturate(1.0 - u), 2.2);
        local.yz *= headBoost;
    }
    // Tail fin fork: trailing edge dips deeper toward the lobes.
    if (part == 3)
    {
        float ay = saturate(abs(local.y) / 0.9);
        float ext = lerp(1.0, lerp(0.45, 1.0, pow(ay, 1.5)), tailFork);
        local.x -= ext * tailLen;
    }
    // Dorsal fin height scales by the tip offset stored in uv.y.
    if (part == 1)
    {
        local.y = input.pos.y + input.uv.y * (dorsalH / 0.45);
    }
    // Pectoral fins.
    if (part == 2)
    {
        local.xz *= lerp(0.6, 1.5, pectoral);
    }

    // Proportions: rays flatten and widen, drifters shrink undulation.
    float sy = 0.17 * bodyH;
    float sz = 0.14 * bodyW;
    if (isRay) { sy = 0.045; sz = 0.52 * bodyW; }

    local.x *= len;
    local.y *= len * sy;
    local.z *= len * sz;

    // Swimming undulation, growing toward the tail.
    float undShape = isRay ? pow(saturate(abs(input.pos.z) * 0.85), 1.3) : pow(u, 1.7);
    float undAmp = undShape * (isDrifter ? 0.10 : 0.24) * len * sz;
    float sway = sin(phase * 6.2831853 - (isRay ? abs(input.pos.z) * 2.6 : u * 5.2));
    if (isRay)
    {
        local.y += sway * undAmp * 2.2;
    }
    else
    {
        local.z += sway * undAmp * (part == 3 ? 1.7 : (part == 2 ? 0.5 + 0.5 * sin(phase * 12.56) : 1.0));
        local.y += sin(phase * 6.2831853 - u * 5.2 + 1.57) * undAmp * 0.18;
    }

    // Orientation: roll (bank) then pitch then yaw, column-vector convention.
    float3x3 R = mul(RotY(f.Orient.x), mul(RotZ(f.Orient.y), RotX(f.Orient.z)));
    float3 world = mul(R, local) + f.PosScale.xyz;
    float3 n = normalize(mul(R, input.normal));

    VSOut o;
    o.pos = mul(float4(world, 1.0), ViewProjection);
    o.world = world;
    o.normal = n;
    o.uv = input.uv;
    o.part = input.part;
    o.colorA = f.ColorA;
    o.colorB = f.ColorB;
    o.fin = f.Extras;
    o.misc = float2(f.ColorB.w, f.ColorA.w);
    o.stripes = max(1.0, f.ShapeB.w);
    return o;
}

float4 PSFish(VSOut input) : SV_Target
{
    float u = input.uv.x;
    float v = input.uv.y;
    uint part = (uint)(input.part + 0.5);
    float3 N = normalize(input.normal);
    float3 V = normalize(CameraPos.xyz - input.world);
    float3 L = normalize(LightDir.xyz);

    float3 dorsal = input.colorA.rgb;
    float3 bright = input.colorB.rgb;
    float3 fin = input.fin.rgb;
    float glow = input.misc.x;
    float pattern = input.misc.y;

    // Base gradient: dorsal to belly by radial angle.
    float topness = smoothstep(0.62, 0.12, abs(v - 0.0) < 0.5 ? v : 1.0 - v);
    float3 base = lerp(bright, dorsal, smoothstep(0.28, 0.75, topness));

    // Species pattern.
    if (pattern > 0.5 && pattern < 1.5)        // Stripes: bright flank line
    {
        base = lerp(base, bright, smoothstep(0.10, 0.035, abs(v - 0.5)));
    }
    else if (pattern > 1.5 && pattern < 2.5)   // Bars: vertical bands
    {
        float bar = smoothstep(0.22, 0.04, abs(frac(u * input.stripes) - 0.5) * 2.0);
        base = lerp(base, bright, bar);
    }
    else if (pattern > 2.5 && pattern < 3.5)   // Spots
    {
        float2 cell = floor(float2(u * 9.0, v * 16.0));
        float h = Hash21(cell);
        float spot = step(0.78, h);
        base = lerp(base, bright, spot * 0.85);
    }
    else if (pattern > 3.5 && pattern < 4.5)   // TwoTone
    {
        base = lerp(bright, dorsal, smoothstep(0.35, 0.6, topness));
    }
    else if (pattern > 4.5 && pattern < 5.5)   // Koi patches (world-anchored)
    {
        float n = ValueNoise(input.world.xz * 1.9 + input.world.y * 2.0);
        float patch = smoothstep(0.58, 0.66, n);
        base = lerp(bright, dorsal, patch);
    }
    else if (pattern > 5.5)                    // GlowDots (photophores)
    {
        float d = frac(u * 9.0);
        float dotM = smoothstep(0.16, 0.04, abs(d - 0.5)) * smoothstep(0.16, 0.03, abs(v - 0.5));
        base += bright * dotM * 2.4;
    }

    // Fins take the fin tint with a hint of transparency.
    if (part > 0)
    {
        base = lerp(base, fin, part == 3 ? 0.55 : 0.4);
    }

    // Eyes (small, believable, near the head on both flanks).
    float eyeSide = min(abs(v - 0.25), abs(v - 0.75));
    float eyeMask = smoothstep(0.055, 0.035, eyeSide) * smoothstep(0.10, 0.06, u) * smoothstep(0.015, 0.05, u);
    base = lerp(base, float3(0.03, 0.035, 0.045), eyeMask);
    float glint = smoothstep(0.02, 0.008, eyeSide) * smoothstep(0.07, 0.05, u) * smoothstep(0.02, 0.05, u);
    base += glint * float3(0.5, 0.55, 0.6);

    // Lighting: directional + soft hemisphere fill.
    float ndl = saturate(dot(N, L));
    float hemi = 0.5 + 0.5 * N.y;
    float3 col = base * (0.42 + 0.58 * ndl) * LightColor.rgb;
    col *= (0.75 + 0.25 * hemi) * LightColor.w;

    // Caustic sparkle on upward-facing scales.
    float caustic = CausticPattern(input.world.xz + input.world.y * 0.31, TimeMusic.x, FloorParams.w, 2);
    col += LightColor.rgb * caustic * saturate(N.y) * 0.3 * EnvMisc.w;

    // Rim light + optional bioluminescent glow.
    float rim = pow(1.0 - saturate(dot(N, V)), 3.0);
    col += LightColor.rgb * rim * 0.22 * LightColor.w;
    float beatPulse = MusicMisc.y;
    col += bright * glow * (0.55 + 0.25 * beatPulse) * (0.35 + rim * 1.2);

    float camDist = length(input.world - CameraPos.xyz);
    col = WaterFog(camDist, saturate(-input.world.y / max(FogParams.z, 1.0)), col);

    return float4(max(col, 0.0), 1.0);
}
