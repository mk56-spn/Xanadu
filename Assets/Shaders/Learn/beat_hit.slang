// A screen effect for a hit, built in six steps: what's behind, pushed by a
// shockwave, split into colour fringes, flashed and darkened at the edges
// (xanadu.filter). In a level, run it from the beat (frac(beat)); the preview
// has no song, so here it hits every two seconds.
import xanadu;

float4 shade(ItemInput i)
{
    Step panel = steps.split(6);
    if (panel.gap)
        return panel.backdrop;
    let behind = pictures.screen();
    // Each panel reads the same patch of the screen: the shader tool's code, left of the preview.
    float2 uv = float2(0.2, 0.08) + panel.uv * float2(0.25, 0.3);
    float hit = frac(time * 0.5);   // 0 on the hit, rising to 1 just before the next (frac(beat) in a level)

    // 1. What's behind, as it is.
    if (panel.stopsAt(0))
        return panel.marked(behind.sampled(uv));

    // 2. A shockwave: a ring growing out from the middle, pushing pixels outwards as it passes.
    float2 centre = float2(0.5) * panel.canvas.size;
    float2 here = panel.uv * panel.canvas.size;
    float2 push = filter.shockwave(here, centre, 120 * ease.outCubic(hit), 26, 24 * (1 - hit));
    float2 pushed = uv + push / behind.size;
    if (panel.stopsAt(1))
        return panel.marked(behind.sampled(pushed));

    // 3. Colour fringes: red and blue pulled apart, strongest right on the beat.
    colour c = behind.aberrated(pushed, 14 * (1 - hit));
    if (panel.stopsAt(2))
        return panel.marked(c);

    // 4. A flash: a quick impulse of light on the beat.
    c.rgb += shaping.impulse(hit, 14) * 0.35;
    if (panel.stopsAt(3))
        return panel.marked(c);

    // 5. Edges darkened, so the eye stays in the middle.
    c.brighten(filter.vignette(panel.uv, 0.7, 0.6));
    if (panel.stopsAt(4))
        return panel.marked(c);

    // 6. A zoom blur that fades out after the beat, and film grain.
    colour rushed = behind.zoomBlurred(pushed, float2(0.325, 0.23), 0.15 * (1 - hit));
    c.rgb = lerp(c.rgb, rushed.rgb, 0.5 * (1 - hit));
    c.rgb += filter.grain(i.pixel, 0.04);
    return panel.marked(c);
}
