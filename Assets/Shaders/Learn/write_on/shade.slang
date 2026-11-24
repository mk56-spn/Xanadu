// A path drawing itself on, in six steps (xanadu.curve, xanadu.steps): the points, straight
// lines through them, a smooth spline through them, traced part way, a glowing pen tip, and
// the whole thing looping in time.
import xanadu;

Spline path(float scale)
{
    // A loop-the-loop signature, laid out for a panel 250 px across.
    Spline s = Spline(4 * scale);
    float2 pts[8] = { float2(-100, 30), float2(-60, -40), float2(-20, 30), float2(0, -50),
                      float2(30, -10), float2(10, 30), float2(60, 10), float2(100, -30) };
    for (int k = 0; k < 8; k++)
        s.add(pts[k] * scale);
    return s;
}

float4 shade(ItemInput i)
{
    Step panel = steps.split(6);
    if (panel.gap)
        return panel.backdrop;
    float scale = panel.canvas.size.x / 250;
    Spline s = path(scale);
    colour c = colours.midnightBlue;

    // 1. The points it must pass through.
    for (int k = 0; k < s.count; k++)
        c.paint(Circle(5 * scale).moved(s.points[k]).stroked(2), colours.white);
    if (panel.stopsAt(0))
        return panel.marked(c);

    // 2. Straight lines through them: a Polyline.
    Polyline straight = Polyline(2 * scale);
    for (int k = 0; k < s.count; k++)
        straight.add(s.points[k]);
    if (panel.stopsAt(1))
    {
        c.paint(straight, colours.gray);
        return panel.marked(c);
    }

    // 3. A smooth curve through them instead: a Spline passes through every point.
    if (panel.stopsAt(2))
    {
        c.paint(s, colours.gold);
        return panel.marked(c);
    }

    // 4. Traced part way: tracedTo(0.6) draws the first 60% of it.
    if (panel.stopsAt(3))
    {
        c.paint(s.tracedTo(0.6), colours.gold);
        return panel.marked(c);
    }

    // 5. A pen tip glowing where the drawing has got to.
    float progress = panel.stopsAt(4) ? 0.6 : saturate(frac(time * 0.4) * 1.3);
    c.paint(s.tracedTo(progress), colours.gold);
    let tip = Circle(3 * scale).moved(s.at(progress));
    c.paint(tip.haloed(30 * scale, 3), colours.orange, blend.add);
    c.paint(tip, colours.white);
    if (panel.stopsAt(4))
        return panel.marked(c);

    // 6. Looping in time: it draws on, holds a moment, and starts again.
    return panel.marked(c);
}
