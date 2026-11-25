// Images that outlive the frame. A comet circles the item; `shown` (the trail alone) is drawn
// fresh each frame from `before`, its own last frame ([History]), faded a little, so the comet
// leaves a trail. `seen` puts it over the paint, and is what the item shows. On
// the beat it drops a spot of paint into `paint` ([Kept]), which nothing clears: the spots pile up
// for as long as the item is shown, and start over when its size changes.
import xanadu;

[Kept] [Image(rgba16f)] RWTexture2D<float4> paint;
[Image(rgba16f)] RWTexture2D<float4> shown;
[History(shown)] RWTexture2D<float4> before;
[Image(rgba16f)] RWTexture2D<float4> seen;

/// How much of last frame's trail is left after a second.
[Default(0.2)] uniform float linger;

/// Where the comet is at `t`, in pixels from the item's centre.
float2 comet(float t, float2 size) { return float2(cos(t * 1.7), sin(t * 2.3)) * size * 0.38; }

[Pixels(8, 8)]
void fade(ItemInput i, Canvas canvas)
{
    float2 at = canvas.centred;
    float head = sdf.filled(length(at - comet(time, canvas.size)) - 14, 1.5);
    float4 trail = before[group.pixel] * pow(linger, dt);
    float4 lit = float4(colours.cyan.rgb, 1) * head;
    shown[group.pixel] = lit + trail * (1 - lit.a);

    // A spot of paint where the comet is on each beat, kept for good.
    float hit = beats.pulse(1.0, 0.08);
    float spot = sdf.filled(length(at - comet(floor(beat) * 0.6, canvas.size)) - 30, 2) * step(0.5, hit);
    if (spot > 0)
        paint[group.pixel] = float4(colours.hue(frac(floor(beat) * 0.13)).rgb, 1) * spot + paint[group.pixel] * (1 - spot);
}

[Pixels(8, 8)]
void compose(ItemInput i, Canvas canvas)
{
    float4 trail = shown[group.pixel];
    float4 under = paint[group.pixel] * 0.6 + float4(colours.midnightBlue.rgb, 1) * (1 - paint[group.pixel].a * 0.6);
    seen[group.pixel] = trail + under * (1 - trail.a);
}

void graph()
{
    fade();
    compose();
    present(seen);
}
