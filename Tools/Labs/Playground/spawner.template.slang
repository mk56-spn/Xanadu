// The spawner: `count` things, each one made by spawn(i) on the GPU every frame.
// Off-stage and hidden ones are thrown away; the rest go in one indirect draw.
// Thing: position (px from the centre), size, rotation, colour, hidden, index.
// shade(Thing t, Canvas canvas) is optional: leave it out for discs.
// static const bool additive = true; blends by adding (overlaps never shimmer).
import xanadu;

static const uint count = 20000;

Thing spawn(uint i)
{
    Thing t;
    float2 home = (random2(float2(i, 1)) - 0.5) * resolution * 1.2;
    float speed = lerp(-1.5, 1.5, random(float2(i, 2)));
    float angle = random(float2(i, 3)) * TAU + time * speed;
    t.position = home + 60 * float2(cos(angle), sin(angle));

    t.size = lerp(4, 14, pow(random(float2(i, 4)), 3));
    t.rotation = angle;
    t.colour = colours.hsv(frac(home.x / resolution.x * 0.5 + time * 0.05), 0.7, 1).withAlpha(0.8);

    // Clear a circle round the pointer.
    t.hidden = distance(t.position, mouse) < 120;
    return t;
}

float4 shade(Thing t, Canvas canvas)
{
    // A third discs, a third rings, a third triangles.
    float kind = random(float2(t.index, 5));
    float r = t.size.x / 2;
    float cover = kind < 0.33 ? Circle(r).filled()
                : kind < 0.66 ? Circle(r).hollowed(1.5).filled()
                : Triangle(r).filled();
    return t.colour.withAlpha(t.colour.a * cover);
}
