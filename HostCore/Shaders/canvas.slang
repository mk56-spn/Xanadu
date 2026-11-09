// The host's standard vertex stage and the plain fragment.
// Vertex uniforms in set 1; everything a fragment sees is xanadu.stage's.
import xanadu.stage;

struct View { float2 screen; float2 pad; };

[[vk::binding(0, 1)]] ConstantBuffer<View> view;

struct VertexIn
{
    float2 position : TEXCOORD0;
    float2 uv : TEXCOORD1;
    float4 colour : TEXCOORD2;
    float2 local : TEXCOORD3;
};

[shader("vertex")]
ItemVaryings item(VertexIn v)
{
    ItemVaryings o;
    float2 ndc = v.position / view.screen * 2.0 - 1.0;
    o.position = float4(ndc.x, -ndc.y, 0.0, 1.0);
    o.uv = v.uv;
    o.colour = v.colour;
    o.local = v.local;
    return o;
}

[shader("fragment")]
float4 shade(ItemVaryings v) : SV_Target
{
    return input(v).colour;
}
