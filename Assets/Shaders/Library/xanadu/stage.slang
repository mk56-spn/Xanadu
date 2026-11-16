// xanadu.stage, contract version 1 (Docs/host.md §3).
//
// What the host's vertex stage hands every fragment effect, and the resources
// every effect can read. Only ever add to it: effects written against v1 must
// keep compiling.
//
// Bindings follow SDL_GPU's SPIR-V layout: fragment samplers in set 2, fragment
// uniform buffers in set 3. The host owns samplers 0 (the item's texture) and 1
// (the screen copy, see sampleScreen) and uniform buffer 0 (the frame); an
// effect's own textures start at sampler 2 and its values live in uniform
// buffer 1, a struct named however it likes. The run's chart (`chart`) is the
// storage buffer after the last sampler:
//
//     struct Params { [Default(1)] float speed; float4 tint; };
//     [[vk::binding(1, 3)]] ConstantBuffer<Params> params;
//     [[vk::binding(2, 2)]] Sampler2D notes;
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
// beat, the play state, the chart) and the same values; it can't read textures.
//
//     ItemVertex deform(ItemVertex v) { v.position.y += sin(time * 4 + v.uv.x * 6) * 8; return v; }
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
};

/// The play state, as the host hands it over each frame (xanadu.play reads it:
/// `player.`, `score.`, `song.`, `contact.`, `events.`). Target pixels; seconds.
/// Only ever add, at the end.
public struct PlayFrame
{
    /// The player's centre (xy) and velocity a second (zw).
    public float4 player;
    /// The playfield: top-left corner (xy) and size (zw).
    public float4 playfield;
    /// 1 or 0: grounded, dashing, ground mode HOLD (0 is SKID), dead.
    public float4 state;
    /// The way the last dash or momentum note sent the player (xy).
    public float4 moves;
    /// Accuracy 0..1, combo, best combo, score 0..1,000,000.
    public float4 score;
    /// The last judgement (as the game numbers them), song progress 0..1.
    public float4 judgement;
    /// Contact's three bars, and the most each holds.
    public float4 contact;
    /// Song beat, seconds until the next note (1e6 when none), song seconds, BPM.
    public float4 song;
    /// Seconds since each event (Event.id), 1e6 when it never happened.
    public float4 events[5];
    /// The chart: how many notes `chart` holds (x), target pixels per playfield unit (y).
    public float4 chart;
};

/// One note of the run's chart, as the host hands it over (xanadu.play reads it:
/// `notes.`). Playfield units; song seconds. Only ever add, at the end.
public struct ChartNote
{
    /// Where it is placed (xy), before any group moves it.
    public float4 place;
    /// Its colour.
    public float4 colour;
    /// When it is due (x), its kind (y), its action key (z), its index (w).
    public float4 timing;
    /// When it was judged (x, 1e6 while waiting), the judgement (y), how early
    /// (-) or late (+) in milliseconds (z), its state (w: 0 waiting, 1 hit, 2 missed).
    public float4 result;
    /// Where it was when judged, groups' moves included (xy).
    public float4 judged;
};

/// Per-frame values the host sets once.
public struct Frame
{
    public float2 screen;
    public float time;
    /// 1 when the item's texture holds premultiplied colour (a buffered group's
    /// image); input() and sampleItem() hand effects straight colour either way.
    public float itemPremultiplied;
    /// Seconds since each play key was last pressed (1e6 when never):
    /// left, right, up, down; then the three action keys and a spare.
    public float4 directionKeys;
    public float4 actionKeys;
    /// The play state (xanadu.play).
    public PlayFrame play;
};

[[vk::binding(0, 2)]] public Sampler2D itemTexture;
[[vk::binding(1, 2)]] public Sampler2D screenTexture;
// The frame is fragment uniform buffer 0; a deforming effect's vertex stage
// reads it as vertex uniform buffer 2 (the host says, when it compiles one).
#ifndef XANADU_FRAME_BINDING
#define XANADU_FRAME_BINDING 0
#endif
#ifndef XANADU_FRAME_SET
#define XANADU_FRAME_SET 3
#endif
[[vk::binding(XANADU_FRAME_BINDING, XANADU_FRAME_SET)]] public ConstantBuffer<Frame> frame;

// The chart sits right after an effect's samplers (SDL_GPU puts storage buffers
// there), so where depends on how many textures the effect declares: the host
// says, when it compiles the effect. Read it through `notes.` in xanadu.play.
#ifndef XANADU_CHART_BINDING
#define XANADU_CHART_BINDING 2
#endif
#ifndef XANADU_CHART_SET
#define XANADU_CHART_SET 2
#endif
[[vk::binding(XANADU_CHART_BINDING, XANADU_CHART_SET)]] public StructuredBuffer<ChartNote> chart;

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

/// Seconds since the game started, the same clock as frame.time, for any
/// shader and any function it calls: no need to pass it down. Set before
/// `shade` runs, and before `update` for kept state.
public static float time = 0.0;

/// Song beats since the song started, for `clock.beats`: from the song's
/// timing in a level, from its timeline in the shader editor.
public static float beat = 0.0;

/// Seconds since each play key was last pressed, from the frame: `keys.` in
/// xanadu.anim reads them. Set before `shade` runs; 1e6 in `update`.
public static float4 directionKeys = float4(1e6);
public static float4 actionKeys = float4(1e6);

/// The play state from the frame, for xanadu.play. Set before `shade` runs;
/// at rest (every event never) in `update`.
/// (Set from a function: Slang can't read a global made from a list of numbers back
/// out of the precompiled library.)
public static PlayFrame playState = restingPlay();

/// @hidden
PlayFrame restingPlay()
{
    PlayFrame p = {};
    p.playfield = float4(0.0, 0.0, 1.0, 1.0);
    p.score = float4(1.0, 0.0, 0.0, 0.0);
    p.contact = float4(0.0, 0.0, 0.0, 5.0);
    p.song = float4(0.0, 1e6, 0.0, 120.0);
    p.events = { float4(1e6), float4(1e6), float4(1e6), float4(1e6), float4(1e6) };
    p.chart = float4(0.0, 1.0, 0.0, 0.0);
    return p;
}

/// Sets the globals (time, beat, keys, play state) from the frame for a vertex
/// stage, as input() does for a pixel.
/// @hidden
public void vertexClock()
{
    time = frame.time;
    directionKeys = frame.directionKeys;
    actionKeys = frame.actionKeys;
    playState = frame.play;
    beat = frame.play.song.x;
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
    i.time = frame.time;
    time = frame.time;
    directionKeys = frame.directionKeys;
    actionKeys = frame.actionKeys;
    playState = frame.play;
    beat = frame.play.song.x;
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
