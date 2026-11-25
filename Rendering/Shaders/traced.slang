// A graph's images presented on the frame (ExitDraws.Present): the item's box, its images the
// box's size or stretched over it, the colour as the graph left it, its depth (pixels into the screen, as a deform's corners have it)
// squashed into the step's slice of the frame's depth the way a deform's vertex wrapper does,
// so the stack's own triangles drawn after it hide behind it or show in front. Pixels it left
// clear write nothing.

struct View
{
    float2 screen;
    float depth;
    float span;
    float2 origin;
    float2 size;
    // Pixels of the box per texel of the colour and of the depth: 1 for a box-sized image.
    float2 colourStretch;
    float2 depthStretch;
};

[[vk::binding(0, 1)]] ConstantBuffer<View> vertexView;
[[vk::binding(0, 2)]] Sampler2D tracedColour;
[[vk::binding(1, 2)]] Sampler2D tracedDepth;
[[vk::binding(0, 3)]] ConstantBuffer<View> fragmentView;

struct Covered
{
    float4 position : SV_Position;
};

/// The box as two triangles: corners (0,0) (1,0) (0,1), (1,0) (1,1) (0,1).
[shader("vertex")]
Covered cover(uint vertex : SV_VertexID)
{
    float2 corner = float2((0x1A >> vertex) & 1, (0x34 >> vertex) & 1);
    float2 ndc = (vertexView.origin + corner * vertexView.size) / vertexView.screen * 2.0 - 1.0;
    Covered c;
    c.position = float4(ndc.x, -ndc.y, 0.0, 1.0);
    return c;
}

struct Shown
{
    float4 colour : SV_Target;
    float depth : SV_Depth;
};

[shader("fragment")]
Shown show(Covered c)
{
    // The images cover the item's box from its top left. A box-sized one is sampled at its texel's centre, so
    // exactly; a smaller or fixed one is stretched over the box, filtered. Depth is never blended between texels.
    float2 inBox = c.position.xy - fragmentView.origin;
    float2 size;
    tracedColour.GetDimensions(size.x, size.y);
    float4 colour = tracedColour.SampleLevel(inBox / fragmentView.colourStretch / size, 0.0);
    if (colour.a <= 0.0)
        discard;
    float d = tracedDepth.Load(int3(int2(inBox / fragmentView.depthStretch), 0)).r;
    float near = 0.5 - 0.5 * d / (abs(d) + 1024.0);
    Shown o;
    o.colour = colour;
    o.depth = fragmentView.depth - near * fragmentView.span;
    return o;
}
