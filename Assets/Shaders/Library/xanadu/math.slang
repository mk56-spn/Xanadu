// xanadu.math: constants and the GLSL-isms effects reach for.
module math;

public static const float PI = 3.14159265359;
public static const float TAU = 6.28318530718;

/// GLSL's mod: the remainder with the divisor's sign (HLSL's fmod truncates instead).
public float mod(float x, float y) { return x - y * floor(x / y); }
public float2 mod(float2 x, float2 y) { return x - y * floor(x / y); }
public float2 mod(float2 x, float y) { return x - y * floor(x / y); }
public float3 mod(float3 x, float3 y) { return x - y * floor(x / y); }

/// A sine from 0 to 1 instead of -1 to 1: sin(x) * 0.5 + 0.5. 0.5 at x = 0, rising first.
public float sin01(float x) { return sin(x) * 0.5 + 0.5; }

/// A cosine from 0 to 1 instead of -1 to 1: cos(x) * 0.5 + 0.5. 1 at x = 0.
public float cos01(float x) { return cos(x) * 0.5 + 0.5; }

/// A ramp from 0 to 1 every `period`, dropping back to 0: frac(x / period).
public float saw(float x, float period = 1.0) { return frac(x / period); }

/// Up from 0 to 1 and back down every `period`, 0 at x = 0.
public float triangle(float x, float period = 1.0) { return 1.0 - abs(2.0 * frac(x / period) - 1.0); }

/// x moved from the range `from` (low, high) to the range `to`, in proportion; not held
/// inside it: remapped(sin(t), float2(-1, 1), float2(0.25, 0.75)).
public float remapped(float x, float2 from, float2 to)
{
    return to.x + (x - from.x) / (from.y - from.x) * (to.y - to.x);
}

public extension float
{
    /// Scales how far it is from 0.5: 0 flat 0.5, 1 as it is, above 1 punchier (as a colour's contrast).
    [mutating] public void contrast(float factor) { this = (this - 0.5) * factor + 0.5; }

    /// A copy scaled in how far it is from 0.5: 0 flat 0.5, 1 as it is, above 1 punchier.
    public float contrasted(float factor) { return (this - 0.5) * factor + 0.5; }
}

/// p turned round the origin by angle radians, clockwise on screen (y down), as shapes turn.
public float2 rotate(float2 position, float angle)
{
    float c = cos(angle), s = sin(angle);
    return float2(c * position.x - s * position.y, s * position.x + c * position.y);
}

public extension float2
{
    /// The angle from this point to `target`, in radians clockwise on screen (y down) from
    /// pointing right: what rotate and a shape's rotated take, so `s.rotated(s.placement.offset.angleTo(p))`
    /// points a shape's right-hand side at p. 0 when they're the same point.
    public float angleTo(float2 target)
    {
        float2 to = target - this;
        return atan2(to.y, to.x);
    }
}

// Repeatable random numbers in 0..1 from a seed: the seed's exact bits go
// through Jarzynski and Olano's PCG hashes (jcgt.org/published/0009/03/02), so
// big, negative or fractional seeds are as random as small ones (the old
// sin(dot(…)) hash went blocky past a few thousand). Each takes its own salt,
// so random(id), random2(id) and random3(id) don't repeat each other.

uint3 pcg3(uint3 v)
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    v ^= v >> 16u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    return v;
}

float3 unit(uint3 v) { return float3(v >> 8u) * (1.0 / 16777216.0); }

/// A random number in 0..1 for a seed.
public float random(float seed) { return unit(pcg3(uint3(asuint(seed), 0x9e3779b9u, 0x85ebca6bu))).x; }

/// A random number in 0..1 for a 2D seed (a cell's id, a position).
public float random(float2 seed) { return unit(pcg3(uint3(asuint(seed), 0x27d4eb2fu))).x; }

/// A random point in 0..1 for a 2D seed: each axis independent.
public float2 random2(float2 seed) { return unit(pcg3(uint3(asuint(seed), 0x165667b1u))).xy; }

/// Three random numbers in 0..1 for a 2D seed (a colour, a point and a size, …).
public float3 random3(float2 seed) { return unit(pcg3(uint3(asuint(seed), 0x9e3779b9u))); }

/// A random number in 0..1 for a 3D seed (a lattice point in space, a cell and a time).
public float random(float3 seed) { return unit(pcg3(asuint(seed))).x; }

/// Three random numbers in 0..1 for a 3D seed: each independent.
public float3 random3(float3 seed) { return unit(pcg3(asuint(seed) ^ uint3(0x85ebca6bu, 0xc2b2ae35u, 0x27d4eb2fu))); }

/// A random whole number for a seed: all 32 bits, for picking (random(id) % count would be
/// biased by the float; randomBits(id) % count isn't).
public uint randomBits(float2 seed) { return pcg3(uint3(asuint(seed), 0x68e31da4u)).x; }

/// A random direction for a 2D seed: a point on the unit circle, every angle as likely.
public float2 randomDirection(float2 seed)
{
    float a = random(seed) * TAU;
    return float2(cos(a), sin(a));
}

/// A random direction for a 3D seed: a point on the unit sphere, every direction as likely.
public float3 randomDirection(float3 seed)
{
    float2 r = random3(seed).xy;
    float z = r.x * 2.0 - 1.0;
    float a = r.y * TAU;
    return float3(sqrt(max(1.0 - z * z, 0.0)) * float2(cos(a), sin(a)), z);
}

/// A random number in `range` (low, high) for a seed: remapped(random(seed), 0..1, range).
public float randomIn(float2 seed, float2 range) { return lerp(range.x, range.y, random(seed)); }

// ---- Ranges and steps -------------------------------------------------------------

/// How far x is from a to b, 0 at a and 1 at b, not held inside (lerp's inverse): unlerp(10, 20, 15) is 0.5.
public float unlerp(float a, float b, float x) { return (x - a) / (b - a); }

/// A straight ramp from 0 at `edge0` to 1 at `edge1`, held at both ends: smoothstep without the curve.
public float linearstep(float edge0, float edge1, float x) { return saturate((x - edge0) / (edge1 - edge0)); }

/// x moved to the nearest multiple of `step`: snapped(13, 5) is 15. Stepped motion, pixel grids.
public float snapped(float x, float step) { return round(x / step) * step; }

/// p moved to the nearest multiple of `step` on each axis.
public float2 snapped(float2 p, float2 step) { return round(p / step) * step; }

/// x wrapped into `low`..`high`, going round as it leaves: angles, scrolling, looping counters.
public float wrap(float x, float low, float high) { return low + mod(x - low, high - low); }

/// 0 below `edge`, 1 above, the step smoothed over one screen pixel of x: step without jaggies,
/// whatever units x is in.
public float aastep(float edge, float x)
{
    float w = max(fwidth(x), 1e-6) * 0.5;
    return smoothstep(edge - w, edge + w, x);
}

/// Moves `current` towards `target` by `rate` of the way per second, however long `dt` is
/// (exponential smoothing that frame rate doesn't change): for kept state.
public float damped(float current, float target, float rate, float dt) { return lerp(target, current, exp(-rate * dt)); }

/// Moves `current` towards `target`, frame rate free (see damped on floats).
public float2 damped(float2 current, float2 target, float rate, float dt) { return lerp(target, current, exp(-rate * dt)); }

// ---- Angles and turning --------------------------------------------------------------

/// The matrix that turns a point by `angle` radians clockwise on screen (y down): mul(m, p), as rotate(p, angle).
public float2x2 rotationMatrix(float angle)
{
    float c = cos(angle), s = sin(angle);
    return float2x2(c, -s, s, c);
}

/// A point as (how far from the origin, its angle in radians clockwise from pointing right).
public float2 polar(float2 p) { return float2(length(p), atan2(p.y, p.x)); }

/// A point from how far out it is and its angle (polar's inverse).
public float2 fromPolar(float radius, float angle) { return radius * float2(cos(angle), sin(angle)); }

/// A point from (how far, angle), as polar gives (polar's inverse).
public float2 fromPolar(float2 radiusAngle) { return fromPolar(radiusAngle.x, radiusAngle.y); }

// ---- Shaping: Quilez's little functions --------------------------------------------

/// Curves that bend a 0..1 number into a shape: pulses, impulses, biased ramps. Most are Inigo
/// Quilez's (iquilezles.org/articles/functions) and Christophe Schlick's.
public namespace shaping
{
    /// A spike rising fast and dying slowly, peaking at 1 when x is 1 / k: a hit, a thump.
    public float impulse(float x, float k)
    {
        float h = k * x;
        return h * exp(1.0 - h);
    }

    /// A smooth bump 1 at `centre` falling to 0 `width` either side: a cubic pulse (smoothstep's hump).
    public float pulse(float x, float centre, float width)
    {
        x = abs(x - centre);
        if (x > width)
            return 0.0;
        x /= width;
        return 1.0 - x * x * (3.0 - 2.0 * x);
    }

    /// 0 at 0, 1 at 0.5, 0 at 1, raised to `k` (higher is narrower): a hump over 0..1.
    public float parabola(float x, float k) { return pow(4.0 * x * (1.0 - x), k); }

    /// A hump over 0..1 leaning: `a` shapes the rise, `b` the fall; its top is 1.
    public float pcurve(float x, float a, float b)
    {
        float k = pow(a + b, a + b) / (pow(a, a) * pow(b, b));
        return k * pow(x, a) * pow(1.0 - x, b);
    }

    /// A step from 0 to 1 whose sharpness is `k`: 1 a straight-ish ramp, higher snaps (exp(-k x^n)).
    public float expStep(float x, float k, float n = 2.0) { return exp(-k * pow(x, n)); }

    /// Schlick's bias: pushes 0..1 values towards 0 (`b` below 0.5) or 1 (above); 0.5 leaves them.
    public float bias(float x, float b) { return x / ((1.0 / max(b, 1e-5) - 2.0) * (1.0 - x) + 1.0); }

    /// Schlick's gain: an S (`g` above 0.5) or its inverse (below) about the middle; 0.5 leaves it.
    public float gain(float x, float g)
    {
        return x < 0.5 ? bias(x * 2.0, g) * 0.5 : 1.0 - bias(2.0 - x * 2.0, g) * 0.5;
    }

    /// x itself, except near 0 where it never falls below `floorValue`, joining smoothly at `threshold`.
    public float almostIdentity(float x, float threshold, float floorValue)
    {
        if (x > threshold)
            return x;
        float a = 2.0 * floorValue - threshold;
        float b = 2.0 * threshold - 3.0 * floorValue;
        float t = x / threshold;
        return (a * t + b) * t * t + floorValue;
    }

    /// A ringing fade, sin(πkx - π) / (πkx - π): a wobble that settles, a struck bell's shape.
    public float sinc(float x, float k)
    {
        float a = PI * (k * x - 1.0);
        return abs(a) < 1e-4 ? 1.0 : sin(a) / a;
    }

    /// A spring released at t = 0 towards 1: overshoots and rings, `frequency` swings a second,
    /// settling as fast as `damping`. 1 - e^(-damping·t)·cos(2π·frequency·t). A pop, a wobble, a landing.
    public float spring(float t, float frequency = 3.0, float damping = 6.0)
    {
        if (t <= 0.0)
            return 0.0;
        return 1.0 - exp(-damping * t) * cos(TAU * frequency * t);
    }

    /// A floor with smoothed steps, `k` the sharpness: stairs that ease from one to the next.
    public float smoothFloor(float x, float k = 8.0)
    {
        float f = frac(x);
        return floor(x) + pow(f, k) / (pow(f, k) + pow(1.0 - f, k));
    }
}

// ---- Complex numbers ------------------------------------------------------------------

/// Complex numbers as float2 (real, imaginary): fractals (Mandelbrot, Julia), conformal warps.
///
///     float2 z = float2(0), c = p / 200;
///     for (int k = 0; k < 64 && dot(z, z) < 4; k++) z = complex.square(z) + c;
public namespace complex
{
    public float2 mul(float2 a, float2 b) { return float2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x); }
    public float2 div(float2 a, float2 b) { return float2(a.x * b.x + a.y * b.y, a.y * b.x - a.x * b.y) / max(dot(b, b), 1e-12); }
    public float2 square(float2 z) { return float2(z.x * z.x - z.y * z.y, 2.0 * z.x * z.y); }
    /// 1 / z: turns the plane inside out round the unit circle.
    public float2 inverse(float2 z) { return float2(z.x, -z.y) / max(dot(z, z), 1e-12); }
    public float2 conjugate(float2 z) { return float2(z.x, -z.y); }
    /// e^z: wraps a straight strip round into rings (log-polar tunnels).
    public float2 exp(float2 z) { return ::exp(z.x) * float2(cos(z.y), sin(z.y)); }
    /// The natural log: (ln|z|, its angle). Turns rings into straight strips.
    public float2 log(float2 z) { return float2(::log(max(length(z), 1e-12)), atan2(z.y, z.x)); }
    /// z to the power `n`, any real n.
    public float2 pow(float2 z, float n)
    {
        float r = ::pow(max(length(z), 1e-12), n);
        float a = atan2(z.y, z.x) * n;
        return r * float2(cos(a), sin(a));
    }
}
