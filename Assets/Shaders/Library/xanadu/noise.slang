// xanadu.noise: smooth randomness. Each kind of noise is a value, centred then
// placed like a gradient (it *is* one: 0..1 at every point), so it colours,
// paints, rotates and scrolls the same way:
//
//     c.paint(Perlin(80), colours.black, colours.white);                  // cloudy grey
//     let clouds = Simplex(200).fractal(5).evolved(time * 0.2);            // fbm, drifting in place
//     c.paint(clouds.moved(float2(time * 20, 0)), colours.navy, colours.white);
//     float marble = Perlin(120).warped(Simplex(300).fractal(4), 80).amount();   // domain warp
//     float rock = Simplex(60).ridged(6).amount();                          // mountain ridges
//     float spots = Worley(40).amount();                                     // cellular
//
// The kinds: ValueNoise (blocky-smooth), Perlin (gradient noise), Simplex
// (cheaper, fewer grid marks) and Worley (distance to scattered points). `size`
// is how big one feature is, in the units of the position (pixels). Each runs
// in 3D underneath: `phase` is the third axis, in features, so evolved(time)
// changes it in place where moved(…) slides it. Any of them stacks into octaves
// with fractal (fbm), ridged or billowed, and bends another with warped.
//
// The plain functions under `noise.` take a point in features (no size) and
// give 0..1 too: noise.perlin(p), noise.simplex(float3(p, time)), noise.fbm(p, 5),
// noise.curl(p, time) for a swirling flow. Cells, their walls and ids are
// pattern.cells (xanadu.pattern).
module noise;
import xanadu.math;
import xanadu.stage;
import xanadu.shape;
import xanadu.gradient;
import xanadu.pattern;

/// A noise: a gradient (0..1 at every point, placed like a shape) that also changes in place
/// along `phase`, its third axis, in features. Stacks into octaves (fractal, ridged, billowed)
/// and bends others (warped).
/// @fits float colour
public interface INoise : IGradient
{
    /// How far along its third axis it is, in features: change it to make the noise boil in place.
    public property float phase { get; set; }

    /// Its value at `p` in its own space, -1..1 around 0 (what octaves add up): local is this × 0.5 + 0.5.
    public float signedAt(float2 p);
}

// ---- The kinds --------------------------------------------------------------

/// Value noise: a random level per lattice point, smoothly blended between. Softer and blockier
/// than Perlin; the cheapest. `size` is the lattice spacing.
/// @cost 3
public struct ValueNoise : INoise
{
    public Placement placement = Placement();
    public float size;
    public float phase = 0.0;
    public float seed = 0.0;

    public __init(float size, float seed = 0.0) { this.size = size; this.seed = seed; }

    public float signedAt(float2 p) { return noise.value(float3(p / max(size, 1e-5), phase) + noise.offsetOf(seed)) * 2.0 - 1.0; }
    public float local(float2 p) { return signedAt(p) * 0.5 + 0.5; }
}

/// Perlin's gradient noise: smooth hills and hollows about `size` across, with a random slope at
/// each lattice point (improved Perlin, quintic fade). The shadertoy staple.
/// @cost 4
public struct Perlin : INoise
{
    public Placement placement = Placement();
    public float size;
    public float phase = 0.0;
    public float seed = 0.0;

    public __init(float size, float seed = 0.0) { this.size = size; this.seed = seed; }

    public float signedAt(float2 p) { return noise.perlin(float3(p / max(size, 1e-5), phase) + noise.offsetOf(seed)) * 2.0 - 1.0; }
    public float local(float2 p) { return signedAt(p) * 0.5 + 0.5; }
}

/// Simplex noise (Perlin's later design, Gustavson's form): like Perlin, on triangles instead of
/// squares, so it shows no grid lines and costs less per octave.
/// @cost 4
public struct Simplex : INoise
{
    public Placement placement = Placement();
    public float size;
    public float phase = 0.0;
    public float seed = 0.0;

    public __init(float size, float seed = 0.0) { this.size = size; this.seed = seed; }

    public float signedAt(float2 p) { return noise.simplex(float3(p / max(size, 1e-5), phase) + noise.offsetOf(seed)) * 2.0 - 1.0; }
    public float local(float2 p) { return signedAt(p) * 0.5 + 0.5; }
}

/// Worley's cellular noise: how far the nearest of points scattered about `size` apart is, 0 on
/// a point to 1 a whole `size` away (held there). `jitter` scatters them (0 a square grid);
/// `phase` turns each round its home (a whole turn every TAU). Inverted (1 - amount) it is bubbles.
/// @cost 6
public struct Worley : INoise
{
    public Placement placement = Placement();
    public float size;
    public float jitter;
    public float phase = 0.0;

    public __init(float size, float jitter = 1.0) { this.size = size; this.jitter = jitter; }

    public float local(float2 p) { return saturate(pattern.cells(p, size, jitter, phase).nearest / max(size, 1e-5)); }
    public float signedAt(float2 p) { return local(p) * 2.0 - 1.0; }
}

// ---- Built from others --------------------------------------------------------

/// What octaves do with each layer: added as they are (fbm), folded into ridges, or folded into billows.
public enum Octaves
{
    /// Added as they are: fractal Brownian motion, clouds and terrain.
    Plain,
    /// 1 - |n|, squared, each layer weighted by the one before: sharp mountain ridges (Musgrave).
    Ridged,
    /// |n|: puffy rounded billows with creases between (turbulence).
    Billowed,
};

/// A noise stacked in `octaves` layers, each `lacunarity` times finer and `gain` times fainter
/// (fbm). Each layer is turned and shifted so their grids don't line up. Made by n.fractal(…),
/// n.ridged(…) and n.billowed(…).
/// @cost 4
public struct Fractal<N : INoise> : INoise
{
    public Placement placement = Placement();
    public N noise;
    public int octaves;
    public float gain;
    public float lacunarity;
    public Octaves kind;

    public property float phase
    {
        get { return noise.phase; }
        set { noise.phase = newValue; }
    }

    public float signedAt(float2 p)
    {
        // The inner noise's own placement still applies; then each octave scales up round it.
        float2 q = noise.placement.toLocal(p);
        float sum = 0.0, weight = 0.0, amplitude = 1.0, frequency = 1.0, previous = 1.0;
        N layer = noise;
        layer.placement = Placement();
        float basePhase = noise.phase;
        for (int o = 0; o < min(octaves, 12); o++)
        {
            layer.phase = basePhase * frequency + float(o) * 7.31;
            float n = layer.signedAt(q * frequency);
            if (kind == Octaves.Ridged)
            {
                n = 1.0 - abs(n);
                n *= n;
                float v = n * previous;
                previous = saturate(n * 2.0);
                n = v * 2.0 - 1.0;
            }
            else if (kind == Octaves.Billowed)
                n = abs(n) * 2.0 - 1.0;
            sum += n * amplitude;
            weight += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
            // Turned about 37° and shifted, so no two layers share a grid.
            q = float2(0.8 * q.x - 0.6 * q.y, 0.6 * q.x + 0.8 * q.y) + float2(17.13, -31.7) * frequency;
        }
        return weight > 0.0 ? sum / weight : 0.0;
    }

    public float local(float2 p) { return saturate(signedAt(p) * 0.5 + 0.5); }
}

/// A noise read at a point pushed about by another noise: `amount` units along x and y by two
/// readings of `by` (Quilez's domain warping: marble, smoke, oil). Made by n.warped(by, amount).
/// @cost 2
public struct Warped<N : INoise, W : INoise> : INoise
{
    public Placement placement = Placement();
    public N noise;
    public W by;
    public float amount;

    public property float phase
    {
        get { return noise.phase; }
        set { noise.phase = newValue; by.phase = newValue; }
    }

    public float signedAt(float2 p)
    {
        // Two readings of the same warp far apart: independent pushes along x and y.
        float2 push = float2(by.signedAt(by.placement.toLocal(p)),
                             by.signedAt(by.placement.toLocal(p + float2(5200.0, 1300.0))));
        return noise.signedAt(noise.placement.toLocal(p + push * amount));
    }

    public float local(float2 p) { return saturate(signedAt(p) * 0.5 + 0.5); }
}

// ---- What every noise can do ----------------------------------------------------

public extension<N : INoise> N
{
    /// Moves it `amount` features along its third axis: changes it in place (evolve(time) boils it).
    [mutating] public void evolve(float amount) { phase = phase + amount; }

    /// A copy moved `amount` features along its third axis: n.evolved(time * 0.3) changes in place.
    public N evolved(float amount) { N r = this; r.evolve(amount); return r; }

    /// Its value at `position`, -1..1 around 0.
    public float signed(float2 position) { return signedAt(placement.toLocal(position)); }

    /// Its value at the pixel being shaded, -1..1 around 0, from the quad's centre.
    public float signed() { return signed(shading.canvas.centred); }

    /// Stacked in `octaves` layers, each `lacunarity` times finer and `gain` times fainter: fbm.
    public Fractal<N> fractal(int octaves = 5, float gain = 0.5, float lacunarity = 2.0)
    {
        Fractal<N> f;
        f.placement = Placement();
        f.noise = this;
        f.octaves = octaves;
        f.gain = gain;
        f.lacunarity = lacunarity;
        f.kind = Octaves.Plain;
        return f;
    }

    /// Stacked into sharp ridges (1 - |n|, each layer weighted by the one before): mountains, veins, lightning.
    public Fractal<N> ridged(int octaves = 5, float gain = 0.5, float lacunarity = 2.0)
    {
        Fractal<N> f = fractal(octaves, gain, lacunarity);
        f.kind = Octaves.Ridged;
        return f;
    }

    /// Stacked into puffy billows (|n| per layer): turbulence, smoke, fire.
    public Fractal<N> billowed(int octaves = 5, float gain = 0.5, float lacunarity = 2.0)
    {
        Fractal<N> f = fractal(octaves, gain, lacunarity);
        f.kind = Octaves.Billowed;
        return f;
    }

    /// Read at points pushed up to `amount` units by `by` (domain warping).
    public Warped<N, W> warped<W : INoise>(W by, float amount)
    {
        Warped<N, W> w;
        w.placement = Placement();
        w.noise = this;
        w.by = by;
        w.amount = amount;
        return w;
    }
}

// ---- The plain functions --------------------------------------------------------

/// Noise at a point given in features (one lattice step is 1), each 0..1: for loops, formulas and ports.
public namespace noise
{
    /// A seed as a far-off place in the lattice, so seeds give unrelated noise. Its third axis sits halfway between lattice planes: gradient noise is 0 on every lattice point,
    /// so a slice through them shows the grid (worst in anything that reads the slope, like curl).
    /// @hidden
    public float3 offsetOf(float seed)
    {
        float3 r = seed == 0.0 ? float3(0.0) : floor((random3(float2(seed, 0.618)) - 0.5) * 512.0);
        return r + float3(0.0, 0.0, 0.5);
    }

    /// Perlin's quintic fade, 6t⁵ - 15t⁴ + 10t³: smooth to the second derivative at both ends.
    /// @hidden
    public float2 faded(float2 t) { return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }

    /// @hidden
    public float3 faded(float3 t) { return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }

    /// Value noise, 0..1: a random level at each whole point, blended smoothly.
    public float value(float2 p)
    {
        float2 i = floor(p), f = p - i;
        float2 u = faded(f);
        return lerp(lerp(random(i), random(i + float2(1, 0)), u.x),
                    lerp(random(i + float2(0, 1)), random(i + float2(1, 1)), u.x), u.y);
    }

    /// Value noise in 3D, 0..1: float3(p, time) for 2D noise that changes in place.
    public float value(float3 p)
    {
        float3 i = floor(p), f = p - i;
        float3 u = faded(f);
        float a = lerp(lerp(random(i), random(i + float3(1, 0, 0)), u.x),
                       lerp(random(i + float3(0, 1, 0)), random(i + float3(1, 1, 0)), u.x), u.y);
        float b = lerp(lerp(random(i + float3(0, 0, 1)), random(i + float3(1, 0, 1)), u.x),
                       lerp(random(i + float3(0, 1, 1)), random(i + float3(1, 1, 1)), u.x), u.y);
        return lerp(a, b, u.z);
    }

    /// Perlin (gradient) noise, 0..1 with 0.5 the middle: a random slope at each whole point.
    public float perlin(float2 p)
    {
        float2 i = floor(p), f = p - i;
        float2 u = faded(f);
        float a = dot(randomDirection(i), f);
        float b = dot(randomDirection(i + float2(1, 0)), f - float2(1, 0));
        float c = dot(randomDirection(i + float2(0, 1)), f - float2(0, 1));
        float d = dot(randomDirection(i + float2(1, 1)), f - float2(1, 1));
        // Unit slopes reach ±√½ at most: scaled to ±1, then to 0..1.
        return saturate(lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 1.41421356 * 0.5 + 0.5);
    }

    /// Perlin noise in 3D, 0..1: float3(p, time) for 2D noise that changes in place.
    public float perlin(float3 p)
    {
        float3 i = floor(p), f = p - i;
        float3 u = faded(f);
        float n000 = dot(randomDirection(i), f);
        float n100 = dot(randomDirection(i + float3(1, 0, 0)), f - float3(1, 0, 0));
        float n010 = dot(randomDirection(i + float3(0, 1, 0)), f - float3(0, 1, 0));
        float n110 = dot(randomDirection(i + float3(1, 1, 0)), f - float3(1, 1, 0));
        float n001 = dot(randomDirection(i + float3(0, 0, 1)), f - float3(0, 0, 1));
        float n101 = dot(randomDirection(i + float3(1, 0, 1)), f - float3(1, 0, 1));
        float n011 = dot(randomDirection(i + float3(0, 1, 1)), f - float3(0, 1, 1));
        float n111 = dot(randomDirection(i + float3(1, 1, 1)), f - float3(1, 1, 1));
        float n = lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                       lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
        // Unit slopes reach ±√¾ at most.
        return saturate(n * 1.15470054 * 0.5 + 0.5);
    }

    /// Simplex noise, 0..1 with 0.5 the middle (Gustavson's 2D form).
    public float simplex(float2 p)
    {
        const float skew = 0.366025404;    // (√3 - 1) / 2
        const float unskew = 0.211324865;  // (3 - √3) / 6
        float2 i = floor(p + (p.x + p.y) * skew);
        float2 a = p - i + (i.x + i.y) * unskew;
        float2 o = a.x > a.y ? float2(1, 0) : float2(0, 1);
        float2 b = a - o + unskew;
        float2 c = a - 1.0 + 2.0 * unskew;
        float3 h = max(0.5 - float3(dot(a, a), dot(b, b), dot(c, c)), 0.0);
        float3 n = h * h * h * h * float3(dot(a, randomDirection(i)), dot(b, randomDirection(i + o)), dot(c, randomDirection(i + 1.0)));
        // Peaks near ±0.0101 (measured): scaled to ±1, then to 0..1.
        return saturate(dot(n, float3(98.0)) * 0.5 + 0.5);
    }

    /// Simplex noise in 3D, 0..1: float3(p, time) for 2D noise that changes in place.
    public float simplex(float3 p)
    {
        const float skew = 1.0 / 3.0;
        const float unskew = 1.0 / 6.0;
        float3 s = floor(p + dot(p, float3(skew)));
        float3 x = p - s + dot(s, float3(unskew));
        float3 e = step(float3(0.0), x - x.yzx);
        float3 i1 = e * (1.0 - e.zxy);
        float3 i2 = 1.0 - e.zxy * (1.0 - e);
        float3 x1 = x - i1 + unskew;
        float3 x2 = x - i2 + 2.0 * unskew;
        float3 x3 = x - 1.0 + 3.0 * unskew;
        // A kernel radius² of 0.5, not Gustavson's 0.6: 0.6 reaches past the simplex's neighbours and
        // leaves seams at cell boundaries (they show in anything that reads its slope, like curl).
        float4 w = max(0.5 - float4(dot(x, x), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
        float4 d = float4(dot(randomDirection(s), x), dot(randomDirection(s + i1), x1),
                          dot(randomDirection(s + i2), x2), dot(randomDirection(s + 1.0), x3));
        w *= w;
        w *= w;
        // Peaks near ±0.0092 (measured).
        return saturate(dot(d, w) * 105.0 * 0.5 + 0.5);
    }

    /// Fractal Brownian motion over Perlin noise, 0..1: `octaves` layers, each twice as fine and half as strong.
    public float fbm(float2 p, int octaves = 5, float gain = 0.5)
    {
        float sum = 0.0, weight = 0.0, amplitude = 1.0;
        for (int o = 0; o < min(octaves, 12); o++)
        {
            sum += (perlin(p) * 2.0 - 1.0) * amplitude;
            weight += amplitude;
            amplitude *= gain;
            p = float2(0.8 * p.x - 0.6 * p.y, 0.6 * p.x + 0.8 * p.y) * 2.0 + float2(17.13, -31.7);
        }
        return saturate(sum / weight * 0.5 + 0.5);
    }

    /// Fractal Brownian motion over 3D Perlin noise, 0..1: float3(p, time) changes in place.
    public float fbm(float3 p, int octaves = 5, float gain = 0.5)
    {
        float sum = 0.0, weight = 0.0, amplitude = 1.0;
        for (int o = 0; o < min(octaves, 12); o++)
        {
            sum += (perlin(p) * 2.0 - 1.0) * amplitude;
            weight += amplitude;
            amplitude *= gain;
            p = float3(0.8 * p.x - 0.6 * p.y, 0.6 * p.x + 0.8 * p.y, p.z) * 2.0 + float3(17.13, -31.7, 5.3);
        }
        return saturate(sum / weight * 0.5 + 0.5);
    }

    /// Ridged fractal noise, 0..1 (Musgrave): sharp crests where the noise crosses its middle.
    public float ridged(float2 p, int octaves = 5, float gain = 0.5)
    {
        float sum = 0.0, weight = 0.0, amplitude = 1.0, previous = 1.0;
        for (int o = 0; o < min(octaves, 12); o++)
        {
            float n = 1.0 - abs(perlin(p) * 2.0 - 1.0);
            n *= n;
            sum += n * previous * amplitude;
            previous = saturate(n * 2.0);
            weight += amplitude;
            amplitude *= gain;
            p = float2(0.8 * p.x - 0.6 * p.y, 0.6 * p.x + 0.8 * p.y) * 2.0 + float2(17.13, -31.7);
        }
        return saturate(sum / weight);
    }

    /// Worley's cellular noise: how far the nearest point scattered one per whole tile is (F1),
    /// in tiles (0 on a point, rarely past 1). pattern.cells has the rest (F2, ids, walls).
    public float worley(float2 p, float jitter = 1.0) { return pattern.cells(p, 1.0, jitter).nearest; }

    /// Quilez's domain warp, 0..1: fbm read at a point pushed by fbm read at a point pushed by fbm,
    /// `amount` features each time. The swirled-marble look.
    public float warped(float2 p, float amount = 4.0, int octaves = 4)
    {
        float2 q = float2(fbm(p, octaves), fbm(p + float2(5.2, 1.3), octaves)) * 2.0 - 1.0;
        float2 r = float2(fbm(p + amount * q + float2(1.7, 9.2), octaves), fbm(p + amount * q + float2(8.3, 2.8), octaves)) * 2.0 - 1.0;
        return fbm(p + amount * r, octaves);
    }

    /// A swirling flow with no sources or sinks (curl noise, Bridson): the direction to move at
    /// `p` (in features) so particles, smoke or a warp eddy without bunching up. Roughly unit
    /// length; `phase` changes it in place.
    public float2 curl(float2 p, float phase = 0.0)
    {
        // The curl of a scalar field: its gradient turned a quarter, by central differences.
        const float h = 0.01;
        phase += 0.5;   // off the lattice planes (see offsetOf)
        float dx = simplex(float3(p + float2(h, 0), phase)) - simplex(float3(p - float2(h, 0), phase));
        float dy = simplex(float3(p + float2(0, h), phase)) - simplex(float3(p - float2(0, h), phase));
        return float2(dy, -dx) / (2.0 * h) * 0.25;
    }

    /// Two independent noises at once (x and y), each -1..1: a wobble to add to a position.
    /// p + noise.wobble(p / 40 + time) * 6 makes a line shiver.
    public float2 wobble(float2 p)
    {
        return float2(simplex(p), simplex(p + float2(31.4, 47.2))) * 2.0 - 1.0;
    }
}
