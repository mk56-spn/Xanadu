// xanadu.anim: animation as values. A clock says where time comes from, a clip
// is a span of time on one, keys are values along a clip. Like a shape reads
// the pixel without being given it, a clip reads its clock without being given
// the time, so nothing is passed down. Everything is a sum on the clock: a
// seek, a scrub or a pause shows exactly the frame it lands on.
//
//     Clip pop = clock.looped(2).clip(0.2, 0.6).eased(eases.outBack);
//     dot.scale(pop.progress);
//     c.paint(dot, colours.gold.withAlpha(pop.mixed(0.4, 1)));
//
// A clock can also run from anything that says how long ago it happened (an
// `ISince`): the game's play keys, events and notes (rhythm.play) are, so a
// clip plays each time a key goes down, a note is hit…:
//
//     Clip flash = clock.since(keys.action1).clip(0, 0.25).eased(eases.outQuad);
//     Clip pulse = clock.since(events.hit).clip(0, 0.3).eased(eases.outBack);
//
// The shader editor lays the clips on a timeline under the preview. Nothing reads the code for them: its
// probe pass (Slang/ProbeNodes.cs) runs the stack's functions with `probing` on, and every clip declared or
// read then reports itself (`probeClip`), as do the keys read at one.
module anim;
import xanadu.stage;
import xanadu.math;
import xanadu.ease;

/// An easing as a value, for `.eased(...)` and keys: one per `ease.` function,
/// numbered as `ease.by` numbers them. The presets are under `eases.`.
public struct Ease
{
    public int id;

    public __init(int id) { this.id = id; }

    /// t eased: 0..1 in, the eased 0..1 out.
    public float at(float t) { return ease.by(id, t); }
};

/// One easing per `ease.` function, as values: `clip.eased(eases.outBack)`.
public namespace eases
{
    public static const Ease linear = Ease(0);
    public static const Ease inQuad = Ease(1);
    public static const Ease outQuad = Ease(2);
    public static const Ease inOutQuad = Ease(3);
    public static const Ease inCubic = Ease(4);
    public static const Ease outCubic = Ease(5);
    public static const Ease inOutCubic = Ease(6);
    public static const Ease inQuart = Ease(7);
    public static const Ease outQuart = Ease(8);
    public static const Ease inOutQuart = Ease(9);
    public static const Ease inQuint = Ease(10);
    public static const Ease outQuint = Ease(11);
    public static const Ease inOutQuint = Ease(12);
    public static const Ease inSine = Ease(13);
    public static const Ease outSine = Ease(14);
    public static const Ease inOutSine = Ease(15);
    public static const Ease inExpo = Ease(16);
    public static const Ease outExpo = Ease(17);
    public static const Ease inOutExpo = Ease(18);
    public static const Ease inCirc = Ease(19);
    public static const Ease outCirc = Ease(20);
    public static const Ease inOutCirc = Ease(21);
    public static const Ease inElastic = Ease(22);
    public static const Ease outElastic = Ease(23);
    public static const Ease inOutElastic = Ease(24);
    public static const Ease inBack = Ease(25);
    public static const Ease outBack = Ease(26);
    public static const Ease inOutBack = Ease(27);
    public static const Ease inBounce = Ease(28);
    public static const Ease outBounce = Ease(29);
    public static const Ease inOutBounce = Ease(30);
    /// smoothstep's curve (3t² - 2t³): smoothstep(0, 1, t) as an easing.
    public static const Ease smooth = Ease(31);
    /// No change until the very end, then all of it.
    public static const Ease step = Ease(32);
}

/// Something that happened some time ago: a clock can count from it
/// (`clock.since(…)`). Whoever declares a global can make its moments one.
public interface ISince
{
    /// Seconds since it last happened; 1e6 when it never has.
    property float since { get; }

    /// What it is, for the editor's timeline: a play key's number, 1000 plus an event's, or -1 for nothing it knows.
    property int sinceId { get; }
}

/// Where time comes from: seconds (`clock`), music beats (`clock.beats`) or the
/// seconds since something happened (`clock.since(…)`, anything `ISince`),
/// running on or wrapping round a loop (`clock.looped(2)`).
public struct Clock
{
    internal bool inBeats;

    /// Counting from something that happened, rather than from the start.
    internal bool counting;

    /// Seconds since it happened, when `counting`.
    internal float sinceThen;

    /// How long one loop is, in the clock's own units; 0 never wraps.
    internal float loop;

    /// What it counts from when `counting` (an ISince's `sinceId`); -1 for the song's time.
    internal int counts;

    public __init(bool inBeats, float loop)
    {
        this.inBeats = inBeats;
        this.loop = loop;
        this.counting = false;
        this.sinceThen = 0.0;
        this.counts = -1;
    }

    /// The same clock counting music beats instead of seconds.
    public property Clock beats { get { Clock c = this; c.inBeats = true; c.counting = false; return c; } }

    /// A clock counting seconds from when something last happened: 0 as it
    /// does, 1e6 while it never has (`clock.since(keys.left)`, `clock.since(notes[i])`).
    public Clock since<T : ISince>(T happened) { Clock c = Clock(false, loop); c.counting = true; c.sinceThen = happened.since; c.counts = happened.sinceId; return c; }

    /// A copy that wraps every `length` seconds (or beats).
    public Clock looped(float length) { Clock c = this; c.loop = length; return c; }

    /// The clock's reading before any loop: seconds or beats since the start, or since its key's press.
    public property float total
    {
        get
        {
            return counting ? sinceThen : inBeats ? beat : time;
        }
    }

    /// Where the clock is now: inside its loop when it has one.
    public property float now { get { return loop > 0.0 ? mod(total, loop) : total; } }

    /// How many loops are done (whole seconds or beats when it doesn't loop).
    public property int count { get { return int(floor(loop > 0.0 ? total / loop : total)); } }

    /// A clip on this clock: `length` long, starting `start` in (seconds or beats).
    public Clip clip(float start, float length) { return Clip(this, start, length); }
};

/// Seconds since the game started, as a clock; `clock.beats` counts music beats.
public static const Clock clock = Clock(false, 0.0);

/// A span of time on a clock. Its builders return changed copies
/// (`eased`, `delayed`, `staggered`, `bounced`, `reversed`), so one clip can
/// seed many; `then` starts a new one where it ends. Read it with `progress`
/// (eased, 0..1, held at the ends) or `mixed(from, to)`.
public struct Clip
{
    internal Clock clock;
    internal float start;
    internal float length;
    internal Ease easing;
    internal bool bounces;
    internal bool backwards;

    /// Which `Clip name = …;` of the code made it, numbered in the code's order (the probe's lowering sets it); -1 for none.
    internal int site;

    public __init(Clock clock, float start, float length)
    {
        this.site = -1;
        this.clock = clock;
        this.start = start;
        this.length = length;
        this.easing = Ease(0);
        this.bounces = false;
        this.backwards = false;
    }

    /// A copy with an easing: `eases.outBack`, `eases.inOutSine`, ….
    public Clip eased(Ease easing) { Clip c = this; c.easing = easing; return c; }

    /// A copy that starts `by` later.
    public Clip delayed(float by) { Clip c = this; c.start += by; return c; }

    /// A copy that starts `index × step` later: per tile, per ring, per copy.
    public Clip staggered(float index, float step) { return delayed(index * step); }

    /// A copy that plays forward then back, taking twice as long.
    public Clip bounced() { Clip c = this; c.bounces = true; return c; }

    /// A copy that plays backwards: 1 to 0.
    public Clip reversed() { Clip c = this; c.backwards = !backwards; return c; }

    /// How long it takes from start to end (twice its length when bounced).
    public property float span { get { return bounces ? length * 2.0 : length; } }

    /// Where it ends on its clock.
    public property float end { get { return start + span; } }

    /// A new clip, `length` long, starting where this one ends.
    public Clip then(float length) { return Clip(clock, end, length); }

    /// The same clip, as made by `Clip name = …;` number `site` of the code: it reports itself to a probe here, as
    /// declared, whether or not the code goes on to read it.
    /// @hidden
    public Clip xanaduSited(int site)
    {
        Clip c = this;
        c.site = site;
        if (probing)
            probeClip(c);
        return c;
    }

    /// Progress without the easing at a reading of its clock, 0..1.
    public float linearAt(float now)
    {
        if (probing)
            probeClip(this);
        float p = (now - start) / max(length, 1e-5);
        p = bounces ? 1.0 - abs(1.0 - clamp(p, 0.0, 2.0)) : saturate(p);
        return backwards ? 1.0 - p : p;
    }

    /// The way out: eased progress at an explicit reading of its clock.
    public float at(float now) { return easing.at(linearAt(now)); }

    /// Progress now without the easing, 0..1.
    public property float linear { get { return linearAt(clock.now); } }

    /// Eased progress now: 0 before it starts, 1 after it ends (overshooting for back and elastic).
    public property float progress { get { return at(clock.now); } }

    /// Whether its clock has reached its start.
    public property bool started { get { return clock.now >= start; } }

    /// Whether its clock has passed its end.
    public property bool finished { get { return clock.now >= end; } }

    /// Whether the clock is inside it now: started and not yet finished.
    public property bool playing { get { return started && !finished; } }

    /// A new clip over the stretch of this one from `from` to `to` (fractions of its span, 0..1),
    /// linear, on the same clock: a fade inside a scene. smoothstep(0.8, 0.2, scene.linear) is
    /// scene.part(0.2, 0.8).eased(eases.smooth).reversed().progress.
    public Clip part(float from, float to) { return Clip(clock, start + from * span, (to - from) * span); }

    /// From one value to another by its progress.
    public float mixed(float from, float to) { return lerp(from, to, progress); }
    public float2 mixed(float2 from, float2 to) { return lerp(from, to, progress); }
    public float3 mixed(float3 from, float3 to) { return lerp(from, to, progress); }
    public float4 mixed(float4 from, float4 to) { return lerp(from, to, progress); }
};

/// What keys can hold: values that mix by a fraction.
public interface IMixable
{
    This mixedTo(This to, float t);
};

public extension float : IMixable { public float mixedTo(float to, float t) { return lerp(this, to, t); } };
public extension float2 : IMixable { public float2 mixedTo(float2 to, float t) { return lerp(this, to, t); } };
public extension float3 : IMixable { public float3 mixedTo(float3 to, float t) { return lerp(this, to, t); } };
public extension float4 : IMixable { public float4 mixedTo(float4 to, float t) { return lerp(this, to, t); } };

/// Values at points along a clip's progress, up to 8. Each key's easing shapes
/// the stretch arriving at it.
///
///     Keys<float2> path = Keys<float2>(float2(-180, 60))
///         .to(0.5, float2(0, -90), eases.outQuad)
///         .to(1, float2(180, 60), eases.inQuad);
///     dot.move(path.at(trip));
public struct Keys<T : IMixable>
{
    public static const int MAX = 8;

    internal T values[MAX];
    internal float places[MAX];
    internal int easings[MAX];
    internal int count;

    /// The value at progress 0.
    public __init(T first)
    {
        for (int k = 0; k < MAX; k++)
        {
            values[k] = first;
            places[k] = 0.0;
            easings[k] = 0;
        }
        count = 1;
    }

    /// A copy with one more key at progress `at`, reached linearly.
    public Keys<T> to(float at, T value) { return to(at, value, Ease(0)); }

    /// A copy with one more key at progress `at`, reached with `easing`.
    public Keys<T> to(float at, T value, Ease easing)
    {
        Keys<T> k = this;
        if (count < MAX)
        {
            k.values[count] = value;
            k.places[count] = at;
            k.easings[count] = easing.id;
            k.count = count + 1;
        }
        return k;
    }

    /// The value at a progress: the first key's before it, the last key's after.
    public T at(float progress)
    {
        T value = values[0];
        for (int k = 1; k < MAX; k++)
        {
            if (k >= count)
                break;
            float from = places[k - 1];
            float span = max(places[k] - from, 1e-5);
            float t = ease.by(easings[k], saturate((progress - from) / span));
            if (progress <= places[k] || k == count - 1)
                return values[k - 1].mixedTo(values[k], t);
        }
        return value;
    }

    /// The value at a clip's progress.
    public T at(Clip clip)
    {
        if (probing)
            for (int k = 1; k < MAX && k < count; k++)
                probeKey(clip.site, places[k]);
        return at(clip.progress);
    }
};

// The probe's side (Slang/ProbeNodes.cs). A probe pass turns `probing` on and calls the stack's functions; each clip
// read meanwhile adds itself once to `probedClips`, two float4 a clip: (start, length, its clock's loop, easing) and
// (beats 1 + bounced 2 + reversed 4, what its clock counts from, its site, 0); each key read at one adds (site, place)
// to `probedKeys`. All of it is the thread's own: the pass copies it out.

/// Whether a probe pass is running the stack's functions now.
/// @hidden
public static bool probing = false;

/// Clips the probe has room for, and keys.
/// @hidden
public static const uint PROBED_CLIPS = 16;
/// @hidden
public static const uint PROBED_KEYS = 16;

/// @hidden
public static float4 probedClips[PROBED_CLIPS * 2];
/// @hidden
public static uint probedClipCount = 0;
/// @hidden
public static float2 probedKeys[PROBED_KEYS];
/// @hidden
public static uint probedKeyCount = 0;

/// Adds a clip to what the probe saw, once.
/// @hidden
public void probeClip(Clip clip)
{
    float4 shape = float4(clip.start, clip.length, clip.clock.loop, float(clip.easing.id));
    float4 kind = float4(float((clip.clock.inBeats ? 1 : 0) + (clip.bounces ? 2 : 0) + (clip.backwards ? 4 : 0)),
        float(clip.clock.counting ? clip.clock.counts : -1), float(clip.site), 0.0);
    for (uint k = 0; k < probedClipCount; k++)
        if (all(probedClips[k * 2] == shape) && all(probedClips[k * 2 + 1] == kind))
            return;
    if (probedClipCount >= PROBED_CLIPS)
        return;
    probedClips[probedClipCount * 2] = shape;
    probedClips[probedClipCount * 2 + 1] = kind;
    probedClipCount++;
}

/// Adds a key's place on the clip from `site` to what the probe saw, once.
/// @hidden
public void probeKey(int site, float place)
{
    float2 key = float2(float(site), place);
    for (uint k = 0; k < probedKeyCount; k++)
        if (all(probedKeys[k] == key))
            return;
    if (probedKeyCount < PROBED_KEYS)
        probedKeys[probedKeyCount++] = key;
}
