// DEFORM: once per live particle (the engine folds dead slots away). A quad round
// it, stretched along its velocity and reaching past it for the glow.
import xanadu;

// Size at birth and at death, in pixels, and how far a moving one stretches (seconds of travel).
[Default(5, 2)] uniform float2 size;
[Default(0.01)] uniform float stretch;
[Default(0.5, 0.8, 1, 1)] uniform colour born;
[Default(0.1, 0.3, 1, 0)] uniform colour dies;

/// How far through its life it is, 0..1.
float lived() { return saturate(item.age / max(item.life, 1e-4)); }

/// Its radius now, and half the length its velocity stretches it to.
float2 extent()
{
    return float2(length(item.velocity) * stretch * 0.5, max(lerp(size.x, size.y, lived()) * 0.5, 0.5));
}

ItemVertex deform(ItemVertex v)
{
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
