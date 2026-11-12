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
// A clock can also run from a play key's last press, so a clip plays each
// time it goes down:
//
//     Clip flash = clock.since(keys.action1).clip(0, 0.25).eased(eases.outQuad);
//
// or from a play event (xanadu.play), so it plays on every hit, miss, dash…:
//
//     Clip pulse = clock.since(events.hit).clip(0, 0.3).eased(eases.outBack);
//
// The shader editor reads the clips out of the code and lays them on a
// timeline under the preview.
module anim;
import xanadu.stage;
import xanadu.math;
import xanadu.ease;
import xanadu.play;

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
}

/// A play key: a direction (S D K L by default) or an action key (F Space J).
/// The presets are under `keys.`.
public struct Key
{
    internal int id;

    public __init(int id) { this.id = id; }

    /// Seconds since it was last pressed; 1e6 when it never has been.
    public property float since
    {
        get
        {
            if (id < 4)
                return directionKeys[id];
            if (id < 7)
                return actionKeys[id - 4];
            if (id == 7)
                return min(min(directionKeys.x, directionKeys.y), min(directionKeys.z, directionKeys.w));
            return min(actionKeys.x, min(actionKeys.y, actionKeys.z));
        }
    }

    /// Whether it was pressed within the last `seconds`.
    public bool pressedWithin(float seconds) { return since < seconds; }
};

/// The play keys, as the game numbers them (rebindable; the defaults in brackets).
public namespace keys
{
    /// S
    public static const Key left = Key(0);
    /// D
    public static const Key right = Key(1);
    /// K
    public static const Key up = Key(2);
    /// L
    public static const Key down = Key(3);
    /// F
    public static const Key action1 = Key(4);
    /// Space
    public static const Key action2 = Key(5);
    /// J
    public static const Key action3 = Key(6);
    /// Whichever direction key was pressed last.
    public static const Key anyDirection = Key(7);
    /// Whichever action key was pressed last.
    public static const Key anyAction = Key(8);
}

/// Where time comes from: seconds (`clock`), song beats (`clock.beats`) or the
/// seconds since a key was pressed (`clock.since(keys.left)`) or an event
/// happened (`clock.since(events.hit)`), running on or wrapping round a loop
/// (`clock.looped(2)`).
public struct Clock
{
    internal bool inBeats;

    /// The key it counts from (Key.id), an event (1000 + Event.id), or -1 for the song's time.
    internal int key;

    /// How long one loop is, in the clock's own units; 0 never wraps.
    internal float loop;

    public __init(bool inBeats, float loop)
    {
        this.inBeats = inBeats;
        this.loop = loop;
        this.key = -1;
    }

    /// The same clock counting song beats instead of seconds.
    public property Clock beats { get { Clock c = this; c.inBeats = true; c.key = -1; return c; } }

    /// A clock counting seconds from a key's last press: 0 as it goes down.
    public Clock since(Key pressed) { Clock c = Clock(false, loop); c.key = pressed.id; return c; }

    /// A clock counting seconds from when an event last happened: 0 as it does.
    public Clock since(Event happened) { Clock c = Clock(false, loop); c.key = 1000 + happened.id; return c; }

    /// A copy that wraps every `length` seconds (or beats).
    public Clock looped(float length) { Clock c = this; c.loop = length; return c; }

    /// The clock's reading before any loop: seconds or beats since the start, or since its key's press.
    public property float total
    {
        get { return key >= 1000 ? Event(key - 1000).since : key >= 0 ? Key(key).since : inBeats ? beat : time; }
    }

    /// Where the clock is now: inside its loop when it has one.
    public property float now { get { return loop > 0.0 ? mod(total, loop) : total; } }

    /// How many loops are done (whole seconds or beats when it doesn't loop).
    public property int count { get { return int(floor(loop > 0.0 ? total / loop : total)); } }

    /// A clip on this clock: `length` long, starting `start` in (seconds or beats).
    public Clip clip(float start, float length) { return Clip(this, start, length); }
};

/// Seconds since the game started, as a clock; `clock.beats` counts song beats.
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

    public __init(Clock clock, float start, float length)
    {
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

    /// Progress without the easing at a reading of its clock, 0..1.
    public float linearAt(float now)
    {
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
    public T at(Clip clip) { return at(clip.progress); }
};
