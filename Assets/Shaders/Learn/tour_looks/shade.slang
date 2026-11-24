// Library tour: the ready-made pieces in xanadu.looks, one per panel.
import xanadu;

float4 shade(ItemInput i)
{
    Step panel = steps.split(16, 2);
    if (panel.gap)
        return panel.backdrop;
    float2 p = panel.canvas.centred;
    colour c = colours.black;
    switch (panel.index)
    {
        case 0: c = looks.starfield(p, 0.6); break;
        case 1: { c = Linear(panel.canvas.size.y).rotated(PI / 2).coloured(colours.indigo, colours.black);
                  c.paint(looks.synthwaveFloor(p, 0.0, time), colours.hotPink, blend.add); break; }
        case 2: { c = colours.midnightBlue; float a = looks.aurora(p, 0.1);
                  c.paint(a, colours.springGreen.mixed(colours.mediumPurple, saturate(-p.y / 100)), blend.add); break; }
        case 3: { c = colours.teal.brightened(0.6); c.paint(looks.caustics(p, 60), colours.white, blend.add); break; }
        case 4: { c = colours.darkSlateGray; c.paint(looks.rain(p, 0.4), colours.lightBlue.withAlpha(0.7)); break; }
        case 5: { c = colours.midnightBlue; c.paint(looks.snow(p, 0.6), colours.white); break; }
        case 6: { c = colours.black; c.paint(looks.burst(p, frac(time * 0.4) * 1.0 + 0.2, 3, 28, 50), colours.gold, blend.add); break; }
        case 7: c = looks.plasma(p, 40); break;
        case 8: { c = Linear(panel.canvas.size.y).rotated(PI / 2).coloured(colours.indigo, colours.deepPink);
                  c.paint(looks.retroSun(p, 70), Linear(140).rotated(PI / 2).coloured(colours.yellow, colours.hotPink)); break; }
        case 9: { c = colours.gold; c.paint(Sunburst(18).rotated(time * 0.2), colours.orange); c.paint(Circle(30), colours.white); break; }
        case 10: { c = colours.black; let bolt = Lightning(float2(-80, -70), float2(70, 75), 2, floor(time * 8));
                   c.paint(bolt.haloed(24, 3), colours.dodgerBlue, blend.add); c.paint(bolt, colours.white); break; }
        case 11: { c = looks.starfield(p, 0.5, 3); c.paint(looks.burst(p, frac(time * 0.5) * 2.0 + 0.25, floor(time * 0.5), 20, 45), colours.hotPink, blend.add);
                   c.paint(looks.burst(p - float2(40, 30), frac(time * 0.5 + 0.5) * 2.0 + 0.25, 9, 20, 40), colours.cyan, blend.add); break; }
        case 12: { c = colours.black; for (int k = 0; k < 3; k++) c.paint(looks.hitRing(p, frac(time * 0.8) * 0.2 + k * 0.06, 60, 0.4, 6), colours.cyan, blend.add); break; }
        case 13: { c = colours.midnightBlue; float hue; float paper = looks.confetti(p - float2(0, 30), frac(time * 0.25) * 2 + 0.3, hue, 2, 30, 90);
                   c.paint(paper, colours.oklch(0.75, 0.15, hue)); break; }
        case 14: { c = colours.white; c.paint(looks.speedLines(p, float2(0), 30), colours.black); break; }
        case 15: { float2 uv = float2(0.2, 0.08) + panel.uv * float2(0.25, 0.3); let behind = pictures.screen();
                   c = behind.split(filter.glitch(uv, 0.8), float2(6, 0)); break; }
    }
    return panel.marked(c);
}
