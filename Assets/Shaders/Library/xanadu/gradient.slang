// xanadu.gradient: where a point falls along a gradient from p1 to p2, 0..1
// (ported from the legacy gradientLogic include).
module gradient;
import xanadu.math;

public static const int GRADIENT_LINEAR = 0;
public static const int GRADIENT_RADIAL = 1;
public static const int GRADIENT_CONICAL = 2;
public static const int GRADIENT_SQUARE = 3;

public float gradientT(float2 uv, float2 p1, float2 p2, int mode)
{
    switch (mode)
    {
    case GRADIENT_RADIAL:
    {
        float radius = distance(p1, p2);
        return radius > 0.0001 ? distance(uv, p1) / radius : 0.0;
    }
    case GRADIENT_CONICAL:
    {
        float2 target = uv - p1;
        float2 axis = p2 - p1;
        return mod(atan2(target.y, target.x) - atan2(axis.y, axis.x), TAU) / TAU;
    }
    case GRADIENT_SQUARE:
    {
        float2 d = abs(uv - p1), r = abs(p2 - p1);
        float radius = max(r.x, r.y);
        return radius > 0.0001 ? max(d.x, d.y) / radius : 0.0;
    }
    default:
    {
        float2 dir = p2 - p1;
        float lengthSq = dot(dir, dir);
        return lengthSq > 0.0001 ? dot(uv - p1, dir) / lengthSq : 0.0;
    }
    }
}
