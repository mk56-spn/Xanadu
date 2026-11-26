// xanadu.probe: what a graph's probe nodes call (Slang/ProbeNodes.cs). In the shader editor every draw gets a pass that
// runs the draw's own vertex function at sampled times over the timeline's window and measures how each copy moved, so
// the timeline shows motion however the code wrote it. Nothing here is for a stack to call itself.
module probe;

import xanadu.stage;

/// The side of the square quad a probe stands a draw on, in pixels, at the origin.
/// @hidden
public static const float PROBE_QUAD = 1024;

/// Corner `index` of a draw's grid of `across` × `down` cells, as the draw's own vertex stage builds it, on the probe's quad.
/// @hidden
public ItemVertex probeCorner(uint index, uint across, uint down)
{
    uint cell = index / 6, k = index % 6;
    float2 corner = float2((0x1A >> k) & 1, (0x34 >> k) & 1);
    float2 uv = (float2(cell % across, cell / across) + corner) / float2(across, down);
    float2 position = uv * PROBE_QUAD;
    ItemVertex v = { position, uv, float4(1.0), position, canvasFrom(float2(PROBE_QUAD, 0), float2(0, PROBE_QUAD), float2(0), uv), index, 0.0, float3(0.0), float3(0.0) };
    return v;
}

/// Puts the clock at `at` (0..1) through the window: from `window.x` seconds, `window.y` long, `window.z` beats a second.
/// @hidden
public void probeClock(float4 window, float at)
{
    time = window.x + window.y * at;
    beat = time * window.z;
    dt = 0.0;
    unit = 1.0;
}

/// Twice the signed area of a triangle: 0 for one with nothing to measure by.
/// @hidden
public float probeArea(float2 a, float2 b, float2 c)
{
    float2 e = b - a, f = c - a;
    return e.x * f.y - f.x * e.y;
}

/// How three points moved from where they were (`q`) to where they are (`p`), as the plane map p = M q + d:
/// (d.x, d.y) in quads, M's turn in radians, and its scale. All zero when the first three make no triangle.
/// @hidden
public float4 probeMotion(float2 q0, float2 q1, float2 q2, float2 p0, float2 p1, float2 p2)
{
    float2 e1 = q1 - q0, e2 = q2 - q0, f1 = p1 - p0, f2 = p2 - p0;
    float det = e1.x * e2.y - e2.x * e1.y;
    if (abs(det) < 1e-6)
        return float4(0.0);
    float a = e2.y / det, b = -e2.x / det, c = -e1.y / det, e = e1.x / det;
    float m00 = f1.x * a + f2.x * c, m01 = f1.x * b + f2.x * e;
    float m10 = f1.y * a + f2.y * c, m11 = f1.y * b + f2.y * e;
    float2 q = (q0 + q1 + q2) / 3.0, p = (p0 + p1 + p2) / 3.0;
    float2 d = p - float2(m00 * q.x + m01 * q.y, m10 * q.x + m11 * q.y);
    return float4(d / PROBE_QUAD, atan2(m10 - m01, m00 + m11), sqrt(abs(m00 * m11 - m01 * m10)));
}
