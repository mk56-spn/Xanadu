// A TRACE brick's result drawn into the frame (CanvasRenderer.Traces.cs): the box it filled,
// its colour as it left it, its depth (pixels into the screen, as a deform's corners have it)
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
    float2 uv = c.position.xy / fragmentView.screen;
    float4 colour = tracedColour.SampleLevel(uv, 0.0);
    if (colour.a <= 0.0)
        discard;
    float d = tracedDepth.SampleLevel(uv, 0.0).r;
    float near = 0.5 - 0.5 * d / (abs(d) + 1024.0);
    Shown o;
    o.colour = colour;
    o.depth = fragmentView.depth - near * fragmentView.span;
    return o;
}
