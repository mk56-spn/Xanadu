// xanadu.curve: a cubic Bézier curve as a value.
//
//     let c = Bezier(a, b, c, d);   float2 at = c.at(t);   float2 dir = c.tangent(t);
module curve;

public struct Bezier
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
