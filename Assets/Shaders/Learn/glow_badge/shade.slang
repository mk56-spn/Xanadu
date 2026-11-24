// A glowing badge, built in six steps: each panel shows the picture as it was
// after that step (xanadu.steps). Read top to bottom; the numbered dots in a
// panel's corner say which step it stops at.
import xanadu;

[Default(colours.gold)] uniform colour tint;

float4 shade(ItemInput i)
{
    Step panel = steps.split(6);
    if (panel.gap)
        return panel.backdrop;

    // 1. One shape, as its distance: blue inside, orange outside, a line every few pixels.
    //    Everything below is drawn from this one number.
    let star = Star(5, 60, 0.5).rotated(time * 0.3);
    let badge = star.melted(Circle(38), 12);
    if (panel.stopsAt(0))
        return panel.marked(badge.shown(30));

    // 2. Filled: 1 inside, 0 outside, the edge smoothed over a pixel.
    colour c = colours.midnightBlue;
    c.paint(badge, tint);
    if (panel.stopsAt(1))
        return panel.marked(c);

    // 3. An outline just inside the edge, and a hole cut through the middle.
    c.paint(badge.outlined(4), colours.white);
    c.paint(Circle(16), colours.midnightBlue);
    if (panel.stopsAt(2))
        return panel.marked(c);

    // 4. A glow: the distance again, fading away from the edge, added as light.
    c.paint(badge.haloed(40, 2), tint, blend.add);
    if (panel.stopsAt(3))
        return panel.marked(c);

    // 5. A ring of dots round it: one small circle repeated 12 times.
    let dots = Circle(4).repeated(12, 82).rotated(-time * 0.5);
    c.paint(dots, colours.white);
    if (panel.stopsAt(4))
        return panel.marked(c);

    // 6. A beat: everything swells on each beat and the ring flashes.
    float pulse = shaping.impulse(frac(beat), 6);
    c.paint(Circle(82).stroked(2 + 6 * pulse), tint.withAlpha(0.3 + 0.7 * pulse), blend.add);
    return panel.marked(c);
}
