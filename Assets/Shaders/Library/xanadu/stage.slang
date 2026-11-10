// xanadu.stage, contract version 1 (docs/host.md §3).
//
// What the host's vertex stage hands every fragment effect, and the resources
// every effect can read. Only ever add to it: effects written against v1 must
// keep compiling.
//
// Bindings follow SDL_GPU's SPIR-V layout: fragment samplers in set 2, fragment
// uniform buffers in set 3. The host owns samplers 0 (the item's texture) and 1
// (the screen copy, see sampleScreen) and uniform buffer 0 (the frame); an
// effect's own textures start at sampler 2 and its values live in uniform
// buffer 1, a struct named however it likes:
//
//     struct Params { [Default(1)] float speed; float4 tint; };
//     [[vk::binding(1, 3)]] ConstantBuffer<Params> params;
//     [[vk::binding(2, 2)]] Sampler2D notes;
//
// Values a material doesn't set take the field's [Default(...)], else zero.
//
// A level shader slot holds just an effect body: global uniforms (tagged
// [Instance] when a group may key them) and `float4 shade(ItemInput i)`. The
// host wraps it with this import and an entry point, and binds its globals to
// uniform buffer 1:
//
//     [Instance] [Default(0.5)] uniform float strength;
//     uniform float4 tint;
//     float4 shade(ItemInput i) { return i.colour * tint * strength; }
module stage;

public static const int VERSION = 1;

/// A field's value when the material doesn't set one: up to four numbers.
[__AttributeUsage(_AttributeTargets.Var)]
public struct DefaultAttribute
{
    float x;
    float y = 0;
    float z = 0;
    float w = 0;
};

/// Marks a level shader's global uniform as keyable per item: a group's
/// item-shader track can animate it, and each run of items gets its own value.
[__AttributeUsage(_AttributeTargets.Var)]
public struct InstanceAttribute
{
};

/// What the vertex stage passes on, in this order (varyings match by position).
public struct ItemVaryings
{
    public float4 position : SV_Position;
    public float2 uv : TEXCOORD0;
    public float4 colour : COLOR0;
    public float2 local : TEXCOORD1;
};

/// Per-frame values the host sets once.
public struct Frame
{
    public float2 screen;
    public float time;
    /// 1 when the item's texture holds premultiplied colour (a buffered group's
    /// image); input() and sampleItem() hand effects straight colour either way.
    public float itemPremultiplied;
};

[[vk::binding(0, 2)]] public Sampler2D itemTexture;
[[vk::binding(1, 2)]] public Sampler2D screenTexture;
[[vk::binding(0, 3)]] public ConstantBuffer<Frame> frame;

/// The quad being drawn, as it lands on the target, in target pixels (y down).
/// Worked out from how uv changes from pixel to pixel, so it is exact for any
/// rect or quad whose uv runs 0..1 across it, turned or sheared, and the same
/// for every pixel of it; meaningless where uv doesn't run across the shape.
public struct Canvas
{
    /// Its width and height: the lengths of its two sides.
    public float2 size;
    /// Its centre.
    public float2 position;
    /// The corner where uv is (0, 0).
    public float2 corner;
    /// How far its width side is turned from the target's x axis, in radians, clockwise (y down).
    public float rotation;
    /// How far its height side leans from square, as a slope: 0 for a rect, 1 for 45°.
    public float shear;
    /// Its width side as a vector: where uv's x runs from 0 to 1.
    public float2 xAxis;
    /// Its height side as a vector: where uv's y runs from 0 to 1.
    public float2 yAxis;
    /// This pixel's place on the quad from its centre, in pixels along the quad's
    /// own sides (so turning the quad turns it too): what shapes sized by
    /// <c>size</c> want, e.g. <c>Box(c.size).fill(c.centred)</c>.
    public float2 centred;

    /// Width over height.
    public property float aspect { get { return size.y > 0.0 ? size.x / size.y : 0.0; } }

    /// Where on the quad (as uv) a target pixel falls.
    public float2 uvAt(float2 pixel)
    {
        float2 d = pixel - corner;
        float det = xAxis.x * yAxis.y - yAxis.x * xAxis.y;
        if (abs(det) < 1e-9)
            return float2(0.0);
        return float2(d.x * yAxis.y - d.y * yAxis.x, xAxis.x * d.y - xAxis.y * d.x) / det;
    }
};

/// What a fragment effect sees for its pixel.
public struct ItemInput
{
    /// The draw's UV: 0..1 across a rect, or whatever the triangles carry.
    public float2 uv;
    /// The vertex colour (modulates included) times the item's texture: Godot's COLOR.
    public float4 colour;
    /// The vertex colour alone.
    public float4 vertexColour;
    /// The pixel's position in the item's own space: Godot's VERTEX.
    public float2 local;
    /// The pixel's position on the target, 0..1: Godot's SCREEN_UV.
    public float2 screenUv;
    /// The pixel's position on the target, in pixels.
    public float2 pixel;
    /// Seconds since the game started: Godot's TIME.
    public float time;
    /// Half the drawn shape's size in item units, worked out from how uv
    /// changes across the pixel (the legacy CANVAS_HALF_SIZE). Exact for rects
    /// and quads whose uv runs 0..1 across them; meaningless where uv is constant.
    public float2 halfSize;
    /// The pixel's position from the shape's centre in item units, y down (the legacy PIXEL_UV).
    public float2 centred;
    /// The quad being drawn on the target: size, position, rotation, shear, in pixels.
    public Canvas canvas;
};

/// The quad under a pixel: uv's change per target pixel, inverted, gives the
/// quad's sides in pixels, and walking back from this pixel by its uv gives the corner.
public Canvas canvasOf(ItemVaryings v)
{
    Canvas c;
    float2 perX = ddx(v.uv), perY = ddy(v.uv);
    float det = perX.x * perY.y - perY.x * perX.y;
    if (abs(det) < 1e-12)
    {
        c.size = float2(0.0); c.position = v.position.xy; c.corner = v.position.xy;
        c.rotation = 0.0; c.shear = 0.0; c.xAxis = float2(0.0); c.yAxis = float2(0.0); c.centred = float2(0.0);
        return c;
    }

    c.xAxis = float2(perY.y, -perX.y) / det;
    c.yAxis = float2(-perY.x, perX.x) / det;
    c.size = float2(length(c.xAxis), length(c.yAxis));
    c.corner = v.position.xy - c.xAxis * v.uv.x - c.yAxis * v.uv.y;
    c.position = c.corner + 0.5 * (c.xAxis + c.yAxis);
    c.centred = (v.uv - 0.5) * c.size;
    c.rotation = atan2(c.xAxis.y, c.xAxis.x);
    float2 along = c.xAxis / max(c.size.x, 1e-9);
    float2 across = float2(-along.y, along.x);
    float upright = dot(c.yAxis, across);
    c.shear = abs(upright) > 1e-9 ? dot(c.yAxis, along) / upright : 0.0;
    return c;
}

public ItemInput input(ItemVaryings v)
{
    ItemInput i;
    i.uv = v.uv;
    i.vertexColour = v.colour;
    i.colour = sampleItem(v.uv) * v.colour;
    i.local = v.local;
    i.pixel = v.position.xy;
    i.screenUv = v.position.xy / frame.screen;
    i.time = frame.time;
    float2 span = fwidth(v.uv);
    i.halfSize = 0.5 * abs(float2(span.x > 1e-6 ? fwidth(v.local.x) / span.x : 0.0,
                                  span.y > 1e-6 ? fwidth(v.local.y) / span.y : 0.0));
    i.centred = (v.uv - 0.5) * 2.0 * i.halfSize;
    i.canvas = canvasOf(v);
    return i;
}

/// The item's texture at any UV: Godot's TEXTURE.
public float4 sampleItem(float2 uv)
{
    float4 texel = itemTexture.Sample(uv);
    if (frame.itemPremultiplied > 0.5 && texel.a > 0.0)
        texel.rgb /= texel.a;
    return texel;
}

/// What has been drawn on the target so far, at a screen UV (ItemInput.screenUv):
/// Godot's SCREEN_TEXTURE. The host refreshes the copy before any draw that reads
/// it if anything was drawn since the last copy. `lod` reads a blurrier mip.
public float4 sampleScreen(float2 uv, float lod = 0.0)
{
    return screenTexture.SampleLevel(uv, lod);
}
