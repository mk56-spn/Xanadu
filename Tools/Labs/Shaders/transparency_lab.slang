// Xanadu Labs' transparency lab: the same translucent layers drawn four ways.
// Layers are quads pulled from SV_VertexID; each fragment works out its own
// coverage and depth (a tilt makes the depth vary across the quad, so layers
// can cross each other).

struct Layer
{
    float4 rect;   // x, y, width, height in target pixels
    float4 colour; // rgb, alpha
    float4 shape;  // depth at the centre, tilt, shape, unused
};

struct Draw
{
    Layer layer;
    float4 info; // target width, target height, OIT weight power, unused
};

static const int MaxLayers = 32;

struct Scene
{
    float4 info; // target width, target height, layer count, unused
    Layer layers[MaxLayers];
};

[[vk::binding(0, 1)]] ConstantBuffer<Draw> drawVertex;
[[vk::binding(0, 3)]] ConstantBuffer<Draw> draw;
[[vk::binding(0, 3)]] ConstantBuffer<Scene> scene;
[[vk::binding(0, 2)]] Sampler2D accumMap;
[[vk::binding(1, 2)]] Sampler2D revealMap;

struct Varyings
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

static const float2 Corners[6] = { float2(0, 0), float2(1, 0), float2(1, 1), float2(0, 0), float2(1, 1), float2(0, 1) };

[shader("vertex")]
Varyings layerVertex(uint vertex : SV_VertexID)
{
    float2 corner = Corners[vertex];
    float2 pixel = drawVertex.layer.rect.xy + corner * drawVertex.layer.rect.zw;
    float2 ndc = pixel / drawVertex.info.xy * 2.0 - 1.0;
    Varyings o;
    o.position = float4(ndc.x, -ndc.y, 0.5, 1.0);
    o.uv = corner;
    return o;
}

/// The layer's straight colour and alpha at uv (alpha 0 outside its shape), and its depth there.
float4 evaluate(Layer layer, float2 uv, out float depth)
{
    depth = saturate(layer.shape.x + layer.shape.y * (uv.x - 0.5));
    if (any(uv < 0.0) || any(uv > 1.0))
        return float4(0, 0, 0, 0);

    float2 p = uv * 2.0 - 1.0;
    float r2 = dot(p, p);
    float coverage;
    switch (int(layer.shape.z))
    {
    case 1: coverage = r2 <= 1.0 ? 1.0 : 0.0; break;                // circle
    case 2: coverage = pow(saturate(1.0 - r2), 2.0); break;         // soft puff
    default: coverage = 1.0; break;                                 // square
    }
    return float4(layer.colour.rgb, saturate(layer.colour.a * coverage));
}

// ---- order dependent: plain "over" blending, in whatever order the CPU draws ---

[shader("fragment")]
float4 blendShade(Varyings v) : SV_Target
{
    float depth;
    float4 c = evaluate(draw.layer, v.uv, depth);
    if (c.a <= 0.0)
        discard;
    return float4(c.rgb * c.a, c.a); // premultiplied: blend One, OneMinusSrcAlpha
}

// ---- weighted blended OIT (McGuire & Bavoil 2013) ---------------------------

struct OitOut
{
    float4 accum : SV_Target0;  // RGBA16F, blend One + One
    float4 reveal : SV_Target1; // R16F cleared to 1, blend Zero + OneMinusSrcColor
};

[shader("fragment")]
OitOut oitShade(Varyings v)
{
    float depth;
    float4 c = evaluate(draw.layer, v.uv, depth);
    if (c.a <= 0.0)
        discard;

    // Nearer layers count for more: the weight doubles every 1/power of depth.
    // Power 0 is a plain alpha-weighted average, blind to depth.
    float w = c.a * clamp(exp2(draw.info.z * (1.0 - depth)), 1e-2, 1e4);
    OitOut o;
    o.accum = float4(c.rgb * c.a, c.a) * w;
    o.reveal = float4(c.a, c.a, c.a, c.a);
    return o;
}

struct FullVaryings
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

[shader("vertex")]
FullVaryings full(uint vertex : SV_VertexID)
{
    float2 uv = float2((vertex << 1) & 2, vertex & 2);
    FullVaryings o;
    o.position = float4(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0, 0.0, 1.0);
    o.uv = uv;
    return o;
}

[shader("fragment")]
float4 oitComposite(FullVaryings v) : SV_Target
{
    float4 accum = accumMap.Sample(v.uv);
    float reveal = revealMap.Sample(v.uv).r;
    if (reveal >= 1.0)
        discard;
    float3 average = accum.rgb / clamp(accum.a, 1e-5, 5e4);
    return float4(average, 1.0 - reveal); // blend SrcAlpha, OneMinusSrcAlpha
}

// ---- exact: every layer under the pixel, sorted per pixel -------------------

[shader("fragment")]
float4 exactShade(FullVaryings v) : SV_Target
{
    float2 pixel = v.uv * scene.info.xy;
    int count = min(int(scene.info.z), MaxLayers);
    float depths[MaxLayers];
    float4 colours[MaxLayers];
    int found = 0;
    for (int i = 0; i < count; i++)
    {
        Layer layer = scene.layers[i];
        float depth;
        float4 c = evaluate(layer, (pixel - layer.rect.xy) / layer.rect.zw, depth);
        if (c.a <= 0.0)
            continue;

        // Insertion sort, nearest first; ties keep draw order.
        int at = found;
        while (at > 0 && depths[at - 1] > depth)
        {
            depths[at] = depths[at - 1];
            colours[at] = colours[at - 1];
            at--;
        }
        depths[at] = depth;
        colours[at] = c;
        found++;
    }

    if (found == 0)
        discard;

    // Front to back: what's left to see through shrinks with each layer.
    float3 colour = 0;
    float through = 1.0;
    for (int i = 0; i < found; i++)
    {
        colour += through * colours[i].rgb * colours[i].a;
        through *= 1.0 - colours[i].a;
    }
    return float4(colour, 1.0 - through); // premultiplied
}

// ---- background: a checker, so what's see-through reads as see-through ------

[shader("fragment")]
float4 back(FullVaryings v) : SV_Target
{
    int2 cell = int2(floor(v.position.xy / 24.0));
    float grey = (cell.x + cell.y) % 2 == 0 ? 0.16 : 0.22;
    return float4(grey, grey, grey * 1.1, 1.0);
}
