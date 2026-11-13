// xanadu.shape: 2D shapes as values. Make one, place it, then draw it:
//
//     Circle c = Circle(30);
//     c.rotate(time);                  // changes c
//     c.move(float2(40, 0));
//     let petals = Box(float2(28), 10).rotated(time).repeated(8, 160);   // copies, for chains
//     float a = petals.filled() + c.stroked(2);
//
// Names follow one rule across the library: a verb in the imperative (rotate,
// paint) changes the thing it's called on and returns nothing; its past
// participle (rotated, painted, filled) leaves it alone and returns the result.
//
// Everything that draws is centred, then placed: it starts on the quad's
// centre at the pixel being shaded (shading.canvas.centred), the caller moves
// it from there, and an explicit point is only an extra overload for what the
// library can't know (a warped position, a tile's local). New drawing code
// becomes an IShape, as Grid and Checker in xanadu.pattern did. Docs/shaders.md
// has the rule.
//
// Every shape carries its placement: rotate, move, scale and anchor change it
// in place; rotated, moved, scaled and anchored give a changed copy. All of
// them work round the shape's own centre, so their order doesn't matter.
// anchor(anchor.topLeft) and the like move it by a point of the quad (a
// corner, a side, or any fraction of its size). repeated, hollowed, or, and,
// minus and melted build new shapes from it; filled, stroked and haloed draw it,
// from the quad's centre unless given a point (the pixel being shaded, at
// canvas.centred). distance(position) is the signed distance (negative inside).
// Sizes are full widths and heights, in the units of the position (pixels by default),
// so Box(canvas.size) fills the quad. Round shapes take a radius. Most
// distances are Inigo Quilez's (iquilezles.org/articles/distfunctions2d).
module shape;
import xanadu.math;
import xanadu.stage;

/// A box's sides for outlined(), joined with |: side.top | side.left.
public namespace side
{
    public static const uint top = 1;
    public static const uint right = 2;
    public static const uint bottom = 4;
    public static const uint left = 8;
    public static const uint all = 15;
}

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
///     for (int b = 0; b < 7; b++) d = sdf.melted(d, ball[b].distance(p), 40);
///     float a = sdf.filled(d) + sdf.haloed(d, 60, 2);
public namespace sdf
{
    /// 1 inside, 0 outside, the edge smoothed over `soft` units.
    public float filled(float d, float soft = 1.0) { return 1.0 - smoothstep(-soft * 0.5, soft * 0.5, d); }

    /// 1 on a line `width` wide along the edge, 0 elsewhere.
    public float stroked(float d, float width, float soft = 1.0)
    {
        return 1.0 - smoothstep(width * 0.5 - soft * 0.5, width * 0.5 + soft * 0.5, abs(d));
    }

    /// 1 at the edge fading to 0 `reach` units away on both sides, shaped by `falloff` (higher is tighter).
    public float haloed(float d, float reach, float falloff = 1.0) { return pow(1.0 - saturate(abs(d) / reach), falloff); }

    /// 1 inside, fading to 0 `width` units outside the edge: a soft edge that only grows outwards
    /// (filled fades across the edge instead, half in, half out). smoothstep(width, 0, d).
    public float feathered(float d, float width) { return 1.0 - smoothstep(0.0, max(width, 1e-5), d); }

    /// A shadow hugging the edge on both sides: 0 at the edge, rising to 1 away from it, 63% of
    /// the way there `reach` units out (1 - e^(-|d|/reach), the shape of fog falloff). Multiply a
    /// colour by it: c.brighten(sdf.shadowed(d, 80)).
    public float shadowed(float d, float reach) { return 1.0 - exp(-abs(d) / max(reach, 1e-5)); }

    /// Contour lines, bands at equal distance from the edge `spacing` units apart, like a map's
    /// height lines: 1 on a line, 1 - `depth` halfway between. Multiply a colour by it:
    /// c.brighten(sdf.contoured(d, 20)).
    public float contoured(float d, float spacing, float depth = 0.4)
    {
        return 1.0 - depth * (0.5 - 0.5 * cos(TAU * d / max(spacing, 1e-5)));
    }

    /// The distance field drawn the way Inigo Quilez draws one: orange outside, blue inside, a
    /// shadow hugging the edge `reach` units deep, contour lines and a white edge, all scaled from
    /// `reach` (his numbers: contours reach × 0.25 apart, the edge line reach × 0.06). The default
    /// suits pixels; in his units (the shorter side 2 across) it is 1/6. For seeing any distance.
    public float4 shown(float d, float reach = 80.0)
    {
        float3 rgb = d > 0.0 ? float3(0.9, 0.6, 0.3) : float3(0.65, 0.85, 1.0);
        rgb *= shadowed(d, reach);
        rgb *= contoured(d, reach * 6.0 * TAU / 150.0);
        float edge = reach * 0.06;
        rgb = lerp(rgb, float3(1.0), stroked(d, edge, edge));
        return float4(rgb, 1.0);
    }

    /// Both together, melted over `blend` units: a smooth min (Quilez's polynomial smin).
    public float melted(float a, float b, float blend)
    {
        float h = saturate(0.5 + 0.5 * (b - a) / max(blend, 1e-5));
        return lerp(b, a, h) - blend * h * (1.0 - h);
    }

    /// Only where both are, the edge rounded over `blend` units: a smooth max.
    public float meltedAnd(float a, float b, float blend) { return -melted(-a, -b, blend); }

    /// `a` with `b` cut out, the cut rounded over `blend` units.
    public float meltedMinus(float a, float b, float blend) { return -melted(-a, b, blend); }
}

/// Where a shape sits: moved by offset, turned by angle (radians, clockwise, y
/// down) and sized by scale, each round its own centre.
public struct Placement
{
    public float2 offset = float2(0.0);
    public float angle = 0.0;
    public float scale = 1.0;

    /// A point in the shape's own space, before it was placed.
    public float2 toLocal(float2 position)
    {
        // Undoing the placement: back by the offset, back by the turn, back by the scale.
        return rotate(position - offset, -angle) / scale;
    }
}

/// Anything centred, then placed: shapes, gradients, sprites. Each carries a
/// Placement, and rotate, move, scale and anchor (or their copies rotated,
/// moved, scaled and anchored) work on all of them alike.
public interface IPlaced
{
    /// Where it sits; rotate, move, scale and anchor change it.
    public property Placement placement { get; set; }
}

/// A shape: a signed distance, centred, then placed. Drawn with filled, stroked, outlined, haloed or c.paint.
/// @fits float
public interface IShape : IPlaced
{
    /// Signed distance from p in the shape's own space, before its placement: negative inside.
    public float local(float2 p);
}

// ---- Shapes ----------------------------------------------------------------

public struct Circle : IShape
{
    public Placement placement = Placement();
    public float radius;

    public __init(float radius) { this.radius = radius; }

    public float local(float2 p) { return length(p) - radius; }
}

/// A line from `a` to `b`, `width` thick with round ends (a capsule); 0 for a bare line to draw with stroked or haloed.
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

public extension Box
{
    /// outlined (a line `width` wide just inside the edge), but only along the chosen `sides`
    /// (side.top | side.left, …). Where a chosen side meets one that isn't, the line ends at the
    /// corner's diagonal (half a rounded corner).
    public float outlined(float2 position, uint sides, float width, float soft = 1.0)
    {
        Placement at = placement;
        float2 p = at.toLocal(position);
        // How far past the straight part of each side the point is: the corner's diagonal is where they're equal.
        float2 past = abs(p) - (size * 0.5 - radius);
        float upright = (sides & (p.x < 0.0 ? side.left : side.right)) != 0 ? 1.0 : 0.0;
        float level = (sides & (p.y < 0.0 ? side.top : side.bottom)) != 0 ? 1.0 : 0.0;
        float edge = soft * 0.5 / at.scale;
        float shown = lerp(level, upright, smoothstep(-edge, edge, past.x - past.y));
        return sdf.stroked(distance(position) + width * 0.5, width, soft) * shown;
    }

    /// outlined at the pixel being shaded, from the quad's centre.
    public float outlined(uint sides, float width, float soft = 1.0) { return outlined(shading.canvas.centred, sides, width, soft); }
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
public struct Hollow<S : IShape> : IShape
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
        return sdf.melted(a.distance(p), b.distance(p), blend);
    }
}

/// Only where both shapes are, the edge rounded over `blend` units.
public struct SmoothIntersection<A : IShape, B : IShape> : IShape
{
    public Placement placement = Placement();
    public A a;
    public B b;
    public float blend;

    public float local(float2 p) { return sdf.meltedAnd(a.distance(p), b.distance(p), blend); }
}

/// `a` with `b` cut out of it, the cut rounded over `blend` units.
public struct SmoothDifference<A : IShape, B : IShape> : IShape
{
    public Placement placement = Placement();
    public A a;
    public B b;
    public float blend;

    public float local(float2 p) { return sdf.meltedMinus(a.distance(p), b.distance(p), blend); }
}

// ---- What everything placed can do -------------------------------------------

public extension<P : IPlaced> P
{
    /// Turns it by `angle` radians round its centre, clockwise (y down).
    [mutating] public void rotate(float angle) { placement.angle += angle; }

    /// Moves it by `offset`.
    [mutating] public void move(float2 offset) { placement.offset += offset; }

    /// Makes it `factor` times bigger round its centre.
    [mutating] public void scale(float factor) { placement.scale *= factor; }

    /// Moves it by a point of the quad: anchor.topLeft, anchor.right, … or any fraction of its size from the centre.
    [mutating] public void anchor(float2 place) { placement.offset += place * shading.canvas.size; }

    /// A copy turned by `angle` radians round its centre, clockwise (y down).
    public P rotated(float angle) { P r = this; r.rotate(angle); return r; }

    /// A copy moved by `offset`.
    public P moved(float2 offset) { P r = this; r.move(offset); return r; }

    /// A copy `factor` times bigger round its centre.
    public P scaled(float factor) { P r = this; r.scale(factor); return r; }

    /// A copy moved by a point of the quad: anchor.topLeft, anchor.right, … or any fraction of its size from the centre.
    public P anchored(float2 place) { P r = this; r.anchor(place); return r; }
}

// ---- What every shape can do ------------------------------------------------

public extension<S : IShape> S
{
    /// The signed distance from p to its edge, placed: negative inside, positive outside.
    public float distance(float2 position)
    {
        Placement at = placement;
        return local(at.toLocal(position)) * at.scale;
    }

    /// distance at the pixel being shaded, from the quad's centre.
    public float distance() { return distance(shading.canvas.centred); }

    /// `count` copies in a circle, each `radius` out from the centre.
    public Repeated<S> repeated(float count, float radius) { Repeated<S> r; r.placement = Placement(); r.shape = this; r.count = count; r.radius = radius; return r; }

    /// Just a shell `thickness` wide round its edge.
    public Hollow<S> hollowed(float thickness) { Hollow<S> r; r.placement = Placement(); r.shape = this; r.thickness = thickness; return r; }

    /// This shape and `other` together.
    public Union<S, T> or<T : IShape>(T other) { Union<S, T> r; r.placement = Placement(); r.a = this; r.b = other; return r; }

    /// Only where this shape and `other` overlap.
    public Intersection<S, T> and<T : IShape>(T other) { Intersection<S, T> r; r.placement = Placement(); r.a = this; r.b = other; return r; }

    /// This shape with `other` cut out of it.
    public Difference<S, T> minus<T : IShape>(T other) { Difference<S, T> r; r.placement = Placement(); r.a = this; r.b = other; return r; }

    /// This shape and `other` melted together over `blend` units.
    public SmoothUnion<S, T> melted<T : IShape>(T other, float blend) { SmoothUnion<S, T> r; r.placement = Placement(); r.a = this; r.b = other; r.blend = blend; return r; }

    /// Only where this shape and `other` overlap, the edge rounded over `blend` units.
    public SmoothIntersection<S, T> meltedAnd<T : IShape>(T other, float blend) { SmoothIntersection<S, T> r; r.placement = Placement(); r.a = this; r.b = other; r.blend = blend; return r; }

    /// This shape with `other` cut out of it, the cut rounded over `blend` units.
    public SmoothDifference<S, T> meltedMinus<T : IShape>(T other, float blend) { SmoothDifference<S, T> r; r.placement = Placement(); r.a = this; r.b = other; r.blend = blend; return r; }

    /// 1 inside, 0 outside, the edge smoothed over `soft` units.
    public float filled(float2 position, float soft = 1.0) { return sdf.filled(distance(position), soft); }

    /// 1 on a line `width` wide along the edge, 0 elsewhere.
    public float stroked(float2 position, float width, float soft = 1.0) { return sdf.stroked(distance(position), width, soft); }

    /// 1 inside, fading to 0 `width` units outside the edge (filled fades across it instead).
    public float feathered(float2 position, float width) { return sdf.feathered(distance(position), width); }

    /// feathered at the pixel being shaded, from the quad's centre.
    public float feathered(float width) { return feathered(shading.canvas.centred, width); }

    /// 1 at the edge fading to 0 `reach` units away on both sides, shaped by `falloff` (higher is tighter).
    public float haloed(float2 position, float reach, float falloff = 1.0) { return sdf.haloed(distance(position), reach, falloff); }

    /// filled at the pixel being shaded, from the quad's centre.
    public float filled(float soft = 1.0) { return filled(shading.canvas.centred, soft); }

    /// 1 on a line `width` wide just inside the edge, 0 elsewhere: the shape keeps its size
    /// (stroked is centred on the edge instead, half outside).
    public float outlined(float2 position, float width, float soft = 1.0) { return sdf.stroked(distance(position) + width * 0.5, width, soft); }

    /// outlined at the pixel being shaded, from the quad's centre.
    public float outlined(float width, float soft = 1.0) { return outlined(shading.canvas.centred, width, soft); }

    /// stroked at the pixel being shaded, from the quad's centre.
    public float stroked(float width, float soft = 1.0) { return stroked(shading.canvas.centred, width, soft); }

    /// haloed at the pixel being shaded, from the quad's centre.
    public float haloed(float reach, float falloff = 1.0) { return haloed(shading.canvas.centred, reach, falloff); }
}
