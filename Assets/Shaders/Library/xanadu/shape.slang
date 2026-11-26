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
// measured() works a shape out once into a Measured; shapes and Measured are
// both IMeasurable, so drawing, painting and joins take either.
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

    /// filled with an edge exactly one screen pixel wide, whatever the units: for shapes drawn in
    /// uv, tiles or any space that isn't pixels (the soft width comes from how fast d changes).
    public float antialiased(float d) { return filled(d, max(fwidth(d), 1e-6)); }

    /// A neon glow: `width` / |d|, so it is 1 `width` units from the edge, brighter nearer and
    /// falling slowly away; `falloff` above 1 tightens it. Not held to 1: meant for blend.add or
    /// brighten, where the blown-out middle is the look (shadertoy's 0.02 / abs(d)).
    public float glowed(float d, float width, float falloff = 1.0) { return min(pow(width / max(abs(d), 1e-4), falloff), 1e3); }

    /// Where only one of the two is (exclusive or).
    public float xor(float a, float b) { return max(min(a, b), -max(a, b)); }

    /// Both together, joined with a 45° bevel `size` across instead of a round (a chamfered union).
    public float bevelled(float a, float b, float size) { return min(min(a, b), (a - size + b) * 0.70710678); }

    /// Both together, `steps` stair steps filling the join over `size` units (Mercury's hg_sdf opUnionStairs).
    public float stairs(float a, float b, float size, float steps)
    {
        float s = size / max(steps, 1.0);
        float u = b - size;
        return min(min(a, b), 0.5 * (u + a + abs(mod(u - a + s, 2.0 * s) - s)));
    }

    /// A groove `depth` deep and `width` wide cut along where `b`'s edge crosses `a`.
    public float grooved(float a, float b, float width, float depth) { return max(a, min(a + depth, width - abs(b))); }

    /// A distance cut into bands `width` units wide, 0 and 1 by turns: rings round any shape.
    public float banded(float d, float width) { return step(0.5, frac(d / max(width, 1e-5))); }

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

/// Anything with a signed distance: a shape (measured anywhere) or a Measured (fixed at its point).
/// Drawing (filled, stroked, …) and painting take either; joins between them give a Measured.
public interface IMeasurable
{
    /// Its distance at the pixel being shaded: a shape from the quad's centre, a Measured itself.
    public Measured measured();
    /// Its distance at `position`. A Measured has only its own point and gives itself back.
    public Measured measured(float2 position);
}

/// A shape: a signed distance, centred, then placed. Drawn with filled, stroked, outlined, haloed or c.paint.
/// @fits float
public interface IShape : IPlaced, IMeasurable
{
    /// Signed distance from p in the shape's own space, before its placement: negative inside.
    public float local(float2 p);
}

// ---- Shapes ----------------------------------------------------------------

/// @cost 1
/// @handle radius radius
public struct Circle : IShape
{
    public Placement placement = Placement();
    public float radius;

    public __init(float radius) { this.radius = radius; }

    public float local(float2 p) { return length(p) - radius; }
}

/// A line from `a` to `b`, `width` thick with round ends (a capsule); 0 for a bare line to draw with stroked or haloed.
/// @cost 2
/// @handle a point
/// @handle b point
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
/// @cost 2
/// @handle size size
/// @handle radius corner
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
/// @cost 3
/// @handle size size
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
/// @cost 3
/// @handle size size
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
/// @cost 3
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
/// @cost 4
/// @handle radius radius
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
/// @cost 3
/// @handle radius radius
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
/// @cost 4
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
/// @cost 3
/// @handle radius radius
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
/// @cost 4
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

/// An ellipse `radius.x` across from its centre to the sides and `radius.y` to the top and bottom
/// (Quilez's exact distance, solving the quartic).
/// @cost 5
/// @handle radius half
public struct Ellipse : IShape
{
    public Placement placement = Placement();
    public float2 radius;

    public __init(float2 radius) { this.radius = radius; }

    public float local(float2 p)
    {
        float2 ab = max(radius, float2(1e-4));
        if (abs(ab.x - ab.y) < 1e-3 * max(ab.x, ab.y))
            return length(p) - ab.x;
        p = abs(p);
        if (p.x > p.y) { p = p.yx; ab = ab.yx; }
        float l = ab.y * ab.y - ab.x * ab.x;
        float m = ab.x * p.x / l, m2 = m * m;
        float n = ab.y * p.y / l, n2 = n * n;
        float c = (m2 + n2 - 1.0) / 3.0, c3 = c * c * c;
        float q = c3 + m2 * n2 * 2.0;
        float d = c3 + m2 * n2;
        float g = m + m * n2;
        float co;
        if (d < 0.0)
        {
            float h = acos(clamp(q / c3, -1.0, 1.0)) / 3.0;
            float s = cos(h), t = sin(h) * sqrt(3.0);
            float rx = sqrt(max(-c * (s + t + 2.0) + m2, 0.0));
            float ry = sqrt(max(-c * (s - t + 2.0) + m2, 0.0));
            co = (ry + sign(l) * rx + abs(g) / max(rx * ry, 1e-9) - m) / 2.0;
        }
        else
        {
            float h = 2.0 * m * n * sqrt(d);
            float s = sign(q + h) * pow(abs(q + h), 1.0 / 3.0);
            float u = sign(q - h) * pow(abs(q - h), 1.0 / 3.0);
            float rx = -s - u - c * 4.0 + 2.0 * m2;
            float ry = (s - u) * sqrt(3.0);
            float rm = sqrt(rx * rx + ry * ry);
            co = (ry / sqrt(max(rm - rx, 1e-9)) + 2.0 * g / rm - m) / 2.0;
        }
        co = clamp(co, 0.0, 1.0);
        float2 r = ab * float2(co, sqrt(1.0 - co * co));
        return length(r - p) * sign(p.y - r.y);
    }
}

/// A regular polygon with `sides` equal sides (3 a triangle, 5 a pentagon, 6 a hexagon, …), its
/// corners `radius` from the centre. One corner points up when `sides` is odd; an even count sits
/// on a flat side.
/// @cost 3
/// @handle radius radius
public struct Polygon : IShape
{
    public Placement placement = Placement();
    public float sides;
    public float radius;

    public __init(float sides, float radius) { this.sides = sides; this.radius = radius; }

    public float local(float2 p)
    {
        // Folded into half of one side's slice, measured to that side.
        float half = PI / max(sides, 2.0);
        float angle = mod(atan2(p.x, p.y), 2.0 * half) - half;
        p = length(p) * float2(cos(angle), abs(sin(angle)));
        p -= radius * float2(cos(half), sin(half));
        p.y += clamp(-p.y, 0.0, radius * sin(half));
        return length(p) * sign(p.x);
    }
}

/// A star with `points` tips `radius` from the centre, one pointing up when `points` is odd.
/// `sharpness` 0 is the plain polygon, 1 the thinnest spikes (Quilez's star).
/// @cost 3
/// @handle radius radius
public struct Star : IShape
{
    public Placement placement = Placement();
    public float points;
    public float radius;
    public float sharpness;

    public __init(float points, float radius, float sharpness = 0.6) { this.points = points; this.radius = radius; this.sharpness = sharpness; }

    public float local(float2 p)
    {
        float n = max(points, 2.0);
        float m = lerp(n, 2.0, saturate(sharpness));
        float an = PI / n, en = PI / m;
        float2 acs = float2(cos(an), sin(an)), ecs = float2(cos(en), sin(en));
        float angle = mod(atan2(p.x, p.y), 2.0 * an) - an;
        p = length(p) * float2(cos(angle), abs(sin(angle)));
        p -= radius * acs;
        p += ecs * clamp(-dot(p, ecs), 0.0, radius * acs.y / ecs.y);
        return length(p) * sign(p.x);
    }
}

/// A diamond `size` wide and high, its corners on the middles of that box's sides.
/// @cost 2
/// @handle size size
public struct Rhombus : IShape
{
    public Placement placement = Placement();
    public float2 size;

    public __init(float2 size) { this.size = size; }

    public float local(float2 p)
    {
        float2 b = size * 0.5;
        p = abs(p);
        float2 e = b - 2.0 * p;
        float h = clamp((e.x * b.x - e.y * b.y) / dot(b, b), -1.0, 1.0);
        float d = length(p - 0.5 * b * float2(1.0 - h, 1.0 + h));
        return d * sign(p.x * b.y + p.y * b.x - b.x * b.y);
    }
}

/// A trapezoid `height` high, `bottom` wide along its bottom and `top` wide along its top.
/// @cost 3
public struct Trapezoid : IShape
{
    public Placement placement = Placement();
    public float bottom;
    public float top;
    public float height;

    public __init(float bottom, float top, float height) { this.bottom = bottom; this.top = top; this.height = height; }

    public float local(float2 p)
    {
        float r1 = bottom * 0.5, r2 = top * 0.5, he = height * 0.5;
        p.y = -p.y;
        float2 k1 = float2(r2, he);
        float2 k2 = float2(r2 - r1, 2.0 * he);
        p.x = abs(p.x);
        float2 ca = float2(p.x - min(p.x, p.y < 0.0 ? r1 : r2), abs(p.y) - he);
        float2 cb = p - k1 + k2 * clamp(dot(k1 - p, k2) / dot(k2, k2), 0.0, 1.0);
        float s = (cb.x < 0.0 && ca.y < 0.0) ? -1.0 : 1.0;
        return s * sqrt(min(dot(ca, ca), dot(cb, cb)));
    }
}

/// A triangle `width` across its base and `height` from the base to its tip, pointing up.
/// @cost 3
public struct Spike : IShape
{
    public Placement placement = Placement();
    public float width;
    public float height;

    public __init(float width, float height) { this.width = width; this.height = height; }

    public float local(float2 p)
    {
        // Quilez's isosceles triangle: tip on the origin, base `height` below it.
        float2 q = float2(width * 0.5, height);
        p.y += height * 0.5;
        p.x = abs(p.x);
        float2 a = p - q * clamp(dot(p, q) / dot(q, q), 0.0, 1.0);
        float2 b = p - q * float2(clamp(p.x / q.x, 0.0, 1.0), 1.0);
        float s = -sign(q.y);
        float2 d = min(float2(dot(a, a), s * (p.x * q.y - p.y * q.x)), float2(dot(b, b), s * (p.y - q.y)));
        return -sqrt(d.x) * sign(d.y);
    }
}

/// A slice of a disc `radius` round, opening upwards `angle` radians either side of straight up
/// (PI is the whole disc).
/// @cost 3
public struct Pie : IShape
{
    public Placement placement = Placement();
    public float radius;
    public float angle;

    public __init(float radius, float angle) { this.radius = radius; this.angle = angle; }

    public float local(float2 p)
    {
        float2 c = float2(sin(angle), cos(angle));
        p.y = -p.y;
        p.x = abs(p.x);
        float l = length(p) - radius;
        float m = length(p - c * clamp(dot(p, c), 0.0, radius));
        return max(l, m * sign(c.y * p.x - c.x * p.y));
    }
}

/// A heart about `size` across and high, point down (Quilez's exact heart).
/// @cost 3
public struct Heart : IShape
{
    public Placement placement = Placement();
    public float size;

    public __init(float size) { this.size = size; }

    public float local(float2 p)
    {
        // His heart runs from its point at 0 up to about 1.05: centred, y up, in units of size.
        float2 q = float2(abs(p.x), -p.y + 0.525 * size) / size;
        float d;
        if (q.y + q.x > 1.0)
            d = length(q - float2(0.25, 0.75)) - sqrt(2.0) / 4.0;
        else
        {
            float2 a = q - float2(0.0, 1.0);
            float2 b = q - 0.5 * max(q.x + q.y, 0.0);
            d = sqrt(min(dot(a, a), dot(b, b))) * sign(q.x - q.y);
        }
        return d * size;
    }
}

/// A pointed oval standing upright: where two circles of `radius`, their centres `offset` either
/// side of the middle, overlap (a vesica, an eye, a leaf). `offset` below `radius`.
/// @cost 2
public struct Vesica : IShape
{
    public Placement placement = Placement();
    public float radius;
    public float offset;

    public __init(float radius, float offset) { this.radius = radius; this.offset = offset; }

    public float local(float2 p)
    {
        float r = radius, d = min(offset, radius * 0.999);
        p = abs(p);
        float b = sqrt(r * r - d * d);
        return ((p.y - b) * d > p.x * b) ? length(p - float2(0.0, b)) : length(p - float2(-d, 0.0)) - r;
    }
}

/// An X `size` wide and high, its arms `thickness` thick with round ends.
/// @cost 2
public struct Saltire : IShape
{
    public Placement placement = Placement();
    public float size;
    public float thickness;

    public __init(float size, float thickness) { this.size = size; this.thickness = thickness; }

    public float local(float2 p)
    {
        p = abs(p);
        return length(p - min(p.x + p.y, size) * 0.5) - thickness * 0.5;
    }
}

/// A line from `a` to `b` that thickens or thins along the way: `widthA` wide at `a`, `widthB` at
/// `b`, round at both ends (Quilez's uneven capsule). A tadpole, a tapered stroke, a comet.
/// @cost 3
public struct TaperedLine : IShape
{
    public Placement placement = Placement();
    public float2 a;
    public float2 b;
    public float widthA;
    public float widthB;

    public __init(float2 a, float2 b, float widthA, float widthB) { this.a = a; this.b = b; this.widthA = widthA; this.widthB = widthB; }

    public float local(float2 p)
    {
        float2 along = b - a;
        float h = max(length(along), 1e-5);
        float2 up = along / h;
        // Into the line's frame: a at the origin, b straight along +y.
        p -= a;
        p = float2(abs(up.y * p.x - up.x * p.y), dot(p, up));
        float r1 = widthA * 0.5, r2 = widthB * 0.5;
        float s = clamp((r1 - r2) / h, -0.9999, 0.9999);
        float c = sqrt(1.0 - s * s);
        float k = dot(p, float2(-s, c));
        if (k < 0.0)
            return length(p) - r1;
        if (k > c * h)
            return length(p - float2(0.0, h)) - r2;
        return dot(p, float2(c, s)) - r1;
    }
}

/// A curve from `a` to `b` pulled towards `control` (a quadratic Bézier), `width` thick with
/// round ends; 0 for a bare curve to draw with stroked or haloed (Quilez's exact distance).
/// @cost 5
/// @handle a point
/// @handle control point
/// @handle b point
public struct Arch : IShape
{
    public Placement placement = Placement();
    public float2 a;
    public float2 control;
    public float2 b;
    public float width;

    public __init(float2 a, float2 control, float2 b, float width = 0.0) { this.a = a; this.control = control; this.b = b; this.width = width; }

    public float local(float2 pos)
    {
        float2 A = a, B = control, C = b;
        // A straight one has no curve to solve: nudge its control off the line.
        float2 bend = A - 2.0 * B + C;
        if (dot(bend, bend) < 1e-6)
            B += float2(1e-3, -1e-3);
        float2 ea = B - A;
        float2 eb = A - 2.0 * B + C;
        float2 ec = ea * 2.0;
        float2 ed = A - pos;
        float kk = 1.0 / dot(eb, eb);
        float kx = kk * dot(ea, eb);
        float ky = kk * (2.0 * dot(ea, ea) + dot(ed, eb)) / 3.0;
        float kz = kk * dot(ed, ea);
        float pp = ky - kx * kx;
        float p3 = pp * pp * pp;
        float q = kx * (2.0 * kx * kx - 3.0 * ky) + kz;
        float h = q * q + 4.0 * p3;
        float res;
        if (h >= 0.0)
        {
            h = sqrt(h);
            float2 x = (float2(h, -h) - q) / 2.0;
            float2 uv = sign(x) * pow(abs(x), float2(1.0 / 3.0));
            float t = clamp(uv.x + uv.y - kx, 0.0, 1.0);
            float2 w = ed + (ec + eb * t) * t;
            res = dot(w, w);
        }
        else
        {
            float z = sqrt(-pp);
            float v = acos(clamp(q / (pp * z * 2.0), -1.0, 1.0)) / 3.0;
            float m = cos(v), n = sin(v) * 1.732050808;
            float3 t = clamp(float3(m + m, -n - m, n - m) * z - kx, 0.0, 1.0);
            float2 w1 = ed + (ec + eb * t.x) * t.x;
            float2 w2 = ed + (ec + eb * t.y) * t.y;
            res = min(dot(w1, w1), dot(w2, w2));
        }
        return sqrt(res) - width * 0.5;
    }
}

/// Any closed outline through up to 32 corners in order (either way round), straight between
/// them: Quilez's polygon. Add the corners, then draw it like any shape.
///
///     Outline house;
///     house.add(float2(-40, 40)); house.add(float2(40, 40)); house.add(float2(40, -10));
///     house.add(float2(0, -50)); house.add(float2(-40, -10));
///     c.paint(house, colours.white);
/// @cost 6
public struct Outline : IShape
{
    public Placement placement = Placement();
    public float2 corners[32];
    public int count = 0;

    public __init() { count = 0; }

    /// Adds a corner after the last; past 32 they're dropped.
    [mutating] public void add(float2 corner)
    {
        if (count < 32)
        {
            corners[count] = corner;
            count++;
        }
    }

    /// A copy with a corner added after the last.
    public Outline added(float2 corner) { Outline r = this; r.add(corner); return r; }

    public float local(float2 p)
    {
        if (count < 1)
            return 1e10;
        float2 first = p - corners[0];
        float d = dot(first, first);
        float s = 1.0;
        for (int i = 0; i < count; i++)
        {
            int j = i == 0 ? count - 1 : i - 1;
            float2 e = corners[j] - corners[i];
            float2 w = p - corners[i];
            float2 b = w - e * clamp(dot(w, e) / max(dot(e, e), 1e-9), 0.0, 1.0);
            d = min(d, dot(b, b));
            bool3 c = bool3(p.y >= corners[i].y, p.y < corners[j].y, e.x * w.y > e.y * w.x);
            if (all(c) || !any(c))
                s = -s;
        }
        return s * sqrt(d);
    }
}

/// An open path through up to 32 points in order, `width` thick with round joins and ends; 0 for
/// a bare path to draw with stroked or haloed. Add the points as for an Outline.
/// @cost 5
public struct Polyline : IShape
{
    public Placement placement = Placement();
    public float2 points[32];
    public int count = 0;
    public float width = 0.0;
    /// How much of it is drawn, 0..1 of its length from its start: tracedTo animates it drawing itself on.
    public float until = 1.0;

    public __init(float width = 0.0) { count = 0; this.width = width; }

    /// A copy drawn only from its start to `progress` (0..1) of its length: a path drawing itself on.
    public Polyline tracedTo(float progress) { Polyline r = this; r.until = saturate(progress); return r; }

    /// Adds a point after the last; past 32 they're dropped.
    [mutating] public void add(float2 point)
    {
        if (count < 32)
        {
            points[count] = point;
            count++;
        }
    }

    /// A copy with a point added after the last.
    public Polyline added(float2 point) { Polyline r = this; r.add(point); return r; }

    public float local(float2 p)
    {
        if (count < 1)
            return 1e10;
        float2 first = p - points[0];
        float d = dot(first, first);
        // The length to draw: `until` of the whole.
        float total = 0.0;
        for (int i = 0; i + 1 < count; i++)
            total += length(points[i + 1] - points[i]);
        float left = until * total;
        for (int i = 0; i + 1 < count && left > 0.0; i++)
        {
            float2 ba = points[i + 1] - points[i];
            float segment = length(ba);
            // The last piece drawn stops part way along.
            float shown = min(left, segment) / max(segment, 1e-9);
            float2 pa = p - points[i];
            float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-9), 0.0, shown);
            float2 off = pa - ba * h;
            d = min(d, dot(off, off));
            left -= segment;
        }
        return sqrt(d) - width * 0.5;
    }
}

/// An arrow pointing right, `span` from tail to tip: a shaft `width` thick and a head `head`
/// long and twice `width` + `head` across at its back. Rotate it to point elsewhere.
/// @cost 4
public struct Arrow : IShape
{
    public Placement placement = Placement();
    public float span;
    public float width;
    public float head;

    public __init(float span, float width, float head) { this.span = span; this.width = width; this.head = head; }

    public float local(float2 p)
    {
        float shaftLength = max(span - head, 0.0);
        // The shaft from the tail to the head's back; the head a triangle pointing right.
        float2 q = p - float2(-span * 0.5 + shaftLength * 0.5, 0.0);
        float2 b = abs(q) - float2(shaftLength * 0.5, width * 0.5);
        float shaft = length(max(b, 0.0)) + min(max(b.x, b.y), 0.0);
        float2 tip = float2(span * 0.5, 0.0);
        float2 h = float2(p.y, tip.x - p.x);            // the head's own space: tip up at (0, 0)
        float2 sz = float2(width + head, head);
        float2 hq = float2(sz.x * 0.5, sz.y);
        h.x = abs(h.x);
        float2 ha = h - hq * clamp(dot(h, hq) / dot(hq, hq), 0.0, 1.0);
        float2 hb = h - hq * float2(clamp(h.x / hq.x, 0.0, 1.0), 1.0);
        float s = -sign(hq.y);
        float2 hd = min(float2(dot(ha, ha), s * (h.x * hq.y - h.y * hq.x)), float2(dot(hb, hb), s * (h.y - hq.y)));
        float headDistance = -sqrt(hd.x) * sign(hd.y);
        return min(shaft, headDistance);
    }
}

/// A squircle: between a box and an ellipse, `size` wide and high, `power` 2 an ellipse, 4 the
/// soft square of app icons, higher ever squarer. Its distance is close, not exact.
/// @cost 3
public struct Squircle : IShape
{
    public Placement placement = Placement();
    public float2 size;
    public float power;

    public __init(float2 size, float power = 4.0) { this.size = size; this.power = power; }

    public float local(float2 p)
    {
        float2 half = max(size * 0.5, float2(1e-4));
        float2 q = abs(p) / half;
        float n = max(power, 1.0);
        float r = pow(pow(q.x, n) + pow(q.y, n), 1.0 / n);
        // The radial distance in units of the shape, scaled back by the shorter half: close enough near the edge.
        return (r - 1.0) * min(half.x, half.y);
    }
}

/// An egg standing upright: a round bottom `radius` across and a pointed top, `tip` rounding its
/// point (Quilez's egg). The bottom circle's middle is the shape's centre.
/// @cost 3
public struct Egg : IShape
{
    public Placement placement = Placement();
    public float radius;
    public float tip;

    public __init(float radius, float tip = 0.0) { this.radius = radius; this.tip = tip; }

    public float local(float2 p)
    {
        const float k = sqrt(3.0);
        p.y = -p.y;
        p.x = abs(p.x);
        float r = radius - tip;
        float d = p.y < 0.0 ? length(p) - r
                : k * (p.x + r) < p.y ? length(float2(p.x, p.y - k * r))
                : length(float2(p.x + r, p.y)) - 2.0 * r;
        return d - tip;
    }
}

/// A number in seven-segment digits, like a clock or a scoreboard: `value` (its whole part, as a
/// positive number) in digits `height` tall, strokes `thickness` thick. `places` above 0 always
/// shows that many digits, zeros in front; 0 shows as many as it needs (up to 8). Centred on the
/// whole number. A shape: combo counters, BPM, a countdown.
///
///     c.paint(Digits(combo, 40, 5), colours.white);
/// @cost 9
public struct Digits : IShape
{
    public Placement placement = Placement();
    public float value;
    public float height;
    public float thickness;
    public int places = 0;

    public __init(float value, float height, float thickness = 0.0, int places = 0)
    {
        this.value = value; this.height = height; this.thickness = thickness > 0.0 ? thickness : height * 0.12; this.places = places;
    }

    /// How many digits it draws.
    public property int count
    {
        get
        {
            if (places > 0)
                return min(places, 8);
            float v = floor(abs(value));
            int n = 1;
            for (int k = 1; k < 8; k++)
                if (v >= pow(10.0, float(k)))
                    n = k + 1;
            return n;
        }
    }

    /// The segments lit for digit `d` (bits a to g: top, top right, bottom right, bottom, bottom left, top left, middle).
    /// @hidden
    public uint segmentsOf(int d)
    {
        switch (d)
        {
            case 0: return 0x3Fu;
            case 1: return 0x06u;
            case 2: return 0x5Bu;
            case 3: return 0x4Fu;
            case 4: return 0x66u;
            case 5: return 0x6Du;
            case 6: return 0x7Du;
            case 7: return 0x07u;
            case 8: return 0x7Fu;
            default: return 0x6Fu;
        }
    }

    /// The distance to digit `d` drawn with its middle at the origin.
    /// @hidden
    public float digitDistance(float2 p, int d)
    {
        float w = height * 0.55, h = height;
        float hx = w * 0.5 - thickness * 0.5, hy = h * 0.5 - thickness * 0.5;
        float gap = thickness * 0.6;   // the segments stop short of the corners, as on a real display
        uint lit = segmentsOf(d);
        float best = 1e10;
        // a, d, g: across; b, c, e, f: up and down.
        float4 seg[7] = {
            float4(-hx + gap, -hy, hx - gap, -hy),   // a
            float4(hx, -hy + gap, hx, -gap),         // b
            float4(hx, gap, hx, hy - gap),           // c
            float4(-hx + gap, hy, hx - gap, hy),     // d
            float4(-hx, gap, -hx, hy - gap),         // e
            float4(-hx, -hy + gap, -hx, -gap),       // f
            float4(-hx + gap, 0.0, hx - gap, 0.0)    // g
        };
        for (int k = 0; k < 7; k++)
        {
            if ((lit & (1u << uint(k))) == 0u)
                continue;
            float2 a = seg[k].xy, b = seg[k].zw;
            float2 pa = p - a, ba = b - a;
            float t = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-9));
            best = min(best, length(pa - ba * t) - thickness * 0.5);
        }
        return best;
    }

    public float local(float2 p)
    {
        int n = count;
        float advance = height * 0.55 + thickness * 1.5;
        float total = advance * float(n);
        // Which slot the point is over, counted from the left, and its neighbours.
        float fromLeft = p.x + total * 0.5;
        int slot = clamp(int(floor(fromLeft / advance)), 0, n - 1);
        float v = floor(abs(value));
        float d = 1e10;
        for (int k = max(slot - 1, 0); k <= min(slot + 1, n - 1); k++)
        {
            int fromRight = n - 1 - k;
            int digit = int(mod(floor(v / pow(10.0, float(fromRight))), 10.0));
            float2 middle = float2(-total * 0.5 + advance * (float(k) + 0.5), 0.0);
            d = min(d, digitDistance(p - middle, digit));
        }
        return d;
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

/// Where only one of the two shapes is, not both (exclusive or).
public struct Exclusive<A : IShape, B : IShape> : IShape
{
    public Placement placement = Placement();
    public A a;
    public B b;

    public float local(float2 p) { return sdf.xor(a.distance(p), b.distance(p)); }
}

/// A shape with its edge moved `amount` out (in if negative): sharp corners grow round.
public struct Grown<S : IShape> : IShape
{
    public Placement placement = Placement();
    public S shape;
    public float amount;

    public float local(float2 p) { return shape.distance(p) - amount; }
}

/// A shape copied over a grid, a copy every `spacing`; `count` (when above 0) keeps that many
/// either side of the middle on each axis (Quilez's limited repetition), else forever.
public struct Tiled<S : IShape> : IShape
{
    public Placement placement = Placement();
    public S shape;
    public float2 spacing;
    public float2 count;

    public float local(float2 p)
    {
        float2 id = round(p / spacing);
        if (count.x > 0.0) id.x = clamp(id.x, -count.x, count.x);
        if (count.y > 0.0) id.y = clamp(id.y, -count.y, count.y);
        return shape.distance(p - spacing * id);
    }
}

/// A shape folded about its centre: the right half copied over the left (`x`), the bottom over the top (`y`).
public struct Mirrored<S : IShape> : IShape
{
    public Placement placement = Placement();
    public S shape;
    public bool x;
    public bool y;

    public float local(float2 p)
    {
        if (x) p.x = abs(p.x);
        if (y) p.y = abs(p.y);
        return shape.distance(p);
    }
}

/// A shape pulled apart from its middle by `amount` on each axis, the gap filled straight
/// (Quilez's elongation): a circle stretched into a capsule, a cross into a longer cross.
public struct Elongated<S : IShape> : IShape
{
    public Placement placement = Placement();
    public S shape;
    public float2 amount;

    public float local(float2 p)
    {
        float2 half = amount * 0.5;
        float2 q = abs(p) - half;
        return shape.distance(sign(p) * max(q, 0.0)) + min(max(q.x, q.y), 0.0);
    }
}

/// A shape twisted round its centre, turning `turns` radians more for every unit out: a swirl.
/// Not an exact distance any more, so keep soft edges modest.
public struct Swirled<S : IShape> : IShape
{
    public Placement placement = Placement();
    public S shape;
    public float turns;

    public float local(float2 p) { return shape.distance(rotate(p, -length(p) * turns)); }
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
    /// @handle angle turn
    public P rotated(float angle) { P r = this; r.rotate(angle); return r; }

    /// A copy moved by `offset`.
    /// @handle offset move
    public P moved(float2 offset) { P r = this; r.move(offset); return r; }

    /// A copy `factor` times bigger round its centre.
    /// @handle factor scale
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

    /// Worked out once at `position`: a Measured that filled, and, paint and the rest reuse.
    public Measured measured(float2 position) { return Measured(distance(position), position); }

    /// The way out of the shape at `position`: a unit direction across its edge, away from the
    /// inside (the distance's slope, from four reads round the point). What light or a lens bends by.
    /// @cost 4
    public float2 outward(float2 position)
    {
        const float e = 0.5;
        float2 slope = float2(distance(position + float2(e, 0.0)) - distance(position - float2(e, 0.0)),
            distance(position + float2(0.0, e)) - distance(position - float2(0.0, e)));
        float size = length(slope);
        return size > 1e-6 ? slope / size : float2(0.0);
    }

    /// measured at the pixel being shaded, from the quad's centre.
    public Measured measured() { return measured(shading.canvas.centred); }

    /// `count` copies in a circle, each `radius` out from the centre.
    /// @cost 2
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
    /// @cost 1
    public SmoothUnion<S, T> melted<T : IShape>(T other, float blend) { SmoothUnion<S, T> r; r.placement = Placement(); r.a = this; r.b = other; r.blend = blend; return r; }

    /// Only where this shape and `other` overlap, the edge rounded over `blend` units.
    /// @cost 1
    public SmoothIntersection<S, T> meltedAnd<T : IShape>(T other, float blend) { SmoothIntersection<S, T> r; r.placement = Placement(); r.a = this; r.b = other; r.blend = blend; return r; }

    /// This shape with `other` cut out of it, the cut rounded over `blend` units.
    /// @cost 1
    public SmoothDifference<S, T> meltedMinus<T : IShape>(T other, float blend) { SmoothDifference<S, T> r; r.placement = Placement(); r.a = this; r.b = other; r.blend = blend; return r; }

    /// Only where one of this shape and `other` is, not both (exclusive or).
    public Exclusive<S, T> xor<T : IShape>(T other) { Exclusive<S, T> r; r.placement = Placement(); r.a = this; r.b = other; return r; }

    /// Its edge moved `amount` units out (in if negative): sharp corners come out rounded.
    public Grown<S> grown(float amount) { Grown<S> r; r.placement = Placement(); r.shape = this; r.amount = amount; return r; }

    /// Its edge moved `amount` units in: corners stay sharp, parts thinner than twice `amount` vanish.
    public Grown<S> shrunk(float amount) { return grown(-amount); }

    /// Copies over a grid, one every `spacing`, forever.
    /// @cost 1
    public Tiled<S> tiled(float2 spacing) { Tiled<S> r; r.placement = Placement(); r.shape = this; r.spacing = spacing; r.count = float2(0.0); return r; }

    /// Copies over a grid, one every `spacing`, forever.
    public Tiled<S> tiled(float spacing) { return tiled(float2(spacing)); }

    /// Copies over a grid, one every `spacing`, `count` of them either side of the middle on each axis.
    public Tiled<S> tiled(float2 spacing, float2 count) { Tiled<S> r = tiled(spacing); r.count = max(count, float2(1e-3)); return r; }

    /// Folded about its centre: the right half copied over the left (`x`), the bottom over the top (`y`).
    public Mirrored<S> mirrored(bool x = true, bool y = false) { Mirrored<S> r; r.placement = Placement(); r.shape = this; r.x = x; r.y = y; return r; }

    /// Pulled apart from its middle by `amount` on each axis, the gap filled straight.
    public Elongated<S> elongated(float2 amount) { Elongated<S> r; r.placement = Placement(); r.shape = this; r.amount = amount; return r; }

    /// Twisted round its centre, `turns` radians more for every unit out.
    public Swirled<S> swirled(float turns) { Swirled<S> r; r.placement = Placement(); r.shape = this; r.turns = turns; return r; }

    /// This and a measured shape together, this measured at its point.
    public Measured or(Measured other) { return measured(other.at).or(other); }

    /// Only where this and a measured shape overlap, this measured at its point.
    public Measured and(Measured other) { return measured(other.at).and(other); }

    /// This with a measured shape cut out of it, this measured at its point.
    public Measured minus(Measured other) { return measured(other.at).minus(other); }

    /// This and a measured shape melted together over `blend` units, this measured at its point.
    public Measured melted(Measured other, float blend) { return measured(other.at).melted(other, blend); }

    /// Only where this and a measured shape overlap, rounded over `blend` units, this measured at its point.
    public Measured meltedAnd(Measured other, float blend) { return measured(other.at).meltedAnd(other, blend); }

    /// This with a measured shape cut out, the cut rounded over `blend` units, this measured at its point.
    public Measured meltedMinus(Measured other, float blend) { return measured(other.at).meltedMinus(other, blend); }

    /// 1 inside, 0 outside, the edge smoothed over `soft` units.
    public float filled(float2 position, float soft = 1.0) { return sdf.filled(distance(position), soft); }

    /// 1 on a line `width` wide along the edge, 0 elsewhere.
    public float stroked(float2 position, float width, float soft = 1.0) { return sdf.stroked(distance(position), width, soft); }

    /// 1 inside, fading to 0 `width` units outside the edge (filled fades across it instead).
    public float feathered(float2 position, float width) { return sdf.feathered(distance(position), width); }

    /// 1 at the edge fading to 0 `reach` units away on both sides, shaped by `falloff` (higher is tighter).
    public float haloed(float2 position, float reach, float falloff = 1.0) { return sdf.haloed(distance(position), reach, falloff); }

    /// 1 on a line `width` wide just inside the edge, 0 elsewhere: the shape keeps its size
    /// (stroked is centred on the edge instead, half outside).
    public float outlined(float2 position, float width, float soft = 1.0) { return sdf.stroked(distance(position) + width * 0.5, width, soft); }
}

// ---- Measured: a shape worked out once ----------------------------------------

/// A shape already worked out at one point: its distance there, and the point. A shape is a
/// recipe that runs again for every filled, and or paint; measure it once you're done placing
/// it and everything after reuses the number. Copying one never works anything out again.
///
///     let ring = Circle(100).hollowed(10).measured();
///     let dot = Circle(30).moved(float2(100, 0)).measured();
///     c.paint(ring, colours.red);
///     c.paint(dot.and(ring), colours.green);       // a max, nothing measured again
///
/// It can't be moved or drawn somewhere else (a shadow, a trail): keep the shape for that.
public struct Measured : IMeasurable
{
    /// The signed distance at `at`: negative inside.
    public float d;
    /// The point it was measured at; a shape joined to it is measured there too.
    public float2 at;

    public __init(float d, float2 at) { this.d = d; this.at = at; }

    /// Itself: it was measured already.
    public Measured measured() { return this; }

    /// Itself: it only knows its own point, so `position` is not used. Keep the shape to measure elsewhere.
    public Measured measured(float2 position) { return this; }

    /// Just a shell round its edge, `thickness` out on each side (as a shape's hollowed).
    public Measured hollowed(float thickness) { return Measured(abs(d) - thickness, at); }

    /// The edge moved `amount` units out (d - amount): sharp corners come out rounded. One
    /// subtraction on the stored distance; the shape isn't worked out again.
    public Measured grown(float amount) { return Measured(d - amount, at); }

    /// The edge moved `amount` units in (d + amount): corners stay sharp, parts thinner than
    /// twice `amount` vanish. One addition on the stored distance.
    public Measured shrunk(float amount) { return Measured(d + amount, at); }

    /// This and `other` together; a shape is measured at the same point.
    public Measured or<T : IMeasurable>(T other) { return Measured(min(d, other.measured(at).d), at); }

    /// Only where this and `other` overlap; a shape is measured at the same point.
    public Measured and<T : IMeasurable>(T other) { return Measured(max(d, other.measured(at).d), at); }

    /// This with `other` cut out of it; a shape is measured at the same point.
    public Measured minus<T : IMeasurable>(T other) { return Measured(max(d, -other.measured(at).d), at); }

    /// This and `other` melted together over `blend` units; a shape is measured at the same point.
    public Measured melted<T : IMeasurable>(T other, float blend) { return Measured(sdf.melted(d, other.measured(at).d, blend), at); }

    /// Only where this and `other` overlap, the edge rounded over `blend` units; a shape is measured at the same point.
    public Measured meltedAnd<T : IMeasurable>(T other, float blend) { return Measured(sdf.meltedAnd(d, other.measured(at).d, blend), at); }

    /// This with `other` cut out of it, the cut rounded over `blend` units; a shape is measured at the same point.
    public Measured meltedMinus<T : IMeasurable>(T other, float blend) { return Measured(sdf.meltedMinus(d, other.measured(at).d, blend), at); }
}

// ---- What everything measurable can do ----------------------------------------

/// Drawing, once for shapes and Measured alike: a shape is measured at the pixel being shaded,
/// from the quad's centre (its overloads taking a position draw elsewhere); a Measured uses its number.
public extension<M : IMeasurable> M
{
    /// The signed distance: negative inside.
    public float distance() { return measured().d; }

    /// 1 inside, 0 outside, the edge smoothed over `soft` units.
    public float filled(float soft = 1.0) { return sdf.filled(measured().d, soft); }

    /// 1 on a line `width` wide along the edge, 0 elsewhere.
    public float stroked(float width, float soft = 1.0) { return sdf.stroked(measured().d, width, soft); }

    /// 1 on a line `width` wide just inside the edge, 0 elsewhere: the shape keeps its size
    /// (stroked is centred on the edge instead, half outside).
    public float outlined(float width, float soft = 1.0) { return sdf.stroked(measured().d + width * 0.5, width, soft); }

    /// 1 inside, fading to 0 `width` units outside the edge (filled fades across it instead).
    public float feathered(float width) { return sdf.feathered(measured().d, width); }

    /// 1 at the edge fading to 0 `reach` units away on both sides, shaped by `falloff` (higher is tighter).
    public float haloed(float reach, float falloff = 1.0) { return sdf.haloed(measured().d, reach, falloff); }

    /// A shadow hugging the edge on both sides, 63% back to 1 `reach` units out; multiply a colour by it.
    public float shadowed(float reach) { return sdf.shadowed(measured().d, reach); }

    /// Contour lines `spacing` units apart: 1 on a line, 1 - `depth` between; multiply a colour by it.
    public float contoured(float spacing, float depth = 0.4) { return sdf.contoured(measured().d, spacing, depth); }

    /// filled with an edge exactly one screen pixel wide, whatever the units.
    public float antialiased() { return sdf.antialiased(measured().d); }

    /// A neon glow, 1 `width` units from the edge and brighter nearer; not held to 1 (for blend.add).
    public float glowed(float width, float falloff = 1.0) { return sdf.glowed(measured().d, width, falloff); }

    /// The field drawn the way Inigo Quilez draws one, scaled from `reach`: for seeing a distance.
    public float4 shown(float reach = 80.0) { return sdf.shown(measured().d, reach); }
}
