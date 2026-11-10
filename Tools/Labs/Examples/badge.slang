// Badge: a little emblem from the shape library, in layers: a rounded plate,
// a ring with a gap, a star cut out of a disc, ticks round the edge.
import xanadu;

float4 shade(ItemInput i)
{
    colour c = colours.midnightBlue;

    // Shadow, then the plate.
    Box plate = Box(float2(420, 300), 40);
    c = lerp(c, colours.black, plate.moved(float2(10, 14)).glow(30, 2) * 0.5);
    c = lerp(c, colours.darkSlateBlue, plate.fill());
    c = lerp(c, colours.slateBlue, plate.stroke(3));

    // A ring with a gap at the bottom, turning slowly.
    Arc ring = Arc(110, 14, 0.5);
    ring.rotate(time * 0.5);
    c = lerp(c, colours.gold, ring.fill());

    // Ticks round the ring.
    let ticks = Box(float2(14, 4), 2).repeat(24, 135);
    c = lerp(c, colours.goldenrod, ticks.fill());

    // A disc with a star cut out of it, the star's hole showing the plate.
    Circle disc = Circle(70);
    Star6 star = Star6(34);
    star.rotate(-time);
    c = lerp(c, colours.orange, disc.minus(star).fill());

    // A highlight across the top half of the disc.
    float shine = disc.and(Box(float2(140, 70)).moved(float2(0, -35))).fill();
    c.rgb += colours.white.rgb * shine * 0.15;

    // Corner screws: the plate's corners, inset.
    Circle screw = Circle(8);
    float screws = 0;
    screws = max(screws, screw.moved(float2(-180, -120)).fill());
    screws = max(screws, screw.moved(float2(180, -120)).fill());
    screws = max(screws, screw.moved(float2(-180, 120)).fill());
    screws = max(screws, screw.moved(float2(180, 120)).fill());
    c = lerp(c, colours.silver, screws);
    return c;
}
