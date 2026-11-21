// A cloudy sky, built in six steps: from one layer of noise to drifting,
// lit clouds (xanadu.noise, xanadu.steps).
import xanadu;

[Default(0.4)] uniform float cover;
[Default(20)] uniform float wind;

float4 shade(ItemInput i)
{
    Step panel = steps.split(6);
    if (panel.gap)
        return panel.backdrop;

    // 1. One layer of noise: smooth hills and hollows about 120 px across, 0..1.
    let one = Simplex(120);
    if (panel.stopsAt(0))
        return panel.marked(colour(float3(one.amount())));

    // 2. Five layers (fbm): each twice as fine and half as strong. Now it looks like something.
    let layered = Simplex(160).fractal(5);
    if (panel.stopsAt(1))
        return panel.marked(colour(float3(layered.amount())));

    // 3. Warped by another noise: the clouds curl instead of sitting on a grid of blobs.
    let curled = layered.warped(Perlin(300), 60).evolved(time * 0.05).moved(float2(time * wind, 0));
    float n = curled.amount();
    if (panel.stopsAt(2))
        return panel.marked(colour(float3(n)));

    // 4. Cut by `cover`: below it is sky, above it cloud, with a soft edge.
    float cloud = smoothstep(cover, cover + 0.25, n);
    if (panel.stopsAt(3))
        return panel.marked(colour(float3(cloud)));

    // 5. Coloured: a sky gradient underneath, the clouds laid over it.
    colour c = Linear(panel.canvas.size.y).rotated(PI / 2).coloured(colours.steelBlue, colours.lightSkyBlue);
    c.paint(cloud, colours.white);
    if (panel.stopsAt(4))
        return panel.marked(c);

    // 6. Lit from above: the noise read a little lower is the cloud's underside, darker.
    float below = smoothstep(cover, cover + 0.25, curled.amount(panel.canvas.centred + float2(0, 14)));
    c.paint(cloud * saturate(below - cloud + 0.4), colours.slateGray.withAlpha(0.6));
    return panel.marked(c.debanded(i.pixel));
}
