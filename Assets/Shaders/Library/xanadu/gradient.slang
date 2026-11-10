// xanadu.gradient: how far along a gradient from `from` to `to` a point is,
// 0..1, all under `gradient.`. Feed the result to lerp or a ramp.
//
//     gradient.linear(p, a, b)   gradient.radial(p, centre, edge)
module gradient;
import xanadu.math;

public namespace gradient
{
    /// Along the line from `from` (0) to `to` (1).
    public float linear(float2 p, float2 from, float2 to)
    {
        float2 dir = to - from;
        float lengthSq = dot(dir, dir);
        return lengthSq > 0.0001 ? dot(p - from, dir) / lengthSq : 0.0;
    }

    /// Out from `centre` (0) to the circle through `edge` (1).
    public float radial(float2 p, float2 centre, float2 edge)
    {
        float radius = distance(centre, edge);
        return radius > 0.0001 ? distance(p, centre) / radius : 0.0;
    }

    /// Round `centre`, starting (0) in the direction of `start` and going all the way round (1).
    public float conical(float2 p, float2 centre, float2 start)
    {
        float2 target = p - centre;
        float2 axis = start - centre;
        return mod(atan2(target.y, target.x) - atan2(axis.y, axis.x), TAU) / TAU;
    }

    /// Out from `centre` (0) in squares, to the square through `edge` (1).
    public float square(float2 p, float2 centre, float2 edge)
    {
        float2 d = abs(p - centre), r = abs(edge - centre);
        float radius = max(r.x, r.y);
        return radius > 0.0001 ? max(d.x, d.y) / radius : 0.0;
    }
}
