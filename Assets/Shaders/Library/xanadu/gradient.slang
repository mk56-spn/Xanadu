// xanadu.gradient: gradients as values, centred then placed like shapes. Each
// says how far along it the pixel is, 0..1; colour it, paint it or use the
// amount:
//
//     c.paint(Linear(400).rotated(PI / 2), colours.navy, colours.black);   // top to bottom
//     c.paint(Radial(200).anchored(anchor.top), colours.gold, colours.transparent);
//     float t = Conical().rotated(time).amount();
//     colour sky = Linear(600).coloured(colours.navy, colours.purple, colours.orange);   // three stops
//     c.paint(Radial(40).repeated(1).moved(float2(time * 30, 0)), colours.white, colours.black);   // rings
//
// The kinds: Linear, Reflected, Radial (round or an ellipse), Focal, Conical,
// Diamond, Square and Spiral; gradient.corners for a colour per corner.
module gradient;
import xanadu.math;
import xanadu.stage;
import xanadu.shape;

/// A gradient: how far along it a point is, in its own space. Placed like a
/// shape (IPlaced), so move, rotate, scale and anchor work on it.
/// @fits float colour
public interface IGradient : IPlaced
{
    /// How far along a point in the gradient's own space is: 0 at its start, 1 at its end, unclamped.
    public float local(float2 p);
}

/// From left (0) to right (1) across `length` pixels, centred on the quad.
/// Rotate it for other directions: rotated(PI / 2) runs top to bottom.
public struct Linear : IGradient
{
    public Placement placement = Placement();
    public float length;

    public __init(float length) { this.length = length; }

    public float local(float2 p) { return length > 0.0001 ? p.x / length + 0.5 : 0.0; }
}

/// Out from the centre (0) to a circle `radius` pixels round it (1). A float2
/// `radius` makes an ellipse, that wide and that high.
public struct Radial : IGradient
{
    public Placement placement = Placement();
    public float2 radius;

    public __init(float radius) { this.radius = float2(radius); }
    public __init(float2 radius) { this.radius = radius; }

    public float local(float2 p) { return length(p / max(radius, 0.0001)); }
}

/// Round the centre (conic, angular, sweep): 0 pointing right, going clockwise
/// (y down) all the way round to 1. Rotate it to start elsewhere.
public struct Conical : IGradient
{
    public Placement placement = Placement();

    public __init() {}

    public float local(float2 p) { return mod(atan2(p.y, p.x), TAU) / TAU; }
}

/// Out from a line through the centre (0) to `length / 2` either side (1):
/// a linear gradient mirrored about its middle. Upright until rotated.
public struct Reflected : IGradient
{
    public Placement placement = Placement();
    public float length;

    public __init(float length) { this.length = length; }

    public float local(float2 p) { return length > 0.0001 ? abs(p.x) / (length * 0.5) : 0.0; }
}

/// Out from the centre (0) in diamonds, to the one whose corners are `radius` away (1).
public struct Diamond : IGradient
{
    public Placement placement = Placement();
    public float radius;

    public __init(float radius) { this.radius = radius; }

    public float local(float2 p) { return radius > 0.0001 ? (abs(p.x) + abs(p.y)) / radius : 0.0; }
}

/// Out from the centre (0) in squares, to the one whose sides are `radius` away (1).
/// A float2 `radius` makes oblongs.
public struct Square : IGradient
{
    public Placement placement = Placement();
    public float2 radius;

    public __init(float radius) { this.radius = float2(radius); }
    public __init(float2 radius) { this.radius = radius; }

    public float local(float2 p)
    {
        float2 r = max(radius, 0.0001);
        return max(abs(p.x) / r.x, abs(p.y) / r.y);
    }
}

/// Out from `focus` (0) to a circle `radius` round the centre (1): a radial
/// gradient whose bright spot is off-centre (SVG's fx, fy). `focus` is from the
/// centre in pixels and is kept just inside the circle.
public struct Focal : IGradient
{
    public Placement placement = Placement();
    public float radius;
    public float2 focus;

    public __init(float radius, float2 focus) { this.radius = radius; this.focus = focus; }

    public float local(float2 p)
    {
        float r = max(radius, 0.0001);
        float2 f = focus;
        float reach = length(f);
        if (reach > r * 0.999)
            f *= r * 0.999 / reach;
        // From the focus through p to the circle: p is 1/k of the way, where |f + k d| = r.
        float2 d = p - f;
        float dd = dot(d, d);
        if (dd < 1e-8)
            return 0.0;
        float fd = dot(f, d);
        return dd / (sqrt(fd * fd - dd * (dot(f, f) - r * r)) - fd);
    }
}

/// Round and out at once: the amount climbs with the angle (0 pointing right,
/// clockwise) and with the distance, a whole turn every `pitch` pixels out,
/// wrapping back to 0. `arms` spirals wind together.
public struct Spiral : IGradient
{
    public Placement placement = Placement();
    public float pitch;
    public float arms;

    public __init(float pitch, float arms = 1.0) { this.pitch = pitch; this.arms = arms; }

    public float local(float2 p)
    {
        float turn = mod(atan2(p.y, p.x), TAU) / TAU;
        return frac(turn * arms + (pitch > 0.0001 ? length(p) / pitch : 0.0));
    }
}

/// Another gradient spread past its end: repeated starts it again, mirrored
/// runs it back and forth. Made by `g.repeated(times)` and `g.mirrored(times)`.
public struct Spread<G : IGradient> : IGradient
{
    public Placement placement = Placement();
    public G gradient;
    public float times;
    public bool mirror;

    public float local(float2 p)
    {
        float t = gradient.local(gradient.placement.toLocal(p)) * times;
        return mirror ? 1.0 - abs(mod(t, 2.0) - 1.0) : frac(t);
    }
}

// ---- What every gradient can do ----------------------------------------------

public extension<G : IGradient> G
{
    /// How far along it `position` is, 0..1.
    public float amount(float2 position) { return saturate(local(placement.toLocal(position))); }

    /// How far along it the pixel being shaded is, 0..1, from the quad's centre.
    public float amount() { return amount(shading.canvas.centred); }

    /// `from` at its start fading to `to` at its end, at `position`.
    public float4 coloured(float2 position, float4 from, float4 to) { return lerp(from, to, amount(position)); }

    /// `from` at its start fading to `to` at its end, at the pixel being shaded.
    public float4 coloured(float4 from, float4 to) { return coloured(shading.canvas.centred, from, to); }

    /// Three stops evenly spaced: `from` at the start, `via` halfway, `to` at the end.
    public float4 coloured(float2 position, float4 from, float4 via, float4 to) { return gradient.stops(amount(position), from, via, to); }

    /// Three stops evenly spaced, at the pixel being shaded.
    public float4 coloured(float4 from, float4 via, float4 to) { return coloured(shading.canvas.centred, from, via, to); }

    /// Four stops evenly spaced, at `position`.
    public float4 coloured(float2 position, float4 a, float4 b, float4 c, float4 d) { return gradient.stops(amount(position), a, b, c, d); }

    /// Four stops evenly spaced, at the pixel being shaded.
    public float4 coloured(float4 a, float4 b, float4 c, float4 d) { return coloured(shading.canvas.centred, a, b, c, d); }

    /// The gradient run `times` over its length, starting again each time (CSS's repeating-…).
    public Spread<G> repeated(float times = 1.0)
    {
        Spread<G> s;
        s.placement = Placement();
        s.gradient = this;
        s.times = times;
        s.mirror = false;
        return s;
    }

    /// The gradient run `times` over its length, back and forth (SVG's reflect).
    public Spread<G> mirrored(float times = 1.0)
    {
        Spread<G> s = repeated(times);
        s.mirror = true;
        return s;
    }
}

public namespace gradient
{
    /// Three stops evenly spaced along `t` (0..1): `from`, `via` at 0.5, `to`.
    public float4 stops(float t, float4 from, float4 via, float4 to)
    {
        float x = saturate(t) * 2.0;
        return x < 1.0 ? lerp(from, via, x) : lerp(via, to, x - 1.0);
    }

    /// Four stops evenly spaced along `t` (0..1), at 0, 1/3, 2/3 and 1.
    public float4 stops(float t, float4 a, float4 b, float4 c, float4 d)
    {
        float x = saturate(t) * 3.0;
        return x < 1.0 ? lerp(a, b, x) : x < 2.0 ? lerp(b, c, x - 1.0) : lerp(c, d, x - 2.0);
    }

    /// `t` between stops at the given places along it (each 0..1, in order):
    /// `from` until `at.x`, `via` at `at.y`, `to` from `at.z` on.
    public float4 stops(float t, float3 at, float4 from, float4 via, float4 to)
    {
        if (t <= at.y)
            return lerp(from, via, saturate((t - at.x) / max(at.y - at.x, 1e-5)));
        return lerp(via, to, saturate((t - at.y) / max(at.z - at.y, 1e-5)));
    }

    /// A colour per corner of the quad, blended across it: top left, top right,
    /// bottom left, bottom right (y down), at `uv` (0..1 across the quad).
    public float4 corners(float2 uv, float4 topLeft, float4 topRight, float4 bottomLeft, float4 bottomRight)
    {
        return lerp(lerp(topLeft, topRight, uv.x), lerp(bottomLeft, bottomRight, uv.x), uv.y);
    }

    /// A colour per corner of the quad, at the pixel being shaded.
    public float4 corners(float4 topLeft, float4 topRight, float4 bottomLeft, float4 bottomRight)
    {
        float2 uv = shading.canvas.centred / max(shading.canvas.size, 0.0001) + 0.5;
        return corners(uv, topLeft, topRight, bottomLeft, bottomRight);
    }
}
