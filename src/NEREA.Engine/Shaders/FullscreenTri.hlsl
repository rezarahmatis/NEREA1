// NEREA - Fullscreen triangle vertex shader, shared by all post passes.

struct VSPostOut
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
};

VSPostOut VSFullscreen(uint id : SV_VertexID)
{
    VSPostOut o;
    float2 uv = float2((id << 1) & 2, id & 2);
    o.pos = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    o.uv = uv;
    return o;
}
