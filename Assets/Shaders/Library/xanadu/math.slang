// xanadu.math: constants and the GLSL-isms effects reach for.
module math;

public static const float PI = 3.14159265359;
public static const float TAU = 6.28318530718;

/// GLSL's mod: the remainder with the divisor's sign (HLSL's fmod truncates instead).
public float mod(float x, float y) { return x - y * floor(x / y); }
public float2 mod(float2 x, float2 y) { return x - y * floor(x / y); }
public float2 mod(float2 x, float y) { return x - y * floor(x / y); }
public float3 mod(float3 x, float3 y) { return x - y * floor(x / y); }

/// p turned by angle radians.
public float2 rotate(float2 p, float angle)
{
    float c = cos(angle), s = sin(angle);
    return float2(c * p.x + s * p.y, -s * p.x + c * p.y);
}

/// A repeatable pseudo-random number in 0..1 for a 2D seed.
public float random(float2 co)
{
    return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453);
}
