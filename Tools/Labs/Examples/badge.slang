// Badge: a little emblem from the shape library, in layers: a rounded plate,
// a ring with a gap, a star cut out of a disc, ticks round the edge.
import xanadu;

float4 shade(Canvas canvas)
{
    colour c = colours.midnightBlue;

    // Shadow, then the plate.
    Box plate = Box(float2(420, 300), 40);
    c.paint(plate.moved(float2(10, 14)).filled(30), colours.black.withAlpha(0.5));
    c.paint(plate, colours.darkSlateBlue);
    c.paint(plate.stroked(3), colours.slateBlue);

    // A ring with a gap at the bottom, turning slowly.
    Arc ring = Arc(110, 14, 0.5);
    ring.rotate(time * 0.5);
    c.paint(ring, colours.gold);

    // Ticks round the ring.
    c.paint(Box(float2(14, 4), 2).repeated(24, 135), colours.goldenrod);

    // A disc with a star cut out of it (corners softened), the hole showing the plate.
    Circle disc = Circle(70);
    Star6 star = Star6(34);
    star.rotate(-time);
    c.paint(disc.meltedMinus(star, 4), colours.orange);

    // A highlight across the top half of the disc.
    c.paint(disc.and(Box(float2(140, 70)).moved(float2(0, -35))), colours.white.withAlpha(0.15), blend.screen);

    // Corner screws, inset from the plate's corners.
    Circle screw = Circle(8);
    c.paint(screw.moved(float2(-180, -120)), colours.silver);
    c.paint(screw.moved(float2(180, -120)), colours.silver);
    c.paint(screw.moved(float2(-180, 120)), colours.silver);
    c.paint(screw.moved(float2(180, 120)), colours.silver);
    return c;
}
