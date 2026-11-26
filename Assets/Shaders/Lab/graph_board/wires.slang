// Each straight piece of wire, one copy of a quad drawn onto the frame.
import xanadu;

/// Piece `copy`'s quad, round its line; `local` is along it from a, and across it.
[Draw(1, 1, shade: wireLook, copies: wireCount)]
ItemVertex wire(ItemVertex v)
{
    Wire w = wires[copy];
    float run = distance(w.a, w.b);
    float2 along = run > 0 ? (w.b - w.a) / run : float2(1, 0);
    float reach = WIRE_REACH * zoom;
    float2 local = float2(lerp(-reach, run + reach, v.uv.x), lerp(-reach, reach, v.uv.y));
    v.position = onTarget(v, w.a + along * local.x + float2(-along.y, along.x) * local.y);
    v.local = local;
    return v;
}

float4 wireLook(ItemInput i)
{
    Wire w = wires[copy];
    float run = distance(w.a, w.b);
    float x = i.local.x;
    // Round at both ends, so the pieces of a bent wire join.
    float d = distance(float2(clamp(x, 0, run), 0), i.local);
    float s = w.along + clamp(x, 0, run);
    float core = saturate(1.5 * zoom + 0.5 - d);
    float3 tone = lerp(w.from.rgb, w.to.rgb, w.total > 0 ? saturate(s / w.total) : 0);

    // The dot nearest here, drawn by the piece its centre is on (its quad reaches past its ends for the rest).
    float dot = 1e9;
    float ahead = nearestDot(w.along + x) - w.along;
    if (ahead >= 0 && ahead < run)
        dot = distance(i.local, float2(ahead, 0));
    if (w.both > 0.5)
    {
        float back = w.total - nearestDot(w.total - (w.along + x)) - w.along;
        if (back >= 0 && back < run)
            dot = min(dot, distance(i.local, float2(back, 0)));
    }
    float r = DOT * zoom;
    float disc = saturate(r + 0.5 - dot);
    float glow = 0.12 * exp(-max(dot - r, 0) / (DOT_GLOW * zoom));
    float3 bright = lerp(tone, 1, 0.5);
    float3 rgb = lerp(tone, bright, saturate(disc + glow));
    return float4(rgb, saturate(max(max(core, disc), glow)));
}
