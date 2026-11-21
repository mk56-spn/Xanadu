// Library tour: the second pass — grading, print looks, beats, springs, digits and more shapes.
import xanadu;

float4 shade(ItemInput i)
{
    Step panel = steps.split(12, 2);
    if (panel.gap)
        return panel.backdrop;
    float2 p = panel.canvas.centred;
    float x = panel.uv.x;
    // A test picture: a hue sweep across, dark to light down.
    colour pic = colours.hsv(x, 0.7, 1 - panel.uv.y * 0.8);
    colour c = colours.midnightBlue;
    switch (panel.index)
    {
        case 0: c = pic.levelled(0.2, 0.8, 1.3); break;
        case 1: c = pic.graded(float3(0.05, 0, 0.1), float3(1.1), float3(1.1, 1, 0.85)); break;
        case 2: c = pic.duotoned(colours.navy, colours.gold); break;
        case 3: c = pic.sepiaToned(); break;
        case 4: { c = colours.white; float ink = 1 - Radial(60).amount(p); c.paint(filter.halftone(i.pixel, ink, 6), colours.black); break; }
        case 5: { c = colours.ivory; float ink = Linear(120).amount(p); c.paint(filter.hatched(i.pixel, ink), colours.darkSlateGray); break; }
        case 6: { float s = 1 + 0.3 * beats.pulse(); c.paint(Circle(30).scaled(s), colours.hotPink); c.paint(Box(float2(12)).moved(float2(50 * beats.alternate(), 40)), colours.white); break; }
        case 7: { float t = frac(time * 0.5) * 2; float y = 1 - panel.uv.y; float d = abs(y - shaping.spring(x * 2, 2, 4) * 0.5); c.paint(1 - aastep(0.01, d), colours.white);
                  c.paint(Circle(10).moved(float2(0, -shaping.spring(t, 2, 4) * 40 + 20)), colours.gold); break; }
        case 8: c.paint(Digits(floor(time * 10) + 1234, 40, 4).filled(), colours.lime); break;
        case 9: { c.paint(Arrow(120, 14, 30).rotated(time * 0.5), colours.orange); break; }
        case 10: { c.paint(Squircle(float2(90, 70), 5), colours.skyBlue); c.paint(Egg(25, 4).moved(float2(0, 10)), colours.white); break; }
        case 11: { c = Linear(panel.canvas.size.x).coloured(colours.blue, colours.yellow); if (panel.uv.y > 0.5) c = Linear(panel.canvas.size.x).mixed(colours.blue, colours.yellow);
                   c = c.enlivened(0.5) + pic.levelled(0, 1) * 0; break; }
    }
    return panel.marked(c);
}
