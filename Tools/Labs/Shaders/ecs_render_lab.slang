// Xanadu Labs' ECS render lab. Everything on the stage is one kind of thing:
// an instance, a quad with a place, a size, a colour and a look. The CPU
// writes one per shape and one per glyph, already in draw order, and this
// draws them all with one call: the vertex stage makes each quad's corners
// from its instance, the fragment stage shapes it (a box, a disc, a ring) or
// reads a glyph's coverage from the text atlas.
//
// SDL_GPU's SPIR-V layout: vertex storage set 0, uniforms set 1; fragment
// samplers set 2.

import xanadu.math;
import xanadu.shape;

struct Instance
{
    float4 box;     // centre x, y and width, height (stage px)
    float4 uv;      // a glyph's atlas rect: min u, v, max u, v
    float4 colour;  // straight alpha
    float4 look;    // turn (radians), kind, edge (corner radius or ring width), unused
};

static const float KindBox = 0, KindDisc = 1, KindRing = 2, KindGlyph = 3;

struct Frame
{
    float4 stage;   // width, height (px), first instance of this draw, unused
};

[[vk::binding(0, 0)]] StructuredBuffer<Instance> instances;
[[vk::binding(0, 1)]] ConstantBuffer<Frame> stageFrame;
[[vk::binding(0, 2)]] Sampler2D atlas;

struct Varyings
{
    float4 position : SV_Position;
    float2 local : TEXCOORD0;                   // px from the quad's centre, before its turn
    float2 uv : TEXCOORD1;
    nointerpolation float4 colour : TEXCOORD2;
    nointerpolation float4 shape : TEXCOORD3;   // width, height, kind, edge
};

static const float2 Corners[6] =
{
    float2(-0.5, -0.5), float2(0.5, -0.5), float2(0.5, 0.5),
    float2(-0.5, -0.5), float2(0.5, 0.5), float2(-0.5, 0.5)
};

[shader("vertex")]
Varyings itemVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    Instance item = instances[uint(stageFrame.stage.z) + instance];
    float kind = item.look.y;
    float2 corner = Corners[vertex];

    // A shape's quad is a pixel wider all round, so its smoothed edge has room; a glyph's is exact.
    float2 size = item.box.zw + (kind == KindGlyph ? 0.0 : 2.0);
    float2 local = corner * size;
    float2 pixel = item.box.xy + rotate(local, item.look.x);
    float2 ndc = pixel / stageFrame.stage.xy * 2 - 1;

    Varyings o;
    o.position = float4(ndc.x, -ndc.y, 0, 1);
    o.local = local;
    o.uv = lerp(item.uv.xy, item.uv.zw, corner + 0.5);
    o.colour = item.colour;
    o.shape = float4(item.box.zw, kind, item.look.z);
    return o;
}

[shader("fragment")]
float4 itemShade(Varyings v) : SV_Target
{
    float2 size = v.shape.xy;
    float kind = v.shape.z, edge = v.shape.w;
    float cover;
    if (kind == KindGlyph)
        cover = atlas.SampleLevel(v.uv, 0).a;
    else if (kind == KindRing)
        cover = sdf.stroked(Circle(size.x * 0.5 - edge * 0.5).local(v.local), edge);
    else if (kind == KindDisc)
        cover = sdf.filled(Circle(size.x * 0.5).local(v.local));
    else
        cover = sdf.filled(Box(size, edge).local(v.local));

    float alpha = v.colour.a * cover;
    return float4(v.colour.rgb * alpha, alpha);
}
