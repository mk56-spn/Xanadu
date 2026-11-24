// A synthwave level background, built in six steps from xanadu.looks: sky,
// stars, sun, the glowing floor, mountains, then a pulse on every beat.
// Change a step's numbers and watch only that panel and the ones after it move.
import xanadu;

[Default(colours.hotPink)] uniform colour neon;
[Default(0.12)] uniform float horizon;
[Default(1)] uniform float speed;

float4 shade(ItemInput i)
{
    Step panel = steps.split(6);
    if (panel.gap)
        return panel.backdrop;
    float2 p = panel.canvas.centred;
    float h = panel.canvas.size.y;
    float horizonY = horizon * h;

    // 1. The sky: a gradient from deep blue at the top into purple at the horizon.
    colour c = Linear(h).rotated(PI / 2).moved(float2(0, horizonY - h * 0.5)).coloured(colours.midnightBlue, colours.indigo);
    c.paint(Linear(h * 0.6).rotated(PI / 2).moved(float2(0, horizonY - h * 0.3)).amount() * 0.6, colours.mediumVioletRed, blend.screen);
    if (panel.stopsAt(0))
        return panel.marked(c);

    // 2. Stars, only above the horizon, fading towards it.
    float sky = 1 - linearstep(horizonY - h * 0.3, horizonY, p.y);
    c.paint(looks.starfield(p, 0.5, 3).luminance() * sky, colours.white, blend.add);
    if (panel.stopsAt(1))
        return panel.marked(c);

    // 3. The sun, sitting on the horizon: its gaps scroll down, a yellow-to-pink gradient.
    float2 sunAt = p - float2(0, horizonY - h * 0.18);
    float sun = looks.retroSun(sunAt, h * 0.24) * step(p.y, horizonY);
    colour sunColour = Linear(h * 0.48).rotated(PI / 2).coloured(sunAt, colours.yellow, colours.deepPink);
    c.paint(sun, sunColour);
    c.paint(Circle(h * 0.24).haloed(sunAt, h * 0.2, 3) * step(p.y, horizonY), colours.deepPink, blend.add);
    if (panel.stopsAt(2))
        return panel.marked(c);

    // 4. The floor: black below the horizon, its grid rolling towards you.
    c.paint(step(horizonY, p.y), colours.black.brightened(0.05));
    float grid = looks.synthwaveFloor(p, horizon, time * speed);
    c.paint(grid, neon, blend.add);
    if (panel.stopsAt(3))
        return panel.marked(c);

    // 5. Mountains on the horizon: noise along x as a silhouette, outlined in neon.
    float ridge = horizonY - h * (0.05 + 0.12 * noise.ridged(float2(p.x / (h * 0.35), 3.7), 4));
    float mountain = step(ridge, p.y) * step(p.y, horizonY);
    c.paint(mountain, colours.black.brightened(0.08));
    c.paint(sdf.stroked(p.y - ridge, 2.5) * step(p.y, horizonY), neon.brightened(1.4));
    if (panel.stopsAt(4))
        return panel.marked(c);

    // 6. On the beat: the floor flashes and the sun swells (frac(beat) in a level; time here).
    float hit = frac(time * 0.5);
    float pulse = shaping.impulse(hit, 10);
    c.paint(grid * pulse, colours.white, blend.add);
    c.paint(Circle(h * 0.24 * (1 + 0.06 * pulse)).haloed(sunAt, h * 0.3, 2) * pulse * step(p.y, ridge), colours.yellow, blend.add);
    c.brighten(filter.vignette(panel.uv, 0.5));
    return panel.marked(c.debanded(i.pixel));
}
