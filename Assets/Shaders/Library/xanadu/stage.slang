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
[[vk::binding(0, 3)]] public ConstantBuffer<Frame> frame;

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
    public Canvas canvas;
};

/// The quad under a pixel: uv's change per target pixel, inverted, gives the
/// quad's sides in pixels, and walking back from this pixel by its uv gives the corner.
/// @hidden
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
public static PlayFrame playState = {
    float4(0.0), float4(0.0, 0.0, 1.0, 1.0), float4(0.0), float4(0.0), float4(1.0, 0.0, 0.0, 0.0),
    float4(0.0), float4(0.0, 0.0, 0.0, 5.0), float4(0.0, 1e6, 0.0, 120.0),
    { float4(1e6), float4(1e6), float4(1e6), float4(1e6), float4(1e6) },
    float4(0.0, 1.0, 0.0, 0.0)
};

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
