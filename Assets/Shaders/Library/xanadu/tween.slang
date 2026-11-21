// xanadu.tween: the engine's tweens, read on the GPU. A tween is a clip
// (xanadu.anim's `Clip`, on `clock` or `clock.beats`) playing on a target: the
// CPU writes the same 48 bytes it plays itself (Core/Tweens: Clip, Tween,
// TweenPool), and this builds the very same `Clip` from them, so both sides
// land on the same weight. Offsets and turns add, scales multiply, colours
// fold by priority then start.
//
//     TweenPose pose = tweenPose(target);          // what moves a target now
//     float4 c = tweenColour(target, baseColour);  // its colour over its own
//     Clip slide = tweenClip(tweens[i]);           // any tween as a plain clip
//
// A target is the index the CPU's TweenTarget holds. Not part of `import xanadu;`:
// `import xanadu.tween;`.
module tween;
import xanadu.anim;

/// One tween: `Tween` in Core/Tweens/Tween.cs, 48 bytes. Only ever add, at the end.
public struct Tween
{
    /// Offset (xy), turn in radians (x), scale (xy) or colour (rgba) at full weight.
    public float4 value;
    /// Its clip: start and length on its clock.
    public float start;
    public float length;
    /// Its clip's ease (EasePreset, bits 0..7), clock (8..15: 0 seconds, 1 beats), shape (16..23: 1 bounced, 2 reversed).
    public uint clipBits;
    /// The weight mixed in from (progress 0) to (progress 1).
    public float weightFrom;
    public float weightTo;
    /// A run every this long when it loops; never sooner than the clip's span.
    public float every;
    /// Runs (low 16 bits, 0 forever), the caller's layer name (high 16).
    public uint loopsKey;
    /// Channel (bits 0..7), loop (8..15: 0 none, 1 looped, 2 stacked), priority (16..31).
    public uint bits;
};

/// A target's tweens: a range of `tweens`.
public struct TweenRange
{
    public uint start;
    public uint count;
    public uint pad0;
    public uint pad1;
};

/// Every target's tweens, sorted by priority then start inside each target.
[Global] public StructuredBuffer<Tween> tweens;

/// Each target's range of `tweens`, by target index.
[Global] public StructuredBuffer<TweenRange> tweenTargets;

public static const uint TweenOffset = 0, TweenRotation = 1, TweenScale = 2, TweenColour = 3, TweenNumber = 4, TweenOpacity = 5;
public static const uint TweenOnce = 0, TweenLooped = 1, TweenStacked = 2;

/// Core's EasePreset (by number) as the `eases.` value drawing the same curve.
static const int PresetEases[30] = {
    32,                 // step
    0, 31, 1, 2,        // linear, ease (smooth), ease in (inQuad), ease out (outQuad)
    13, 14, 15,         // sine in, out, in-out
    3,                  // quad in-out
    4, 5, 6,            // cubic
    7, 8, 9,            // quart
    16, 17, 18,         // expo
    19, 20, 21,         // circ
    25, 26, 27,         // back
    22, 23, 24,         // elastic
    28, 29, 30,         // bounce
};

/// The `eases.` value of a Core EasePreset number.
public Ease easeOfPreset(uint preset)
{
    return Ease(preset < 30 ? PresetEases[preset] : 0);
}

/// The tween's clock: `clock` for seconds, `clock.beats` for beats.
public Clock tweenClock(Tween t)
{
    return ((t.clipBits >> 8) & 0xFF) == 1 ? clock.beats : clock;
}

/// The tween's clip, as Core/Tweens/Clip.cs builds it.
public Clip tweenClip(Tween t)
{
    Clip c = tweenClock(t).clip(t.start, t.length).eased(easeOfPreset(t.clipBits & 0xFF));
    uint shape = (t.clipBits >> 16) & 0xFF;
    if ((shape & 1) != 0)
        c = c.bounced();
    if ((shape & 2) != 0)
        c = c.reversed();
    return c;
}

/// Tween.Weight: the clip's progress (plus the runs done when stacked), mixed from its start weight to its end weight.
public float tweenWeight(Tween t, float now)
{
    Clip c = tweenClip(t);
    uint channel = t.bits & 0xFF;
    uint loop = (t.bits >> 8) & 0xFF;
    uint loops = t.loopsKey & 0xFFFF;
    float runs = 0.0;
    if (loop != TweenOnce && now > t.start)
    {
        float every = max(t.every, c.span);
        float index = floor((now - t.start) / max(every, 1e-5));
        if (loops != 0 && index >= float(loops))
        {
            index = float(loops) - 1.0;
            now = c.end + index * every;
        }

        now -= index * every;
        if (loop == TweenStacked && channel != TweenColour)
            runs = index;
    }

    return t.weightFrom + (t.weightTo - t.weightFrom) * (c.at(now) + runs);
}

/// Tween.Weight() now, on its own clock.
public float tweenWeight(Tween t)
{
    return tweenWeight(t, tweenClock(t).total);
}

/// What a target's movement adds up to: `TweenPose` in Core.
public struct TweenPose
{
    public float2 offset;
    public float rotation;
    public float2 scale;
    /// How much of its opacity it keeps, 0..1 (more overshoots).
    public float opacity;

    /// Where a point in the target's own space shows: scaled and turned about `pivot`, then slid.
    public float2 placed(float2 p, float2 pivot)
    {
        float c = cos(rotation), s = sin(rotation);
        float2 d = (p - pivot) * scale;
        return pivot + offset + float2(c * d.x - s * d.y, s * d.x + c * d.y);
    }
};

/// TweenPool.Pose: a target's offsets and turns summed, its scales and opacities multiplied.
public TweenPose tweenPose(uint target)
{
    TweenPose pose;
    pose.offset = float2(0, 0);
    pose.rotation = 0.0;
    pose.scale = float2(1, 1);
    pose.opacity = 1.0;
    TweenRange range = tweenTargets[target];
    for (uint i = range.start; i < range.start + range.count; i++)
    {
        Tween t = tweens[i];
        uint channel = t.bits & 0xFF;
        if (channel == TweenColour || channel == TweenNumber)
            continue;
        float w = tweenWeight(t);
        if (channel == TweenOffset)
            pose.offset += t.value.xy * w;
        else if (channel == TweenRotation)
            pose.rotation += t.value.x * w;
        else if (channel == TweenScale)
            pose.scale *= 1.0 + (t.value.xy - 1.0) * w;
        else if (channel == TweenOpacity)
            pose.opacity *= 1.0 + (t.value.x - 1.0) * w;
    }
    return pose;
}

/// TweenPool.Colour: each colour tween, lowest priority (then earliest) first, fades what is beneath it.
public float4 tweenColour(uint target, float4 under)
{
    float4 c = under;
    TweenRange range = tweenTargets[target];
    for (uint i = range.start; i < range.start + range.count; i++)
    {
        Tween t = tweens[i];
        if ((t.bits & 0xFF) == TweenColour)
            c = lerp(c, t.value, tweenWeight(t));
    }
    return c;
}
