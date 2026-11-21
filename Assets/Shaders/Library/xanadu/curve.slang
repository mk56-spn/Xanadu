// xanadu.curve: a cubic Bézier curve as a value.
//
//     let c = Bezier(a, b, c, d);   float2 at = c.at(t);   float2 dir = c.tangent(t);
module curve;
import xanadu.math;
import xanadu.stage;
import xanadu.shape;

/// A path in 2D: where it is `t` (0..1) along, and which way it heads. A curve spins into a
/// solid (curve.lathed(), xanadu.mesh) or stands up as a wall (curve.extruded(depth)).
public interface ICurve2
{
    /// The point `t` (0..1) along it.
    public float2 at(float t);
    /// Which way it heads `t` along (not normalised).
    public float2 tangent(float t);
}

public struct Bezier : ICurve2
{
    public float2 start;
    public float2 control1;
    public float2 control2;
    public float2 end;

    public __init(float2 start, float2 control1, float2 control2, float2 end)
    {
        this.start = start;
        this.control1 = control1;
        this.control2 = control2;
        this.end = end;
    }

    /// The point `t` (0..1) along the curve.
    public float2 at(float t)
    {
        float u = 1.0 - t;
        return u * u * u * start + 3.0 * u * u * t * control1 + 3.0 * u * t * t * control2 + t * t * t * end;
    }

    /// The curve's direction (not normalised) `t` along it.
    public float2 tangent(float t)
    {
        float u = 1.0 - t;
        return 3.0 * u * u * (control1 - start) + 6.0 * u * t * (control2 - control1) + 3.0 * t * t * (end - control2);
    }
}

/// A path through 3D: where it is `t` (0..1) along, and which way it heads. Anything that is one
/// gives a tube or a ribbon along it (xanadu.mesh): spine.tube(radius), spine.ribbon(width).
public interface ICurve3
{
    /// The point `t` (0..1) along it.
    public float3 at(float t);
    /// Which way it heads `t` along (not normalised).
    public float3 tangent(float t);
}

/// A cubic Bézier curve in 3D: points in pixels, x right, y down, z into the screen.
public struct Bezier3 : ICurve3
{
    public float3 start;
    public float3 control1;
    public float3 control2;
    public float3 end;

    public __init(float3 start, float3 control1, float3 control2, float3 end)
    {
        this.start = start;
        this.control1 = control1;
        this.control2 = control2;
        this.end = end;
    }

    /// The point `t` (0..1) along the curve.
    public float3 at(float t)
    {
        float u = 1.0 - t;
        return u * u * u * start + 3.0 * u * u * t * control1 + 3.0 * u * t * t * control2 + t * t * t * end;
    }

    /// The curve's direction (not normalised) `t` along it.
    public float3 tangent(float t)
    {
        float u = 1.0 - t;
        return 3.0 * u * u * (control1 - start) + 6.0 * u * t * (control2 - control1) + 3.0 * t * t * (end - control2);
    }
}

/// A cubic Bézier drawn as a line `width` thick with round ends: the curve cut into `segments`
/// straight pieces (16 is smooth at most sizes). 0 for a bare line to stroke or halo. A shape.
///
///     c.paint(BezierLine(Bezier(a, b, c, d), 3), colours.white);
/// @cost 8
public struct BezierLine : IShape
{
    public Placement placement = Placement();
    public Bezier curve;
    public float width;
    public int segments = 16;
    /// How much of it is drawn, 0..1 from its start: tracedTo animates it drawing itself on.
    public float until = 1.0;

    public __init(Bezier curve, float width = 0.0, int segments = 16) { this.curve = curve; this.width = width; this.segments = segments; }

    /// A copy drawn only from its start to `progress` (0..1) along: a line drawing itself on.
    public BezierLine tracedTo(float progress) { BezierLine r = this; r.until = saturate(progress); return r; }

    public float local(float2 p)
    {
        int n = clamp(segments, 2, 64);
        float d = 1e10;
        float2 previous = curve.start;
        if (until <= 0.0)
            return 1e10;
        for (int k = 1; k <= n; k++)
        {
            float2 next = curve.at(min(float(k) / float(n), until));
            float2 pa = p - previous, ba = next - previous;
            float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-9));
            float2 off = pa - ba * h;
            d = min(d, dot(off, off));
            previous = next;
            if (float(k) / float(n) >= until)
                break;
        }
        return sqrt(d) - width * 0.5;
    }
}

/// A smooth path in 3D through up to 16 points, in order (a Catmull-Rom spline: it passes
/// through every point, unlike a Bézier's controls). Add the points, then use it as a curve.
///
///     Spline3 path;
///     path.add(float3(-200, 0, 0)); path.add(float3(0, -120, 80)); path.add(float3(200, 0, 0));
///     let rope = path.tube(12);
public struct Spline3 : ICurve3
{
    public float3 points[16];
    public int count = 0;

    public __init() { count = 0; }

    /// Adds a point after the last; past 16 they're dropped.
    [mutating] public void add(float3 point)
    {
        if (count < 16)
        {
            points[count] = point;
            count++;
        }
    }

    /// A copy with a point added after the last.
    public Spline3 added(float3 point) { Spline3 r = this; r.add(point); return r; }

    /// The four points around segment `i`: the ends repeat their neighbour.
    /// @hidden
    public void around(int i, out float3 p0, out float3 p1, out float3 p2, out float3 p3)
    {
        p0 = points[max(i - 1, 0)];
        p1 = points[i];
        p2 = points[min(i + 1, count - 1)];
        p3 = points[min(i + 2, count - 1)];
    }

    public float3 at(float t)
    {
        if (count < 2)
            return count == 1 ? points[0] : float3(0.0);
        float s = saturate(t) * float(count - 1);
        int i = min(int(floor(s)), count - 2);
        float f = s - float(i);
        float3 p0, p1, p2, p3;
        around(i, p0, p1, p2, p3);
        return 0.5 * (2.0 * p1 + (p2 - p0) * f + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * f * f + (3.0 * p1 - p0 - 3.0 * p2 + p3) * f * f * f);
    }

    public float3 tangent(float t)
    {
        if (count < 2)
            return float3(1.0, 0.0, 0.0);
        float s = saturate(t) * float(count - 1);
        int i = min(int(floor(s)), count - 2);
        float f = s - float(i);
        float3 p0, p1, p2, p3;
        around(i, p0, p1, p2, p3);
        return 0.5 * ((p2 - p0) + 2.0 * (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * f + 3.0 * (3.0 * p1 - p0 - 3.0 * p2 + p3) * f * f) * float(count - 1);
    }
}

/// A coil round the upright axis: `radius` round, `height` tall (top to bottom), `turns` times
/// round. A spring, a spiral staircase, DNA.
public struct Helix : ICurve3
{
    public float radius;
    public float height;
    public float turns;

    public __init(float radius, float height, float turns) { this.radius = radius; this.height = height; this.turns = turns; }

    public float3 at(float t)
    {
        float a = t * turns * TAU;
        return float3(sin(a) * radius, (t - 0.5) * height, -cos(a) * radius);
    }

    public float3 tangent(float t)
    {
        float a = t * turns * TAU, k = turns * TAU;
        return float3(cos(a) * radius * k, height, sin(a) * radius * k);
    }
}

/// A smooth path in 2D through up to 16 points, in order (Catmull-Rom: it passes through each),
/// drawn `width` thick with round ends: 0 for a bare path to stroke or halo. A shape.
///
///     c.paint(Spline(4).added(float2(-200, 0)).added(float2(0, -80)).added(float2(200, 40)), colours.white);
/// @cost 10
public struct Spline : IShape, ICurve2
{
    public Placement placement = Placement();
    public float2 points[16];
    public int count = 0;
    public float width = 0.0;
    /// How much of it is drawn, 0..1 from its start: tracedTo animates it drawing itself on.
    public float until = 1.0;

    public __init(float width = 0.0) { count = 0; this.width = width; }

    /// A copy drawn only from its start to `progress` (0..1) along: a path drawing itself on.
    public Spline tracedTo(float progress) { Spline r = this; r.until = saturate(progress); return r; }

    /// Adds a point after the last; past 16 they're dropped.
    [mutating] public void add(float2 point)
    {
        if (count < 16)
        {
            points[count] = point;
            count++;
        }
    }

    /// A copy with a point added after the last.
    public Spline added(float2 point) { Spline r = this; r.add(point); return r; }

    /// The point `t` (0..1) along it.
    public float2 at(float t)
    {
        if (count < 2)
            return count == 1 ? points[0] : float2(0.0);
        float s = saturate(t) * float(count - 1);
        int i = min(int(floor(s)), count - 2);
        float f = s - float(i);
        float2 p0 = points[max(i - 1, 0)], p1 = points[i], p2 = points[min(i + 1, count - 1)], p3 = points[min(i + 2, count - 1)];
        return 0.5 * (2.0 * p1 + (p2 - p0) * f + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * f * f + (3.0 * p1 - p0 - 3.0 * p2 + p3) * f * f * f);
    }

    /// Which way it heads `t` along (not normalised).
    public float2 tangent(float t)
    {
        if (count < 2)
            return float2(1.0, 0.0);
        float s = saturate(t) * float(count - 1);
        int i = min(int(floor(s)), count - 2);
        float f = s - float(i);
        float2 p0 = points[max(i - 1, 0)], p1 = points[i], p2 = points[min(i + 1, count - 1)], p3 = points[min(i + 2, count - 1)];
        return 0.5 * ((p2 - p0) + 2.0 * (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * f + 3.0 * (3.0 * p1 - p0 - 3.0 * p2 + p3) * f * f) * float(count - 1);
    }

    public float local(float2 p)
    {
        if (count < 1)
            return 1e10;
        // Each stretch between two points cut into 6 straight pieces.
        int n = clamp((count - 1) * 6, 1, 90);
        float d = 1e10;
        float2 previous = at(0.0);
        if (until <= 0.0)
            return 1e10;
        for (int k = 1; k <= n; k++)
        {
            float2 next = at(min(float(k) / float(n), until));
            float2 pa = p - previous, ba = next - previous;
            float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-9));
            float2 off = pa - ba * h;
            d = min(d, dot(off, off));
            previous = next;
            if (float(k) / float(n) >= until)
                break;
        }
        return sqrt(d) - width * 0.5;
    }
}

