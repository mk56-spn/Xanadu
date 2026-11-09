// xanadu.curve: cubic Bézier points and tangents (ported from the legacy bezierHelpers).
module curve;

public float2 cubicBezier(float2 p0, float2 p1, float2 p2, float2 p3, float t)
{
    float u = 1.0 - t;
    return u * u * u * p0 + 3.0 * u * u * t * p1 + 3.0 * u * t * t * p2 + t * t * t * p3;
}

public float2 cubicBezierDerivative(float2 p0, float2 p1, float2 p2, float2 p3, float t)
{
    float u = 1.0 - t;
    return 3.0 * u * u * (p1 - p0) + 6.0 * u * t * (p2 - p1) + 3.0 * t * t * (p3 - p2);
}
