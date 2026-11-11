// xanadu.math: constants and the GLSL-isms effects reach for.
module math;

public static const float PI = 3.14159265359;
public static const float TAU = 6.28318530718;

/// GLSL's mod: the remainder with the divisor's sign (HLSL's fmod truncates instead).
public float mod(float x, float y) { return x - y * floor(x / y); }
public float2 mod(float2 x, float2 y) { return x - y * floor(x / y); }
public float2 mod(float2 x, float y) { return x - y * floor(x / y); }
public float3 mod(float3 x, float3 y) { return x - y * floor(x / y); }

/// p turned round the origin by angle radians, clockwise on screen (y down), as shapes turn.
public float2 rotate(float2 position, float angle)
{
    float c = cos(angle), s = sin(angle);
    return float2(c * position.x - s * position.y, s * position.x + c * position.y);
}

// Repeatable random numbers in 0..1 from a seed: the seed's exact bits go
// through Jarzynski and Olano's PCG hashes (jcgt.org/published/0009/03/02), so
// big, negative or fractional seeds are as random as small ones (the old
// sin(dot(…)) hash went blocky past a few thousand). Each takes its own salt,
// so random(id), random2(id) and random3(id) don't repeat each other.

uint3 pcg3(uint3 v)
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    v ^= v >> 16u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    return v;
}

float3 unit(uint3 v) { return float3(v >> 8u) * (1.0 / 16777216.0); }

/// A random number in 0..1 for a seed.
public float random(float seed) { return unit(pcg3(uint3(asuint(seed), 0x9e3779b9u, 0x85ebca6bu))).x; }

/// A random number in 0..1 for a 2D seed (a cell's id, a position).
public float random(float2 seed) { return unit(pcg3(uint3(asuint(seed), 0x27d4eb2fu))).x; }

/// A random point in 0..1 for a 2D seed: each axis independent.
public float2 random2(float2 seed) { return unit(pcg3(uint3(asuint(seed), 0x165667b1u))).xy; }

/// Three random numbers in 0..1 for a 2D seed (a colour, a point and a size, …).
public float3 random3(float2 seed) { return unit(pcg3(uint3(asuint(seed), 0x9e3779b9u))); }
