// xanadu.shape: 2D shapes as values. Make one, change it, then draw it:
//
//     let petal = Box(float2(28.0), 10.0).repeat(8.0, 160.0).turn(time);
//     float a = petal.fill(i.centred);
//
// Every shape has distance(p) (signed: negative inside), and through the
// extension below: turn, move, scale, repeat, onion, or, and, minus, smooth,
// fill, stroke and glow. Sizes are full widths and heights, in the units of p
// (pixels when p is ItemInput.centred), so Box(i.canvas.size) fills the quad.
// Round shapes take a radius. Most distances are Inigo Quilez's
// (iquilezles.org/articles/distfunctions2d).
module shape;
import xanadu.math;

public interface IShape
{
    /// Signed distance from p to the edge: negative inside, positive outside.
    public float distance(float2 p);
}

// ---- Shapes ----------------------------------------------------------------

public struct Circle : IShape
{
    public float radius;

    public __init(float radius) { this.radius = radius; }

    public float distance(float2 p) { return length(p) - radius; }
}

/// A box `size` wide and high, its corners rounded by `radius`.
public struct Box : IShape
{
    public float2 size;
    public float radius;

    public __init(float2 size, float radius = 0.0) { this.size = size; this.radius = radius; }

    public float distance(float2 p)
    {
        float2 q = abs(p) - size * 0.5 + radius;
        return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
    }
}

/// A box `size` wide and high with its own radius per corner: (bottom right, top right, bottom left, top left), y down.
public struct CornerBox : IShape
{
    public float2 size;
    public float4 corners;

    public __init(float2 size, float4 corners) { this.size = size; this.corners = corners; }

    public float distance(float2 p)
    {
        float4 r = corners;
        r.xy = (p.x > 0.0) ? r.xy : r.zw;
        r.x = (p.y > 0.0) ? r.x : r.y;
        float2 q = abs(p) - size * 0.5 + r.x;
        return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
    }
}

/// A box `size` wide and high with its corners cut off at 45° by `chamfer`.
public struct ChamferBox : IShape
{
    public float2 size;
    public float chamfer;

    public __init(float2 size, float chamfer) { this.size = size; this.chamfer = chamfer; }

    public float distance(float2 p)
    {
        p = abs(p) - size * 0.5;
        p = (p.y > p.x) ? p.yx : p.xy;
        p.y += chamfer;
        const float k = 1.0 - sqrt(2.0);
        if (p.y < 0.0 && p.y + p.x * k < 0.0)
            return p.x;
        if (p.x < p.y)
            return (p.x + p.y) * sqrt(0.5);
        return length(p);
    }
}

/// A plus sign `size.x` across (end to end) with arms `size.y` thick, `radius` rounding it.
public struct Cross : IShape
{
    public float2 size;
    public float radius;

    public __init(float2 size, float radius = 0.0) { this.size = size; this.radius = radius; }

    public float distance(float2 p)
    {
        float2 half = size * 0.5;
        p = abs(p);
        p = (p.y > p.x) ? p.yx : p.xy;
        float2 q = p - half;
        float k = max(q.y, q.x);
        float2 w = (k > 0.0) ? q : float2(half.y - p.x, -k);
        return sign(k) * length(max(w, 0.0)) + radius;
    }
}

/// A ring `thickness` wide with a gap: `gap` is the gap's half angle in radians (0 for a full ring), facing down.
public struct Arc : IShape
{
    public float radius;
    public float thickness;
    public float gap;

    public __init(float radius, float thickness, float gap = 0.0) { this.radius = radius; this.thickness = thickness; this.gap = gap; }

    public float distance(float2 p)
    {
        float2 n = float2(sin(gap), cos(gap));
        p.x = abs(p.x);
        p = float2(n.y * p.x - n.x * p.y, n.x * p.x + n.y * p.y);
        return max(abs(length(p) - radius) - thickness * 0.5,
                   length(float2(p.x, max(0.0, abs(radius - p.y) - thickness * 0.5))) * sign(p.x));
    }
}

/// An equilateral triangle pointing up, `radius` from its centre to each side's middle × √3.
public struct Triangle : IShape
{
    public float radius;

    public __init(float radius) { this.radius = radius; }

    public float distance(float2 p)
    {
        const float k = sqrt(3.0);
        float r = radius;
        p.y = -p.y;
        p.x = abs(p.x) - r;
        p.y = p.y + r / k;
        if (p.x + k * p.y > 0.0)
            p = float2(p.x - k * p.y, -k * p.x - p.y) / 2.0;
        p.x -= clamp(p.x, -2.0 * r, 0.0);
        return -length(p) * sign(p.y);
    }
}

/// A slanted box `width` wide and `height` high, its top shifted right by `skew`.
public struct Parallelogram : IShape
{
    public float width;
    public float height;
    public float skew;

    public __init(float width, float height, float skew) { this.width = width; this.height = height; this.skew = skew; }

    public float distance(float2 p)
    {
        float wi = width * 0.5, he = height * 0.5;
        float2 e = float2(skew, he);
        p.y = -p.y;
        p = (p.y < 0.0) ? -p : p;
        float2 w = p - e;
        w.x -= clamp(w.x, -wi, wi);
        float2 d = float2(dot(w, w), -w.y);
        float s = p.x * e.y - p.y * e.x;
        p = (s < 0.0) ? -p : p;
        float2 v = p - float2(wi, 0);
        v -= e * clamp(dot(v, e) / dot(e, e), -1.0, 1.0);
        d = min(d, float2(dot(v, v), wi * he - abs(s)));
        return sqrt(d.x) * sign(-d.y);
    }
}

/// A six-pointed star.
public struct Star6 : IShape
{
    public float radius;

    public __init(float radius) { this.radius = radius; }

    public float distance(float2 p)
    {
        const float4 k = float4(-0.5, 0.8660254038, 0.5773502692, 1.7320508076);
        p = abs(p);
        p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
        p -= 2.0 * min(dot(k.yx, p), 0.0) * k.yx;
        p -= float2(clamp(p.x, radius * k.z, radius * k.w), radius);
        return length(p) * sign(p.y);
    }
}

/// A crescent: a circle of `radius` with a bite of radius `bite` taken from `offset` to its right.
public struct Moon : IShape
{
    public float radius;
    public float bite;
    public float offset;

    public __init(float radius, float bite, float offset) { this.radius = radius; this.bite = bite; this.offset = offset; }

    public float distance(float2 p)
    {
        float d = offset, ra = radius, rb = bite;
        p.y = abs(p.y);
        float a = (ra * ra - rb * rb + d * d) / (2.0 * d);
        float b = sqrt(max(ra * ra - a * a, 0.0));
        if (d * (p.x * b - p.y * a) > d * d * max(b - p.y, 0.0))
            return length(p - float2(a, b));
        return max(length(p) - ra, -(length(p - float2(d, 0)) - rb));
    }
}

// ---- Changes: each wraps a shape and is a shape itself ----------------------

public struct Turned<S : IShape> : IShape
{
    public S shape;
    public float angle;

    public float distance(float2 p) { return shape.distance(rotate(p, -angle)); }
}

public struct Moved<S : IShape> : IShape
{
    public S shape;
    public float2 offset;

    public float distance(float2 p) { return shape.distance(p - offset); }
}

public struct Scaled<S : IShape> : IShape
{
    public S shape;
    public float factor;

    public float distance(float2 p) { return shape.distance(p / factor) * factor; }
}

/// `count` copies round the centre, each `radius` out and facing outwards.
public struct Repeated<S : IShape> : IShape
{
    public S shape;
    public float count;
    public float radius;

    public float distance(float2 p)
    {
        float slice = TAU / count;
        float angle = mod(atan2(p.y, p.x), slice) - slice * 0.5;
        float2 q = float2(cos(angle), sin(angle)) * length(p);
        return shape.distance(q - float2(radius, 0.0));
    }
}

/// A shell `thickness` wide around the shape's edge.
public struct Onion<S : IShape> : IShape
{
    public S shape;
    public float thickness;

    public float distance(float2 p) { return abs(shape.distance(p)) - thickness; }
}

public struct Union<A : IShape, B : IShape> : IShape
{
    public A a;
    public B b;

    public float distance(float2 p) { return min(a.distance(p), b.distance(p)); }
}

public struct Intersection<A : IShape, B : IShape> : IShape
{
    public A a;
    public B b;

    public float distance(float2 p) { return max(a.distance(p), b.distance(p)); }
}

public struct Difference<A : IShape, B : IShape> : IShape
{
    public A a;
    public B b;

    public float distance(float2 p) { return max(a.distance(p), -b.distance(p)); }
}

/// Two shapes melted together over `blend` units.
public struct SmoothUnion<A : IShape, B : IShape> : IShape
{
    public A a;
    public B b;
    public float blend;

    public float distance(float2 p)
    {
        float da = a.distance(p), db = b.distance(p);
        float h = saturate(0.5 + 0.5 * (db - da) / max(blend, 1e-5));
        return lerp(db, da, h) - blend * h * (1.0 - h);
    }
}

// ---- What every shape can do ------------------------------------------------

public extension<S : IShape> S
{
    /// Turned by `angle` radians round its centre.
    public Turned<S> turn(float angle) { Turned<S> r; r.shape = this; r.angle = angle; return r; }

    /// Moved by `offset`.
    public Moved<S> move(float2 offset) { Moved<S> r; r.shape = this; r.offset = offset; return r; }

    /// Made `factor` times bigger round its centre.
    public Scaled<S> scale(float factor) { Scaled<S> r; r.shape = this; r.factor = factor; return r; }

    /// `count` copies in a circle, each `radius` out from the centre.
    public Repeated<S> repeat(float count, float radius) { Repeated<S> r; r.shape = this; r.count = count; r.radius = radius; return r; }

    /// Just a shell `thickness` wide round its edge.
    public Onion<S> onion(float thickness) { Onion<S> r; r.shape = this; r.thickness = thickness; return r; }

    /// This shape and `other` together.
    public Union<S, T> or<T : IShape>(T other) { Union<S, T> r; r.a = this; r.b = other; return r; }

    /// Only where this shape and `other` overlap.
    public Intersection<S, T> and<T : IShape>(T other) { Intersection<S, T> r; r.a = this; r.b = other; return r; }

    /// This shape with `other` cut out of it.
    public Difference<S, T> minus<T : IShape>(T other) { Difference<S, T> r; r.a = this; r.b = other; return r; }

    /// This shape and `other` melted together over `blend` units.
    public SmoothUnion<S, T> melt<T : IShape>(T other, float blend) { SmoothUnion<S, T> r; r.a = this; r.b = other; r.blend = blend; return r; }

    /// 1 inside, 0 outside, the edge smoothed over `soft` units.
    public float fill(float2 p, float soft = 1.0)
    {
        return 1.0 - smoothstep(-soft * 0.5, soft * 0.5, distance(p));
    }

    /// 1 on a line `width` wide along the edge, 0 elsewhere.
    public float stroke(float2 p, float width, float soft = 1.0)
    {
        return 1.0 - smoothstep(width * 0.5 - soft * 0.5, width * 0.5 + soft * 0.5, abs(distance(p)));
    }

    /// 1 at the edge fading to 0 `reach` units away on both sides, shaped by `falloff` (higher is tighter).
    public float glow(float2 p, float reach, float falloff = 1.0)
    {
        return pow(1.0 - saturate(abs(distance(p)) / reach), falloff);
    }
}
