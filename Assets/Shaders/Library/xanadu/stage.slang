// xanadu.stage, contract version 1 (Docs/host.md §3).
//
// What the host's vertex stage hands every fragment effect, and the resources
// every effect can read. Only ever add to it: effects written against v1 must
// keep compiling.
//
// Bindings follow SDL_GPU's SPIR-V layout: fragment samplers in set 2, fragment
// uniform buffers in set 3. The host owns samplers 0 (the item's texture), 1
// (the screen copy, see sampleScreen) and 2 (the depth, see sampleDepth) and
// uniform buffer 0 (the frame); an effect's own textures start at sampler 3 and its values live in uniform
// buffer 1, a struct named however it likes. The globals come after the last
// sampler, as storage buffers the host places (see `Global` below):
//
//     struct Params { [Default(1)] float speed; float4 tint; };
//     [[vk::binding(1, 3)]] ConstantBuffer<Params> params;
//     [[vk::binding(3, 2)]] Sampler2D notes;
//
// Values a material doesn't set take the field's [Default(...)], else zero.
//
// A level shader slot holds just an effect body: global uniforms (tagged
// [Instance] when a group may key them) and `shade`, which takes the quad
// (`Canvas canvas`), the rest of the pixel's input (`ItemInput i`), both, or
// neither. The host wraps it with this import and an entry point that passes
// what shade asked for, and binds its globals to uniform buffer 1:
//
//     [Instance] [Default(0.5)] uniform float strength;
//     uniform float4 tint;
//     float4 shade(ItemInput i) { return i.colour * tint * strength; }
//     float4 shade(Canvas canvas) { return tint * (canvas.centred.x > 0 ? 1 : 0); }
//
// A body may also declare `ItemVertex deform(ItemVertex v)`: its own vertex
// stage, run on every corner before it lands. It sees the same globals (time,
// beat, and every [Global] buffer) and the same values; of the textures it can
// read only the depth (sampleDepth).
//
//     ItemVertex deform(ItemVertex v) { v.position.y += sin(time * 4 + v.uv.x * 6) * 8; return v; }
//
// A rect has four corners; [Grid(columns, rows)] on deform cuts it into cells.
module stage;

/// @hidden
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

/// Asks for a value the item's shape gives when the item fits its shader to
/// it (FIT, on in the XIF editor): x and y become the shape's half width and
/// half height, in item units; z and w keep the set value.
///
///     [Fit] [Default(1, 1)] uniform float2 box;
[__AttributeUsage(_AttributeTargets.Var)]
public struct FitAttribute
{
};

/// Names a whole-number value's choices, split by |, so editors show a list of
/// names to pick from instead of a number: the first name is 0, the next 1, …
///
///     [Options("circle|box|star")] [Default(1)] uniform float shape;
[__AttributeUsage(_AttributeTargets.Var)]
public struct OptionsAttribute
{
    string names;
};

/// Marks a read-only global every shader can read (Docs/shader-pipeline.md §0):
///
///     [Global] public StructuredBuffer<PlayFrame> playFrame;
///
/// in any library module. The API gives it its binding in every stage; whoever
/// owns the data declares it on the C# side (ShaderGlobals.Declare) under the
/// same name and writes it. The API itself knows nothing about what it holds.
/// An effect that reads a global the running host doesn't declare doesn't compile.
[__AttributeUsage(_AttributeTargets.Var)]
public struct GlobalAttribute
{
};

/// Put on `deform`: every rect the effect draws is cut into `columns` × `rows` cells
/// (uv 0..1 across, corners shared), so a deform has corners inside to bend, not just
/// four at its edges. xanadu.mesh surfaces need one; the host caps each side at 256.
///
///     [Grid(64, 36)] ItemVertex deform(ItemVertex v) { return ball().deformed(v, camera()); }
[__AttributeUsage(_AttributeTargets.Function)]
public struct GridAttribute
{
    int columns;
    int rows;
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
    /// Where the corner was before `deform` moved it, in target pixels: blended across
    /// the triangle like uv, it gives `rest`, the quad as it was. Where it landed, without one.
    public float2 rest : TEXCOORD2;
};

/// One corner of what is drawn, as an effect's `deform` sees it and changes it:
/// the vertex stage, before it lands on the target. Moving `position` moves the
/// shape; uv, colour and local travel on to `shade` as they are left.
public struct ItemVertex
{
    /// Where it lands on the target, in pixels (y down).
    public float2 position;
    /// Its UV: 0..1 across a rect.
    public float2 uv;
    /// Its colour, modulates included.
    public float4 colour;
    /// Where it is in the item's own space.
    public float2 local;
    /// The quad at rest it belongs to, on the target, before any deform: `rest.position`
    /// is the quad's middle (`v.rest.position + rotate(v.position - v.rest.position, a)`
    /// turns round it), `rest.centred` this corner's place on it from there. Lines and
    /// untextured shapes, whose uv doesn't span them, give it no size.
    public Canvas rest;
    /// Its point in the item's mesh: triangles that share a corner share it, so it can
    /// seed per-point randomness that keeps the mesh joined.
    public uint index;
    /// How far into the screen it is, in pixels: 0 on the item's plane, more is farther.
    /// Where the effect's own triangles overlap, the nearest wins per pixel; left at 0
    /// they draw in order, later over earlier. It never moves the item past others:
    /// the effect keeps its place in the frame's draw order whatever its depth.
    public float depth;
};

/// Per-frame values the host sets once.
public struct Frame
{
    public float2 screen;
    public float time;
    /// 1 when the item's texture holds premultiplied colour (a buffered group's
    /// image); input() and sampleItem() hand effects straight colour either way.
    public float itemPremultiplied;
    /// Music beats since the music started, from whoever plays it (the game: its song's timing).
    public float beat;
};

[[vk::binding(0, 2)]] public Sampler2D itemTexture;
[[vk::binding(1, 2)]] public Sampler2D screenTexture;
// The depth is fragment sampler 2; a deforming effect's vertex stage reads it as
// vertex sampler 0 (the host says, when it compiles one).
#ifndef XANADU_DEPTH_BINDING
#define XANADU_DEPTH_BINDING 2
#endif
#ifndef XANADU_DEPTH_SET
#define XANADU_DEPTH_SET 2
#endif
[[vk::binding(XANADU_DEPTH_BINDING, XANADU_DEPTH_SET)]] public Sampler2D depthTexture;
// The frame is fragment uniform buffer 0; a deforming effect's vertex stage
// reads it as vertex uniform buffer 2 (the host says, when it compiles one).
#ifndef XANADU_FRAME_BINDING
#define XANADU_FRAME_BINDING 0
#endif
#ifndef XANADU_FRAME_SET
#define XANADU_FRAME_SET 3
#endif
[[vk::binding(XANADU_FRAME_BINDING, XANADU_FRAME_SET)]] public ConstantBuffer<Frame> frame;

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
    /// <c>size</c> want, e.g. <c>Box(c.size).filled(c.centred)</c>.
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
    /// Seconds since the game started; the global `time` is the same, and reaches everywhere.
    public float time;
    /// The quad being drawn on the target: size, position, rotation, shear, in pixels.
    /// After a `deform` it follows the moved corners, so shapes drawn with it bend with them:
    /// `centred` runs with uv across the whole bent mesh in the quad's own `size` (rest's),
    /// so they stay whole. Anything that wants the quad as it was comes through `rest`.
    public Canvas canvas;
    /// The quad at rest, as it was before `deform` moved its corners (an elastic band before
    /// the pull), and this pixel's true place on it: shapes drawn at `rest.centred` stay
    /// straight on the target while the mesh bends (the bent mesh still bounds what shows).
    /// The same as `canvas` without a `deform`.
    public Canvas rest;
};

/// A canvas from its two sides, its uv-(0, 0) corner, and this pixel's uv on it.
/// @hidden
public Canvas canvasFrom(float2 xAxis, float2 yAxis, float2 corner, float2 uv)
{
    Canvas c;
    c.xAxis = xAxis;
    c.yAxis = yAxis;
    c.size = float2(length(xAxis), length(yAxis));
    c.corner = corner;
    c.position = corner + 0.5 * (xAxis + yAxis);
    c.centred = (uv - 0.5) * c.size;
    c.rotation = atan2(xAxis.y, xAxis.x);
    float2 along = xAxis / max(c.size.x, 1e-9);
    float2 across = float2(-along.y, along.x);
    float upright = dot(yAxis, across);
    c.shear = abs(upright) > 1e-9 ? dot(yAxis, along) / upright : 0.0;
    return c;
}

/// A canvas for a pixel where uv doesn't run across anything: all zeros at the pixel.
/// @hidden
public Canvas noCanvas(float2 pixel)
{
    Canvas c;
    c.size = float2(0.0); c.position = pixel; c.corner = pixel;
    c.rotation = 0.0; c.shear = 0.0; c.xAxis = float2(0.0); c.yAxis = float2(0.0); c.centred = float2(0.0);
    return c;
}

/// The quad under a pixel: uv's change per target pixel, inverted, gives the
/// quad's sides in pixels, and walking back from this pixel by its uv gives the corner.
/// @hidden
public Canvas canvasOf(ItemVaryings v)
{
    float2 perX = ddx(v.uv), perY = ddy(v.uv);
    float det = perX.x * perY.y - perY.x * perX.y;
    if (abs(det) < 1e-12)
        return noCanvas(v.position.xy);
    float2 xAxis = float2(perY.y, -perX.y) / det;
    float2 yAxis = float2(-perY.x, perX.x) / det;
    return canvasFrom(xAxis, yAxis, v.position.xy - xAxis * v.uv.x - yAxis * v.uv.y, v.uv);
}

/// The quad at rest under a pixel: on the quad before `deform`, the rest position is
/// corner + xAxis * uv.x + yAxis * uv.y, and blending keeps that true on every bent
/// triangle, so how rest and uv change from pixel to pixel gives the sides exactly
/// (rest's change times uv's change inverted). This pixel's true place is then where the
/// target pixel falls on that quad. Where the bent triangle is too thin to say, `bent`.
/// @hidden
public Canvas restCanvasOf(ItemVaryings v, Canvas bent)
{
    float2 uvX = ddx(v.uv), uvY = ddy(v.uv);
    float2 restX = ddx(v.rest), restY = ddy(v.rest);
    float det = uvX.x * uvY.y - uvY.x * uvX.y;
    if (abs(det) < 1e-12)
        return bent;
    float2 xAxis = (restX * uvY.y - restY * uvX.y) / det;
    float2 yAxis = (restY * uvX.x - restX * uvY.x) / det;
    float2 corner = v.rest - xAxis * v.uv.x - yAxis * v.uv.y;
    Canvas c = canvasFrom(xAxis, yAxis, corner, v.uv);
    c.centred = (c.uvAt(v.position.xy) - 0.5) * c.size;
    return c;
}

/// The pixel being shaded: input() keeps it here, so shapes can default to it
/// (`circle.filled()` is `circle.filled(canvas.centred)`).
/// @hidden
public static ItemInput shading = {};

/// How uv winds on screen at the pixel: more than 0 when it reads left to right, top to
/// bottom (the front of a surface built from it), less when mirrored. Worked out in input(),
/// before any code of the effect's branches, since a derivative taken after a branch reads
/// garbage where neighbouring pixels went the other way.
/// @hidden
public static float uvWinding = 0.0;

/// Seconds since the game started, the same clock as frame.time, for any
/// shader and any function it calls: no need to pass it down. Set before
/// `shade` runs, and before `update` for kept state.
public static float time = 0.0;

/// Music beats since the music started, for `clock.beats`: from the song's
/// timing in a level, from its timeline in the shader editor.
public static float beat = 0.0;

/// Sets the globals (time, beat) from the frame for a vertex stage, as input()
/// does for a pixel.
/// @hidden
public void vertexClock()
{
    time = frame.time;
    beat = frame.beat;
}

/// The shader editor's triangle overlay: drawn line-filled through the effect's own
/// vertex stage, so every triangle's edges show where its corners landed.
/// @hidden
public float4 overlayTriangles(ItemInput i)
{
    return float4(1.0, 1.0, 1.0, 0.85);
}

/// The shader editor's uv overlay: lines where u (red) and v (green) are 0 (solid)
/// and 1 (dashed), from the uv the vertex stage handed on, so a deform that moves
/// or scales uv moves the lines with it.
/// @hidden
public float4 overlayUvLines(ItemInput i)
{
    // About 1.5 pixels either side of each line, whatever uv's scale on screen.
    float2 width = max(fwidth(i.uv), float2(1e-6)) * 1.5;
    float2 near0 = 1.0 - saturate(abs(i.uv) / width);
    float2 near1 = 1.0 - saturate(abs(i.uv - 1.0) / width);
    float dash = step(0.5, frac((i.pixel.x + i.pixel.y) / 12.0));
    float u = max(near0.x, near1.x * dash);
    float v = max(near0.y, near1.y * dash);
    float3 rgb = (u * float3(1.0, 0.3, 0.3) + v * float3(0.3, 1.0, 0.4)) / max(u + v, 1e-6);
    return float4(rgb, max(u, v));
}

/// What the vertex stage passes on for a corner where it lands.
/// @hidden
public ItemVaryings varyings(ItemVertex v)
{
    ItemVaryings o;
    float2 ndc = v.position / frame.screen * 2.0 - 1.0;
    o.position = float4(ndc.x, -ndc.y, 0.0, 1.0);
    o.uv = v.uv;
    o.colour = v.colour;
    o.local = v.local;
    o.rest = v.position;
    return o;
}

/// @hidden
public ItemInput input(ItemVaryings v)
{
    ItemInput i;
    i.uv = v.uv;
    i.vertexColour = v.colour;
    i.colour = sampleItem(v.uv) * v.colour;
    i.local = v.local;
    i.pixel = v.position.xy;
    i.screenUv = v.position.xy / frame.screen;
    float2 uvDx = ddx(v.uv), uvDy = ddy(v.uv);
    uvWinding = uvDx.x * uvDy.y - uvDx.y * uvDy.x;
    i.time = frame.time;
    time = frame.time;
    beat = frame.beat;
    i.canvas = canvasOf(v);
    i.rest = restCanvasOf(v, i.canvas);
    // Painted on the bent mesh: centred runs with uv over every triangle, measured in the
    // quad's own size (rest's, the same everywhere). Each triangle's own stretch would
    // give each its own size and cut shapes at the edges. Without a deform they agree.
    i.canvas.size = i.rest.size;
    i.canvas.centred = (v.uv - 0.5) * i.rest.size;
    shading = i;
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

/// The frame's depth at a screen UV (ItemInput.screenUv): where the nearest
/// opaque thing drawn there sits in the draw order, 0 nearest to 1 (nothing).
/// Things drawn later are nearer. Every `shade` and every `deform` can read it.
/// The host fills it before any draw that reads it.
public float sampleDepth(float2 uv)
{
    return depthTexture.SampleLevel(uv, 0.0).r;
}
