// Draws with copies. `place` puts a ring of dots in `dots` and counts the ones lit this beat into
// `lit`, on the GPU; `dot` is drawn once per lit dot, the count read straight from `lit` (an
// indirect draw: it never comes back to the CPU). `spokes` is drawn `spokeCount` times, a count
// the CPU knows. Each copy sees its number as `copy`.
import xanadu;

static const uint MOST = 64;

struct Dot
{
    float2 at;      // pixels from the item's centre
    float hue;
    float size;
};

[Size(MOST)] List<Dot> dots;
Counter lit;

/// How many of the ring's dots there are.
[Default(40)] uniform int ring;
/// Spokes from the centre, a count the CPU hands the draw.
[Default(12)] uniform int spokeCount;

/// One thread: the dots lit this beat, packed at the front, and how many.
[Pass(1, over: 1)]
void place(uint i)
{
    uint n = 0;
    for (uint k = 0; k < min(uint(ring), MOST); k++)
    {
        // Each beat lights a different half of them.
        if (random(float2(k, floor(beat))) < 0.5)
            continue;
        float a = k * TAU / max(ring, 1) + time * 0.2;
        Dot d;
        d.at = float2(cos(a), sin(a)) * 150;
        d.hue = float(k) / max(ring, 1);
        d.size = 18 + 10 * beats.pulse();
        dots[n++] = d;
    }
    lit[0] = n;
}

[Draw(1, 1, shade: dotLook, copies: lit)]
ItemVertex dot(ItemVertex v)
{
    Dot d = dots[copy];
    v.position = v.rest.position + d.at + (v.uv - 0.5) * d.size * 2;
    v.local = (v.uv - 0.5) * d.size * 2;
    return v;
}

float4 dotLook(ItemInput i)
{
    Dot d = dots[copy];
    return float4(colours.hsv(d.hue, 0.7, 1).rgb, Circle(d.size).filled(i.local, 1));
}

[Draw(1, 1, shade: spokeLook, copies: spokeCount)]
ItemVertex spoke(ItemVertex v)
{
    float a = copy * TAU / max(spokeCount, 1) - time * 0.1;
    float2 along = float2(cos(a), sin(a)), side = float2(-along.y, along.x);
    v.position = v.rest.position + along * (40 + v.uv.x * 180) + side * (v.uv.y - 0.5) * 6;
    return v;
}

float4 spokeLook(ItemInput i)
{
    return float4(1, 1, 1, 0.35);
}

void graph()
{
    place();
    spoke(frame);
    dot(frame);
}
