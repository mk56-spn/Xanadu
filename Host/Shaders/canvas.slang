// The canvas: every item's triangles in pixel space, tinted by their vertex
// colour and sampled from one texture (a white pixel when untextured).
// SDL_GPU layout: vertex uniforms in set 1; fragment samplers in set 2.

struct View { float2 screen; float2 pad; };

[[vk::binding(0, 1)]] ConstantBuffer<View> view;
[[vk::binding(0, 2)]] Sampler2D image;

struct VertexIn
{
    float2 position : TEXCOORD0;
    float2 uv : TEXCOORD1;
    float4 colour : TEXCOORD2;
};

struct Varyings
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
    float4 colour : COLOR0;
};

[shader("vertex")]
Varyings item(VertexIn v)
{
    Varyings o;
    float2 ndc = v.position / view.screen * 2.0 - 1.0;
    o.position = float4(ndc.x, -ndc.y, 0.0, 1.0);
    o.uv = v.uv;
    o.colour = v.colour;
    return o;
}

[shader("fragment")]
float4 shade(Varyings i) : SV_Target
{
    return image.Sample(i.uv) * i.colour;
}
