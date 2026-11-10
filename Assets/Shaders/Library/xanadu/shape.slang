// xanadu.shape: 2D shapes as values. Make one, place it, then draw it:
//
//     Circle c = Circle(30);
//     c.rotate(time);                  // changes c
//     c.move(float2(40, 0));
//     let petals = Box(float2(28), 10).rotated(time).repeat(8, 160);   // copies, for chains
//     float a = petals.fill() + c.stroke(2);
//
// Every shape carries its placement: rotate, move, scale and anchor change it
// in place; rotated, moved, scaled and anchored give a changed copy. All of
// them work round the shape's own centre, so their order doesn't matter.
// anchor(anchor.topLeft) and the like move it by a point of the quad (a
// corner, a side, or any fraction of its size). repeat, onion, or, and,
// minus and melt build new shapes from it; fill, stroke and glow draw it,
// from the quad's centre unless given a point (the pixel being shaded, at
// i.canvas.centred). distance(p) is the signed distance (negative inside).
// Sizes are full widths and heights, in the units of p (pixels by default),
// so Box(i.canvas.size) fills the quad. Round shapes take a radius. Most
// distances are Inigo Quilez's (iquilezles.org/articles/distfunctions2d).
module shape;
import xanadu.math;
import xanadu.stage;

/// Points of the quad for anchor(): from the centre, as fractions of its size (y down).
public namespace anchor
{
    public static const float2 centre = float2(0.0, 0.0);
    public static const float2 top = float2(0.0, -0.5);
    public static const float2 bottom = float2(0.0, 0.5);
    public static const float2 left = float2(-0.5, 0.0);
    public static const float2 right = float2(0.5, 0.0);
    public static const float2 topLeft = float2(-0.5, -0.5);
    public static const float2 topRight = float2(0.5, -0.5);
    public static const float2 bottomLeft = float2(-0.5, 0.5);
    public static const float2 bottomRight = float2(0.5, 0.5);
}

/// Plain signed distances (negative inside), for when you build one yourself
/// (in a loop, from a formula) rather than from shapes: the same drawing and
/// joining the shapes have.
///
///     float d = 1e5;
///     for (int b = 0; b < 7; b++) d = sdf.melt(d, ball[b].distance(p), 40);
///     float a = sdf.fill(d) + sdf.glow(d, 60, 2);
public namespace sdf
{
    /// 1 inside, 0 outside, the edge smoothed over `soft` units.
    public float fill(float d, float soft = 1.0) { return 1.0 - smoothstep(-soft * 0.5, soft * 0.5, d); }

    /// 1 on a line `width` wide along the edge, 0 elsewhere.
    public float stroke(float d, float width, float soft = 1.0)
    {
        return 1.0 - smoothstep(width * 0.5 - soft * 0.5, width * 0.5 + soft * 0.5, abs(d));
    }

    /// 1 at the edge fading to 0 `reach` units away on both sides, shaped by `falloff` (higher is tighter).
    public float glow(float d, float reach, float falloff = 1.0) { return pow(1.0 - saturate(abs(d) / reach), falloff); }

    /// Both together, melted over `blend` units: a smooth min (Quilez's polynomial smin).
    public float melt(float a, float b, float blend)
    {
        float h = saturate(0.5 + 0.5 * (b - a) / max(blend, 1e-5));
        return lerp(b, a, h) - blend * h * (1.0 - h);
    }

    /// Only where both are, the edge rounded over `blend` units: a smooth max.
    public float meltAnd(float a, float b, float blend) { return -melt(-a, -b, blend); }

    /// `a` with `b` cut out, the cut rounded over `blend` units.
    public float meltMinus(float a, float b, float blend) { return -melt(-a, b, blend); }
}

/// Where a shape sits: moved by offset, turned by angle (radians, clockwise, y
/// down) and sized by scale, each round its own centre.
public struct Placement
{
    public float2 offset = float2(0.0);
    public float angle = 0.0;
    public float scale = 1.0;

    /// A point in the shape's own space, before it was placed.
    public float2 toLocal(float2 p)
    {
        // Undoing the placement: back by the offset, back by the turn, back by the scale.
        return rotate(p - offset, -angle) / scale;
    }
}

public interface IShape
{
    /// Signed distance from p in the shape's own space, before its placement: negative inside.
    public float local(float2 p);

    /// Where it sits; rotate, move, scale and anchor change it.
    public property Placement placement { get; set; }
}

// ---- Shapes ----------------------------------------------------------------

public struct Circle : IShape
{
    public Placement placement = Placement();
    public float radius;

    public __init(float radius) { this.radius = radius; }

    public float local(float2 p) { return length(p) - radius; }
}

/// A line from `a` to `b`, `width` thick with round ends (a capsule); 0 for a bare line to stroke or glow.
public struct Line : IShape
{
    public Placement placement = Placement();
    public float2 a;
    public float2 b;
    public float width;

    public __init(float2 a, float2 b, float width = 0.0) { this.a = a; this.b = b; this.width = width; }

    public float local(float2 p)
    {
        float2 pa = p - a, ba = b - a;
        float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-9));
        return length(pa - ba * h) - width * 0.5;
    }
}

/// A box `size` wide and high, its corners rounded by `radius`.
public struct Box : IShape
{
    public Placement placement = Placement();
    public float2 size;
    public float radius;

    public __init(float2 size, float radius = 0.0) { this.size = size; this.radius = radius; }

    public float local(float2 p)
    {
        float2 q = abs(p) - size * 0.5 + radius;
        return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
    }
}

/// A box `size` wide and high with its own radius per corner: (bottom right, top right, bottom left, top left), y down.
public struct CornerBox : IShape
{
    public Placement placement = Placement();
    public float2 size;
    public float4 corners;

    public __init(float2 size, float4 corners) { this.size = size; this.corners = corners; }

    public float local(float2 p)
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
    public Placement placement = Placement();
    public float2 size;
    public float chamfer;

    public __init(float2 size, float chamfer) { this.size = size; this.chamfer = chamfer; }

    public float local(float2 p)
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
    public Placement placement = Placement();
    public float2 size;
    public float radius;

    public __init(float2 size, float radius = 0.0) { this.size = size; this.radius = radius; }

    public float local(float2 p)
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
    public Placement placement = Placement();
    public float radius;
    public float thickness;
    public float gap;

    public __init(float radius, float thickness, float gap = 0.0) { this.radius = radius; this.thickness = thickness; this.gap = gap; }

    public float local(float2 p)
    {
        // Quilez's ring shows an arc of half angle `shown` centred below; flipped
        // and opened to everything but the gap, the gap faces down.
        float shown = PI - gap;
        float2 n = float2(sin(shown), cos(shown));
        p.y = -p.y;
        p.x = abs(p.x);
        p = float2(n.y * p.x - n.x * p.y, n.x * p.x + n.y * p.y);
        return max(abs(length(p) - radius) - thickness * 0.5,
                   length(float2(p.x, max(0.0, abs(radius - p.y) - thickness * 0.5))) * sign(p.x));
    }
}

/// An equilateral triangle pointing up, `radius` from its centre to each side's middle × √3.
public struct Triangle : IShape
{
    public Placement placement = Placement();
    public float radius;

    public __init(float radius) { this.radius = radius; }

    public float local(float2 p)
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
    public Placement placement = Placement();
    public float width;
    public float height;
    public float skew;

    public __init(float width, float height, float skew) { this.width = width; this.height = height; this.skew = skew; }

    public float local(float2 p)
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
    public Placement placement = Placement();
    public float radius;

    public __init(float radius) { this.radius = radius; }

    public float local(float2 p)
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
    public Placement placement = Placement();
    public float radius;
    public float bite;
    public float offset;

    public __init(float radius, float bite, float offset) { this.radius = radius; this.bite = bite; this.offset = offset; }

    public float local(float2 p)
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

/// `count` copies round the centre, each `radius` out and facing outwards.
public struct Repeated<S : IShape> : IShape
{
    public Placement placement = Placement();
    public S shape;
    public float count;
    public float radius;

    public float local(float2 p)
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
    public Placement placement = Placement();
    public S shape;
    public float thickness;

    public float local(float2 p) { return abs(shape.distance(p)) - thickness; }
}

public struct Union<A : IShape, B : IShape> : IShape
{
    public Placement placement = Placement();
    public A a;
    public B b;

    public float local(float2 p) { return min(a.distance(p), b.distance(p)); }
}

public struct Intersection<A : IShape, B : IShape> : IShape
{
    public Placement placement = Placement();
    public A a;
    public B b;

    public float local(float2 p) { return max(a.distance(p), b.distance(p)); }
}

public struct Difference<A : IShape, B : IShape> : IShape
{
    public Placement placement = Placement();
    public A a;
    public B b;

    public float local(float2 p) { return max(a.distance(p), -b.distance(p)); }
}

/// Two shapes melted together over `blend` units.
public struct SmoothUnion<A : IShape, B : IShape> : IShape
{
    public Placement placement = Placement();
    public A a;
    public B b;
    public float blend;

    public float local(float2 p)
    {
        return sdf.melt(a.distance(p), b.distance(p), blend);
    }
}

/// Only where both shapes are, the edge rounded over `blend` units.
public struct SmoothIntersection<A : IShape, B : IShape> : IShape
{
    public Placement placement = Placement();
    public A a;
    public B b;
    public float blend;

    public float local(float2 p) { return sdf.meltAnd(a.distance(p), b.distance(p), blend); }
}

/// `a` with `b` cut out of it, the cut rounded over `blend` units.
public struct SmoothDifference<A : IShape, B : IShape> : IShape
{
    public Placement placement = Placement();
    public A a;
    public B b;
    public float blend;

    public float local(float2 p) { return sdf.meltMinus(a.distance(p), b.distance(p), blend); }
}

// ---- What every shape can do ------------------------------------------------

public extension<S : IShape> S
{
    /// The signed distance from p to its edge, placed: negative inside, positive outside.
    public float distance(float2 p)
    {
        Placement at = placement;
        return local(at.toLocal(p)) * at.scale;
    }

    /// Turns it by `angle` radians round its centre, clockwise (y down).
    [mutating] public void rotate(float angle) { placement.angle += angle; }

    /// Moves it by `offset`.
    [mutating] public void move(float2 offset) { placement.offset += offset; }

    /// Makes it `factor` times bigger round its centre.
    [mutating] public void scale(float factor) { placement.scale *= factor; }

    /// Moves it by a point of the quad: anchor.topLeft, anchor.right, … or any fraction of its size from the centre.
    [mutating] public void anchor(float2 place) { placement.offset += place * shading.canvas.size; }

    /// A copy turned by `angle` radians round its centre, clockwise (y down).
    public S rotated(float angle) { S r = this; r.rotate(angle); return r; }

    /// A copy moved by `offset`.
    public S moved(float2 offset) { S r = this; r.move(offset); return r; }

    /// A copy `factor` times bigger round its centre.
    public S scaled(float factor) { S r = this; r.scale(factor); return r; }

    /// A copy moved by a point of the quad: anchor.topLeft, anchor.right, … or any fraction of its size from the centre.
    public S anchored(float2 place) { S r = this; r.anchor(place); return r; }

    /// `count` copies in a circle, each `radius` out from the centre.
    public Repeated<S> repeat(float count, float radius) { Repeated<S> r; r.placement = Placement(); r.shape = this; r.count = count; r.radius = radius; return r; }

    /// Just a shell `thickness` wide round its edge.
    public Onion<S> onion(float thickness) { Onion<S> r; r.placement = Placement(); r.shape = this; r.thickness = thickness; return r; }

    /// This shape and `other` together.
    public Union<S, T> or<T : IShape>(T other) { Union<S, T> r; r.placement = Placement(); r.a = this; r.b = other; return r; }

    /// Only where this shape and `other` overlap.
    public Intersection<S, T> and<T : IShape>(T other) { Intersection<S, T> r; r.placement = Placement(); r.a = this; r.b = other; return r; }

    /// This shape with `other` cut out of it.
    public Difference<S, T> minus<T : IShape>(T other) { Difference<S, T> r; r.placement = Placement(); r.a = this; r.b = other; return r; }

    /// This shape and `other` melted together over `blend` units.
    public SmoothUnion<S, T> melt<T : IShape>(T other, float blend) { SmoothUnion<S, T> r; r.placement = Placement(); r.a = this; r.b = other; r.blend = blend; return r; }

    /// Only where this shape and `other` overlap, the edge rounded over `blend` units.
    public SmoothIntersection<S, T> meltAnd<T : IShape>(T other, float blend) { SmoothIntersection<S, T> r; r.placement = Placement(); r.a = this; r.b = other; r.blend = blend; return r; }

    /// This shape with `other` cut out of it, the cut rounded over `blend` units.
    public SmoothDifference<S, T> meltMinus<T : IShape>(T other, float blend) { SmoothDifference<S, T> r; r.placement = Placement(); r.a = this; r.b = other; r.blend = blend; return r; }

    /// 1 inside, 0 outside, the edge smoothed over `soft` units.
    public float fill(float2 p, float soft = 1.0) { return sdf.fill(distance(p), soft); }

    /// 1 on a line `width` wide along the edge, 0 elsewhere.
    public float stroke(float2 p, float width, float soft = 1.0) { return sdf.stroke(distance(p), width, soft); }

    /// 1 at the edge fading to 0 `reach` units away on both sides, shaped by `falloff` (higher is tighter).
    public float glow(float2 p, float reach, float falloff = 1.0) { return sdf.glow(distance(p), reach, falloff); }

    /// fill at the pixel being shaded, from the quad's centre.
    public float fill(float soft = 1.0) { return fill(shading.canvas.centred, soft); }

    /// stroke at the pixel being shaded, from the quad's centre.
    public float stroke(float width, float soft = 1.0) { return stroke(shading.canvas.centred, width, soft); }

    /// glow at the pixel being shaded, from the quad's centre.
    public float glow(float reach, float falloff = 1.0) { return glow(shading.canvas.centred, reach, falloff); }
}
