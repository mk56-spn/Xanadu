// Each slot a copy of a quad round its particle (a dead one's folds to a point), stretched along
// its velocity and reaching past it for the glow. The look is the lab's GLOW: a capsule round
// the particle's spine (a circle when still), a soft core and a halo past it, faded by the
// colour's alpha.
import xanadu;

// Size at birth and at death, in pixels, and how far a moving one stretches (seconds of travel).
[Default(5, 2)] uniform float2 size;
[Default(0.01)] uniform float stretch;
[Default(0.5, 0.8, 1, 1)] uniform colour born;
[Default(0.1, 0.3, 1, 0)] uniform colour dies;

/// The particle the copy being drawn shows.
static Particle item;

/// How far through its life it is, 0..1.
float lived() { return saturate(item.age / max(item.life, 1e-4)); }

/// Its radius now, and half the length its velocity stretches it to.
float2 extent()
{
    return float2(length(item.velocity) * stretch * 0.5, max(lerp(size.x, size.y, lived()) * 0.5, 0.5));
}

[Draw(1, 1, shade: particleLook, copies: SLOTS)]
ItemVertex particle(ItemVertex v)
{
    item = slots[copy];
    if (item.alive == 0)
    {
        v.position = v.rest.position;
        return v;
    }
    float2 e = extent();
    float speed = length(item.velocity);
    float2 along = speed > 1e-3 ? item.velocity / speed : float2(1, 0);
    // Glows reach past the core.
    float glow = e.y * 2.5;
    float2 corner = (v.uv * 2 - 1) * float2(e.x + glow, glow);
    v.position = v.rest.position + item.position + along * corner.x + float2(-along.y, along.x) * corner.y;
    v.local = corner;
    colour c = lerp(born, dies, lived()).hueShifted(item.hue);
    v.colour = c;
    return v;
}

float4 particleLook(ItemInput i)
{
    item = slots[copy];
    float2 e = extent();
    float r = e.y;
    float2 q = float2(i.local.x - clamp(i.local.x, -e.x, e.x), i.local.y);
    float d = length(q) - r;
    float a = saturate(sdf.filled(d + r * 0.5, 1.5) * 0.8 + sdf.haloed(d + r, r * 2.5, 2.5));
    return float4(i.colour.rgb, i.colour.a * a);
}
