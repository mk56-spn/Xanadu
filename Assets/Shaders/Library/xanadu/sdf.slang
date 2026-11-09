// xanadu.sdf: 2D signed distances (negative inside) and how to draw them.
// Ported from the legacy 2DSDF and SDFConstructs includes; most shapes are
// Inigo Quilez's (iquilezles.org/articles/distfunctions2d).
module sdf;
import xanadu.math;

public float sdCircle(float2 p, float r)
{
    return length(p) - r;
}

public float sdCross(float2 p, float2 b, float r)
{
    p = abs(p);
    p = (p.y > p.x) ? p.yx : p.xy;
    float2 q = p - b;
    float k = max(q.y, q.x);
    float2 w = (k > 0.0) ? q : float2(b.y - p.x, -k);
    return sign(k) * length(max(w, 0.0)) + r;
}

/// An arc of a ring: n is the direction of its gap's normal (cos, sin), r its radius, th its thickness.
public float sdRing(float2 p, float2 n, float r, float th)
{
    p.x = abs(p.x);
    p = float2(n.x * p.x - n.y * p.y, n.y * p.x + n.x * p.y);
    return max(abs(length(p) - r) - th * 0.5,
               length(float2(p.x, max(0.0, abs(r - p.y) - th * 0.5))) * sign(p.x));
}

public float sdBox(float2 p, float2 b)
{
    float2 d = abs(p) - b;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

/// A box with a radius per corner: r = (bottom right, top right, bottom left, top left), y down.
public float sdRoundedBox(float2 p, float2 b, float4 r)
{
    r.xy = (p.x > 0.0) ? r.xy : r.zw;
    r.x = (p.y > 0.0) ? r.x : r.y;
    float2 q = abs(p) - b + r.x;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
}

public float sdChamferBox(float2 p, float2 b, float chamfer)
{
    p = abs(p) - b;
    p = (p.y > p.x) ? p.yx : p.xy;
    p.y += chamfer;

    const float k = 1.0 - sqrt(2.0);
    if (p.y < 0.0 && p.y + p.x * k < 0.0)
        return p.x;
    if (p.x < p.y)
        return (p.x + p.y) * sqrt(0.5);
    return length(p);
}

public float sdEquilateralTriangle(float2 p, float r)
{
    const float k = sqrt(3.0);
    p.x = abs(p.x) - r;
    p.y = p.y + r / k;
    if (p.x + k * p.y > 0.0)
        p = float2(p.x - k * p.y, -k * p.x - p.y) / 2.0;
    p.x -= clamp(p.x, -2.0 * r, 0.0);
    return -length(p) * sign(p.y);
}

public float sdParallelogram(float2 p, float wi, float he, float sk)
{
    float2 e = float2(sk, he);
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

public float sdHexagram(float2 p, float r)
{
    const float4 k = float4(-0.5, 0.8660254038, 0.5773502692, 1.7320508076);
    p = abs(p);
    p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
    p -= 2.0 * min(dot(k.yx, p), 0.0) * k.yx;
    p -= float2(clamp(p.x, r * k.z, r * k.w), r);
    return length(p) * sign(p.y);
}

/// A crescent: d between the circles' centres, ra the outer radius, rb the bite's.
public float sdMoon(float2 p, float d, float ra, float rb)
{
    p.y = abs(p.y);
    float a = (ra * ra - rb * rb + d * d) / (2.0 * d);
    float b = sqrt(max(ra * ra - a * a, 0.0));
    if (d * (p.x * b - p.y * a) > d * d * max(b - p.y, 0.0))
        return length(p - float2(a, b));

    return max(length(p) - ra, -(length(p - float2(d, 0)) - rb));
}

/// Folds p into one of n slices round the origin, to repeat a shape in a circle.
public float2 opRepPolar(float2 p, float n)
{
    float angle = atan2(p.y, p.x);
    float radius = length(p);
    float slice = TAU / n;
    angle = mod(angle, slice) - slice * 0.5;
    return float2(cos(angle), sin(angle)) * radius;
}

/// A shell r thick around a shape's edge.
public float opOnion(float shape, float r)
{
    return abs(shape) - r;
}

/// Coverage at a distance: 1 inside, fading to 0 over `smoothness` outside.
public float sdfAlpha(float dist, float smoothness)
{
    return 1.0 - smoothstep(0.0, smoothness, dist);
}

public float4 renderSDF(float dist, float4 fillColor, float smoothness)
{
    fillColor.a *= sdfAlpha(dist, smoothness);
    return fillColor;
}

/// 1 at the rim fading to 0 towards the core, over radius × edge, shaped by power.
public float outerGlowSDF(float d, float radius, float edge, float power)
{
    float t = 1.0 - saturate(-d / (radius * edge));
    return pow(t, power);
}

/// The shape filled, with an outline `outlineWidth` wide drawn inside its edge.
public float4 renderSDFWithOutline(float dist, float4 fillColor, float4 outlineColor, float outlineWidth, float smoothness)
{
    float innerEdge = -outlineWidth;
    float shapeAlpha = 1.0 - smoothstep(0.0, smoothness, dist);
    float outlineMix = smoothstep(innerEdge - smoothness, innerEdge + smoothness, dist);
    float4 color = lerp(outlineColor, fillColor, outlineMix);
    color.a *= shapeAlpha;
    return color;
}

public static const int SHAPE_CIRCLE = 0;
public static const int SHAPE_CROSS = 1;
public static const int SHAPE_RING = 2;
public static const int SHAPE_BOX = 3;
public static const int SHAPE_ROUNDED_BOX = 4;
public static const int SHAPE_CHAMFER_BOX = 5;
public static const int SHAPE_EQUIL_TRIANGLE = 6;
public static const int SHAPE_PARALLELOGRAM = 7;
public static const int SHAPE_HEXAGRAM = 8;
public static const int SHAPE_MOON = 9;

/// Any shape by id, its parameters packed in prm (see each case).
public float sdShape(int id, float2 p, float4 prm)
{
    switch (id)
    {
    case SHAPE_CIRCLE: return sdCircle(p, prm.x);                           // x radius
    case SHAPE_CROSS: return sdCross(p, prm.xy, prm.z);                     // xy half size, z rounding
    case SHAPE_RING: return sdRing(p, prm.xy, prm.z, prm.w);                // xy gap normal, z radius, w thickness
    case SHAPE_BOX: return sdBox(p, prm.xy);                                // xy half size
    case SHAPE_ROUNDED_BOX: return sdRoundedBox(p, prm.xy, prm.zzww);       // xy half size, z right corners, w left corners
    case SHAPE_CHAMFER_BOX: return sdChamferBox(p, prm.xy, prm.z);          // xy half size, z chamfer
    case SHAPE_EQUIL_TRIANGLE: return sdEquilateralTriangle(p, prm.x);      // x radius
    case SHAPE_PARALLELOGRAM: return sdParallelogram(p, prm.x, prm.y, prm.z); // x width, y height, z skew
    case SHAPE_HEXAGRAM: return sdHexagram(p, prm.x);                       // x radius
    case SHAPE_MOON: return sdMoon(p, prm.x, prm.y, prm.z);                 // x centre distance, y outer, z inner radius
    }
    return 1e38;
}
