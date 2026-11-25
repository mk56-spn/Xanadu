// A glow round each hot box, one copy of a quad drawn onto the frame.
import xanadu;

/// Hot box `copy`'s quad, the box grown by its glow.
[Draw(1, 1, shade: hotLook, copies: hotCount)]
ItemVertex hotDrawn(ItemVertex v)
{
    Hot h = hot[copy];
    float reach = HOT_REACH * zoom;
    float2 p = lerp(h.low - reach, h.high + reach, v.uv);
    v.position = onTarget(v, p);
    v.local = p;
    return v;
}

float4 hotLook(ItemInput i)
{
    Hot h = hot[copy];
    float2 outside = max(abs(i.local - (h.low + h.high) * 0.5) - (h.high - h.low) * 0.5, 0);
    return float4(h.tone.rgb, h.tone.a * exp(-length(outside) / (14 * zoom)));
}
