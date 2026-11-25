// One copy of a quad for each thing drawn, in the order they stack: copy 0 is the field under
// it all (the item's own quad), then every trail segment, then every live particle, a quad
// round it stretched along its velocity. All in world pixels, `zoom` onto the item. The shade:
// the field over the background, a trail segment's soft line, or a particle in its look.
import xanadu;

static const uint FIELD = 0, SEGMENT = 1, PARTICLE = 2;

/// What copy `c` draws: the field, segment `segment` of slot `slot`'s trail, or slot `slot`.
struct Drawn
{
    uint kind;
    uint slot;
    uint segment;
};

Drawn drawnOf(uint c)
{
    Drawn d = { FIELD, 0, 0 };
    if (c == 0)
        return d;
    uint segments = lists[TRAILED] * (TRAIL - 1);
    if (c - 1 < segments)
    {
        d.kind = SEGMENT;
        d.slot = lists[TRAIL_LIST + (c - 1) / (TRAIL - 1)];
        d.segment = (c - 1) % (TRAIL - 1);
        return d;
    }
    d.kind = PARTICLE;
    d.slot = lists[ALIVE_LIST + c - 1 - segments];
    return d;
}

/// How far through its life it is, 0..1.
float lived(Particle p) { return saturate(p.age / max(p.life, 1e-4)); }

/// Its colour at its age: birth to death, its hue nudged, fading in.
colour colourOf(Emitter e, Particle p)
{
    colour c = lerp(e.born, e.dies, lived(p));
    if (p.hue != 0)
        c = c.hueShifted(p.hue);
    c.a *= e.fadeIn > 0 ? saturate(p.age / e.fadeIn) : 1;
    return c;
}

float sizeOf(Emitter e, Particle p) { return lerp(e.grows.x, e.grows.y, lived(p)) * p.scale; }

/// Trail segment k, from history sample k to k + 1, thinning and fading towards the tail:
/// its ends, half width and fade. False when there's none (too few samples, or a wrap's jump).
bool segmentOf(uint slot, Particle p, Emitter e, uint k, out float2 from, out float2 to, out float width, out float fade)
{
    uint head = lists[HEAD];
    uint samples = min(e.trail, TRAIL);
    from = k == 0 ? p.position : trails[slot * TRAIL + (head + TRAIL - k) % TRAIL];
    to = trails[slot * TRAIL + (head + TRAIL - k - 1) % TRAIL];
    fade = 1 - (float(k) + 0.5) / float(samples);
    width = max(sizeOf(e, p) * e.trailWidth * fade * 0.5, 0.35);
    float2 span = to - from;
    return k + 1 < samples && dot(span, span) <= WORLD.x * WORLD.x * 0.0625;
}

[Draw(1, 1, shade: particleLook, copies: drawn)]
ItemVertex particle(ItemVertex v)
{
    Drawn d = drawnOf(copy);
    if (d.kind == FIELD)
    {
        v.local = v.rest.centred / max(zoom, 1e-3);
        // Behind everything else: a deform writes depth, and the field is opaque.
        v.depth = 4096;
        return v;
    }

    Particle p = pool[d.slot];
    Emitter e = emitterOf(p.emitter);
    float2 corner = v.uv * 2 - 1;
    if (d.kind == SEGMENT)
    {
        float2 from, to;
        float width, fade;
        if (!segmentOf(d.slot, p, e, d.segment, from, to, width, fade))
        {
            // Every corner on one point: nothing drawn.
            v.position = v.rest.position;
            return v;
        }
        float2 span = to - from;
        float len = length(span);
        float2 along = len > 1e-4 ? span / len : float2(1, 0);
        float2 pixel = lerp(from, to, corner.x * 0.5 + 0.5) + float2(-along.y, along.x) * corner.y * (width + 1);
        v.position = v.rest.position + pixel * zoom;
        v.local = float2(0, corner.y * (width + 1));
        v.uv = float2(0, width);
        colour c = colourOf(e, p);
        c.a *= fade;
        v.colour = c;
        return v;
    }

    float radius = max(sizeOf(e, p) * 0.5, 0.5);
    float speed = length(p.velocity);
    float2 along = speed > 1e-3 ? p.velocity / speed : float2(1, 0);
    float stretch = speed * e.stretch * 0.5;
    float reach = particles.reachOf(e.look, radius);
    float2 at = float2(stretch + reach, reach) * corner;
    float2 pixel = p.position - along * stretch + along * at.x + float2(-along.y, along.x) * at.y;
    v.position = v.rest.position + pixel * zoom;
    v.local = at;
    // The capsule's half length and radius: the same at every corner, so shade reads them whole.
    v.uv = float2(stretch, radius);
    v.colour = colourOf(e, p);
    return v;
}

float4 particleLook(ItemInput i)
{
    Drawn d = drawnOf(copy);
    if (d.kind == FIELD)
    {
        float shown = 1 - exp(-fieldIn(lists[PARITY] * CELLS, i.local) * gain);
        return float4(background.rgb + tint.rgb * shown * tint.a, 1);
    }

    float a = d.kind == SEGMENT
        ? sdf.filled(abs(i.local.y) - i.uv.y, 1.2)
        : particles.cover(emitterOf(pool[d.slot].emitter).look, i.local, i.uv.x, i.uv.y);
    // The vertex colour alone: uv carries the sizes here, not a place on the item's texture.
    return float4(i.vertexColour.rgb, i.vertexColour.a * a);
}
