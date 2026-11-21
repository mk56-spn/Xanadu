// xanadu.looks: ready-made pieces for level backgrounds and hit effects, each
// built from the rest of the library and small enough to read and change.
// Shapes draw like any shape; the rest under `looks.` give a 0..1 amount or a
// colour at a point, so they layer with paint:
//
//     colour c = looks.starfield(canvas.centred, 0.6);
//     c.paint(looks.synthwaveFloor(canvas.centred, 0.1, time), colours.hotPink, blend.add);
//     c.paint(Sunburst(16).rotated(time * 0.2), colours.white.withAlpha(0.08));
//     c.paint(Lightning(a, b, 3, floor(time * 12)).haloed(30), colours.cyan, blend.add);
//     c.paint(looks.burst(canvas.centred, clock.since(events.hit).seconds, 7), colours.gold, blend.add);
//
// Distances and sizes are in pixels, like shapes; `time` is read where a look moves.
module looks;
import xanadu.math;
import xanadu.stage;
import xanadu.shape;
import xanadu.colour;
import xanadu.pattern;
import xanadu.noise;
import xanadu.ease;

/// A jagged bolt from `a` to `b`, `width` thick: `segments` straight pieces whose joins are
/// knocked sideways by up to `jag` of its length, the most in the middle. A new `seed` is a new
/// bolt: floor(time * 12) re-strikes it twelve times a second. Draw with haloed for the glow.
/// @cost 8
public struct Lightning : IShape
{
    public Placement placement = Placement();
    public float2 a;
    public float2 b;
    public float width;
    public float seed;
    public float jag = 0.12;
    public int segments = 12;

    public __init(float2 a, float2 b, float width, float seed = 0.0, float jag = 0.12)
    {
        this.a = a; this.b = b; this.width = width; this.seed = seed; this.jag = jag;
    }

    /// The bolt's point `t` (0..1) along it: knocked sideways by smooth noise, pinned at both ends.
    public float2 pointAt(float t)
    {
        float2 along = b - a;
        float2 side = float2(-along.y, along.x);
        // Two octaves of value noise along the bolt: big kinks and small ones.
        float n = (noise.value(float2(t * 4.0, seed * 7.31)) - 0.5) + 0.5 * (noise.value(float2(t * 11.0, seed * 3.17 + 40.0)) - 0.5);
        return a + along * t + side * n * jag * 2.0 * sin(PI * t);
    }

    public float local(float2 p)
    {
        int n = clamp(segments, 2, 32);
        float d = 1e10;
        float2 previous = a;
        for (int k = 1; k <= n; k++)
        {
            float2 next = pointAt(float(k) / float(n));
            float2 pa = p - previous, ba = next - previous;
            float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-9));
            d = min(d, length(pa - ba * h));
            previous = next;
        }
        // Thinner towards the far end, as a strike tapers.
        return d - width * 0.5;
    }
}

/// Rays out from the centre: `rays` wedges, each `fill` of its share of the turn (0.5: rays and
/// gaps alike), the retro sunburst behind a title. Rotate it to turn them. A shape.
/// @cost 2
public struct Sunburst : IShape
{
    public Placement placement = Placement();
    public float rays;
    public float fill;

    public __init(float rays, float fill = 0.5) { this.rays = rays; this.fill = fill; }

    public float local(float2 p)
    {
        float share = TAU / max(rays, 1.0);
        float a = atan2(p.y, p.x);
        // Angle from the nearest ray's middle, then turned into pixels at this distance out.
        float off = abs(a - share * round(a / share));
        float half = share * saturate(fill) * 0.5;
        return (off - half) * length(p);
    }
}

public namespace looks
{
    /// Stars on black in `layers` depths (up to 6), nearer ones bigger, brighter and moving faster
    /// as `drift` (pixels a second) slides them; each twinkles. `density` 0..1 is how many. Each
    /// layer looks in the 3×3 cells round the pixel, so no glow is cut off at a cell's edge.
    /// @cost 36
    public colour starfield(float2 position, float density = 0.5, int layers = 4, float2 drift = float2(-20.0, 0.0))
    {
        float3 light = float3(0.0);
        for (int l = 0; l < min(layers, 6); l++)
        {
            float depth = 1.0 + float(l) * 0.8;               // 1 near, larger far
            float cell = 90.0 / depth;
            Tile t = pattern.tile(position + drift * time / depth + float2(l * 131.7, l * 71.3), cell);
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 id = t.id + float2(x, y);
                    float3 r = random3(id + float(l) * 17.0);
                    if (r.x > density)
                        continue;
                    float2 star = float2(x, y) * cell + (r.yz - 0.5) * cell * 0.8;
                    float d = length(t.local - star);
                    float size = (1.6 - r.y * 0.8) / depth * 2.0;
                    float twinkle = 0.65 + 0.35 * sin(time * (2.0 + r.z * 4.0) + r.y * TAU);
                    // Gone within a cell, so the 3×3 search sees every star that reaches here.
                    float glow = (size / max(d, 0.3)) * (size / max(d, 0.3)) * (1.0 - smoothstep(cell * 0.5, cell, d));
                    float3 tone = lerp(float3(0.75, 0.85, 1.0), float3(1.0, 0.9, 0.75), r.z);
                    light += tone * saturate(glow) * twinkle / depth * 1.6;
                }
        }
        return colour(saturate(light), 1.0);
    }

    /// The glowing grid floor of a synthwave sunset, 0..1: lines in perspective below `horizon`
    /// (a fraction of the quad's height down from its centre: 0 the middle), rolling towards you as
    /// `travel` grows (pass time). Fades into the distance.
    /// @cost 3
    public float synthwaveFloor(float2 position, float horizon = 0.1, float travel = 0.0, float lineWidth = 0.03)
    {
        float h = shading.canvas.size.y;
        float y = position.y / h - horizon;   // 0 on the horizon, growing downwards
        if (y <= 0.002)
            return 0.0;
        // Into the floor: depth is 1 / height below the horizon, sideways scaled by it too.
        float depth = 0.25 / y;
        float2 floorAt = float2(position.x / h * depth * 4.0, depth + travel);
        float2 off = abs(frac(floorAt + 0.5) - 0.5);
        // Lines a fixed width on screen: divided by how fast the floor point moves per pixel.
        float2 w = max(fwidth(floorAt), float2(1e-5)) * 1.2 + lineWidth * float2(1.0, 0.2);
        float2 lines = 1.0 - smoothstep(float2(0.0), w, off);
        return max(lines.x, lines.y) * smoothstep(0.0, 0.25, y);
    }

    /// Curtains of an aurora, 0..1: ridged noise stretched tall, swaying, bright along its lower
    /// edge and fading upwards; `y` (a fraction of the quad's height from its centre) is where the
    /// curtain hangs. Colour it with a gradient (green into purple).
    /// @cost 10
    public float aurora(float2 position, float y = -0.1)
    {
        float h = shading.canvas.size.y;
        float2 p = position / h;
        // The curtain's bottom edge wanders with slow noise.
        float edge = y + (noise.simplex(float2(p.x * 1.5, time * 0.1)) - 0.5) * 0.3;
        float above = edge - p.y;
        if (above < -0.05)
            return 0.0;
        // Vertical folds: ridged noise across, barely changing up the curtain.
        float folds = noise.ridged(float2(p.x * 6.0 + noise.simplex(float2(p.x * 2.0, time * 0.15)) * 2.0, time * 0.05), 3);
        float fade = exp(-max(above, 0.0) * 5.0) * smoothstep(-0.05, 0.02, above);
        return saturate(folds * folds * fade * 1.6);
    }

    /// Light rippling on a pool's floor, 0..1: two layers of drifting Voronoi walls about `size`
    /// across, multiplied so only where both are bright shines (caustics).
    /// @cost 14
    public float caustics(float2 position, float size = 80.0)
    {
        Cell a = pattern.cells(position + float2(time * 9.0, time * 4.0), size, 1.0, time * 0.9);
        Cell b = pattern.cells(position * 1.3 - float2(time * 6.0, -time * 7.0) + 300.0, size, 1.0, time * 1.1 + 2.0);
        float la = exp(-a.edge / (size * 0.08));
        float lb = exp(-b.edge / (size * 0.08));
        return saturate(la * 0.6 + lb * 0.4 + la * lb * 2.0) * 0.8;
    }

    /// Falling rain, 0..1: thin streaks `length` pixels long in `density` 0..1 of the columns,
    /// slanted by `angle` radians, falling at `speed` pixels a second.
    /// @cost 4
    public float rain(float2 position, float density = 0.3, float angle = 0.2, float speed = 900.0, float length = 40.0)
    {
        float2 p = rotate(position, -angle);
        float column = 6.0;
        float id = floor(p.x / column);
        float3 r = random3(float2(id, 3.7));
        if (r.x > density)
            return 0.0;
        float y = p.y - time * speed * (0.7 + 0.6 * r.y) + r.z * 997.0;
        float along = frac(y / (length * 4.0)) * 4.0;   // 0..4: a streak in the first quarter
        float streak = along < 1.0 ? along : 0.0;
        float across = 1.0 - smoothstep(0.3, 1.2, abs(p.x - (id + 0.5) * column));
        return streak * across * (0.4 + 0.6 * r.y);
    }

    /// Falling snow, 0..1: soft flakes in three depths, drifting side to side.
    /// @cost 9
    public float snow(float2 position, float density = 0.5, float speed = 60.0)
    {
        float s = 0.0;
        for (int l = 0; l < 3; l++)
        {
            float depth = 1.0 + float(l);
            float cell = 70.0 / depth;
            float2 p = position - float2(sin(time * 0.6 + float(l)) * 20.0, time * speed / depth) + float(l) * 113.0;
            Tile t = pattern.tile(p, cell);
            float3 r = random3(t.id + float(l) * 31.0);
            if (r.x > density)
                continue;
            float2 flake = (r.yz - 0.5) * cell * 0.6 + float2(sin(time * 2.0 + r.y * TAU) * cell * 0.15, 0.0);
            float radius = (3.0 - r.z) / depth;
            s += 1.0 - smoothstep(radius * 0.3, radius, length(t.local - flake));
        }
        return saturate(s);
    }

    /// A firework burst `age` seconds after it went off (pass clock.since(…).seconds, or a
    /// clip's time), 0..1: `sparks` streaks flying out from the centre, slowing, falling and
    /// fading over about 1.5 seconds. `seed` changes the spread.
    /// @cost 24
    public float burst(float2 position, float age, float seed = 0.0, int sparks = 24, float reach = 180.0)
    {
        if (age < 0.0 || age > 2.0)
            return 0.0;
        float lit = 0.0;
        // Out fast, then easing off (drag), and pulled down (gravity).
        float travelled = reach * (1.0 - exp(-age * 3.0));
        float fall = 60.0 * age * age;
        float fade = saturate(1.0 - age / 1.6);
        for (int k = 0; k < min(sparks, 48); k++)
        {
            float3 r = random3(float2(float(k), seed));
            float a = (float(k) + r.x * 0.6) * TAU / float(sparks);
            float2 dir = float2(cos(a), sin(a)) * (0.7 + 0.3 * r.y);
            float2 head = dir * travelled + float2(0.0, fall);
            float2 tail = dir * travelled * 0.75 + float2(0.0, fall * 0.8);
            float2 pa = position - tail, ba = head - tail;
            float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
            float d = length(pa - ba * h);
            lit += (1.0 - smoothstep(0.5, 2.5, d)) * (0.3 + 0.7 * h);
        }
        return saturate(lit) * fade;
    }

    /// A hit's ring, 0..1: it bursts out from the centre to `radius` px over `length` seconds after
    /// it happened (`age`: clock.since(events.hit).seconds), thinning and fading as it goes.
    /// @cost 2
    public float hitRing(float2 position, float age, float radius = 120.0, float length = 0.4, float width = 10.0)
    {
        if (age < 0.0 || age > length)
            return 0.0;
        float t = age / length;
        float r = radius * ease.outCubic(t);
        float w = width * (1.0 - t * 0.8);
        return Circle(r).stroked(position, w) * (1.0 - t);
    }

    /// Confetti `age` seconds after it was thrown (pass clock.since(…).seconds), 0..1 of paper
    /// at `position`, and the colour of the piece there in `tint` (a hue 0..1): `count` slips
    /// bursting up from the centre, tumbling and fluttering down.
    /// @cost 30
    public float confetti(float2 position, float age, out float tint, float seed = 0.0, int count = 30, float reach = 260.0)
    {
        tint = 0.0;
        if (age < 0.0 || age > 4.0)
            return 0.0;
        float paper = 0.0;
        for (int k = 0; k < min(count, 60); k++)
        {
            float3 r = random3(float2(float(k), seed + 5.0));
            // Thrown up and out, slowed by the air, falling at a flutter's pace.
            float a = -PI * (0.15 + 0.7 * r.x);
            float2 v = float2(cos(a), sin(a)) * reach * (0.6 + 0.6 * r.y);
            float drag = 1.0 - exp(-age * 2.5);
            float2 at = v * drag + float2(sin(age * (3.0 + 4.0 * r.z) + r.x * TAU) * 20.0, 90.0 * age * age * 0.5 + 40.0 * age);
            // A slip spinning about two axes: its width flickers as it turns.
            float spin = age * (6.0 + 8.0 * r.z) + r.y * TAU;
            float2 size = float2(10.0 * abs(cos(spin)) + 1.0, 5.0);
            float d = Box(size, 1.0).rotated(spin * 0.7).moved(at).distance(position);
            float cover = sdf.filled(d) * saturate(4.0 - age);
            if (cover > paper)
            {
                paper = cover;
                tint = r.z;
            }
        }
        return paper;
    }

    /// Anime speed lines, 0..1: thin wedges rushing in from the edges towards `centre` (px from
    /// the quad's centre), leaving it clear within `clear` px. `count` lines, flickering `rate`
    /// times a second.
    /// @cost 3
    public float speedLines(float2 position, float2 centre = float2(0.0), float clear = 120.0, float count = 60.0, float rate = 12.0)
    {
        float2 d = position - centre;
        float a = atan2(d.y, d.x) / TAU;
        float slot = floor(a * count);
        float tick = floor(time * rate);
        float r = random(float2(slot, tick));
        // Each slot shows a line some of the time, a thin wedge across its middle.
        float across = abs(frac(a * count) - 0.5) * 2.0;
        float line = step(0.55, r) * (1.0 - smoothstep(0.0, 0.25 + 0.3 * r, across));
        float reach = clear * (1.0 + r * 0.6);
        return line * smoothstep(reach, reach * 1.4, length(d));
    }

    /// The classic plasma: four sines over the plane, through a rainbow. `scale` is about how big
    /// a blob is in pixels.
    /// @cost 3
    public colour plasma(float2 position, float scale = 120.0)
    {
        float2 p = position / scale;
        float v = sin(p.x + time) + sin((p.y + time) * 0.5) + sin((p.x + p.y + time) * 0.5);
        float2 c = p + 0.5 * float2(sin(time / 5.0), cos(time / 3.0));
        v += sin(length(c) + time);
        return colours.rainbow(v * 0.25);
    }

    /// A sun of the synthwave kind, `radius` round, cut by gaps that thicken towards its bottom,
    /// 0..1. Paint it with a gradient (yellow into pink) for the look.
    /// @cost 3
    public float retroSun(float2 position, float radius = 120.0, float bands = 7.0)
    {
        float disc = Circle(radius).filled(position);
        float y = position.y / radius;                  // -1 top to 1 bottom
        if (y < 0.0)
            return disc;
        float band = frac(y * bands - time * 0.3);
        float gap = y * 0.6;                            // the gaps grow downwards
        return disc * (1.0 - aastep(1.0 - gap, band));
    }
}

/// Moving with the music, from the global `beat` (the song's beats in a level, the timeline's in
/// the shader editor). `every` counts in beats: 1 each beat, 4 each bar of 4/4, 0.5 each half beat.
///
///     dot.scale(1 + 0.2 * beats.pulse());             // a thump on every beat
///     c.paint(Sunburst(12).rotated(beats.index(4) * 0.5), colours.white.withAlpha(0.1));   // a click each bar
public namespace beats
{
    /// How far through the current one it is, 0 on it to 1 just before the next.
    public float phase(float every = 1.0) { return frac(beat / max(every, 1e-3)); }

    /// Which one it is: 0 for the first, counting up.
    public float index(float every = 1.0) { return floor(beat / max(every, 1e-3)); }

    /// A thump on each one: 1 right on it, dying away by about `decay` of the way to the next
    /// (shaping.impulse's curve, peaking just after the beat).
    public float pulse(float every = 1.0, float decay = 0.25)
    {
        float t = phase(every);
        return saturate(exp(-t / max(decay * 0.35, 1e-3)));
    }

    /// 1 for the first `length` of each one (0..1 of it), else 0: a flash on the beat.
    public float flash(float every = 1.0, float length = 0.15) { return phase(every) < length ? 1.0 : 0.0; }

    /// +1 and -1 by turns, one each: a bop left, then right.
    public float alternate(float every = 1.0) { return mod(index(every), 2.0) < 0.5 ? 1.0 : -1.0; }

    /// A smooth sway, -1..1, one whole swing every two: a head nod in time.
    public float sway(float every = 1.0) { return sin(beat / max(every, 1e-3) * PI); }

    /// A spring released on each one: overshoots and settles before the next (shaping.spring),
    /// `frequency` swings per beat.
    public float bounce(float every = 1.0, float frequency = 2.0)
    {
        float t = phase(every) * every;   // in beats since it
        return shaping.spring(t, frequency, 6.0);
    }
}
