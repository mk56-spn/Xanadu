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
    float beat = pulses(s);
    if (w.both > 0.5)
        beat = max(beat, pulses(w.total - s));
    float core = saturate(1.5 * zoom + 0.5 - d);
    float head = beat * exp(-d * d / (16 * zoom * zoom));
    float light = saturate(0.15 * exp(-d / (6 * zoom)) + head);
    return float4(lerp(pulse.rgb, wireTone.rgb, core * (1 - head)), max(core, light));
}
