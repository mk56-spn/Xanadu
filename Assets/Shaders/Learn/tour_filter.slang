// Library tour: each panel reads the shader tool's own code editor (left of the screen) through one
// filter from xanadu.filter.
import xanadu;

float4 shade(ItemInput i, Canvas canvas)
{
    Step panel = steps.split(12, 2);
    if (panel.gap)
        return panel.backdrop;
    float2 cellUv = panel.uv;
    int k = panel.index;
    // A patch of code: x 0.2..0.45, y 0.08..0.38 of the screen.
    float2 uv = float2(0.2, 0.08) + cellUv * float2(0.25, 0.3);
    let s = pictures.screen();
    colour c = colours.black;
    switch (k)
    {
        case 0: c = s.sampled(uv); break;
        case 1: c = s.blurred(uv, 10); break;
        case 2: c = s.smeared(uv, float2(30, 0)); break;
        case 3: c = s.zoomBlurred(uv, float2(0.32, 0.23), 0.3); break;
        case 4: c = s.spinBlurred(uv, float2(0.32, 0.23), 0.3); break;
        case 5: c = s.sharpened(uv, 1); break;
        case 6: c = colour(float3(s.edges(uv))); break;
        case 7: c = s.embossed(uv, 2); break;
        case 8: c = s.pixelated(uv, 6); break;
        case 9: c = s.aberrated(filter.curved(cellUv, 0.2) * float2(0.25, 0.3) + float2(0.2, 0.08), 12) * filter.scanlines(i.pixel) * filter.vignette(cellUv, 0.8); break;
        case 10: c = s.painterly(uv, 6) + s.bloomed(uv, 0.5, 8); break;
        case 11: c = colour(float3(s.sampled(uv + filter.shockwave(i.pixel, canvas.position, 60, 20, 15) / s.size).luminance())).dithered(i.pixel, 2) ; break;
    }
    c.rgb += filter.grain(i.pixel, 0.0) + filter.ign(i.pixel) * 0 + filter.lens(uv, 0.1).x * 0 + filter.inside(uv) * 0 + filter.heatHaze(i.pixel).x * 0 + filter.ripple(uv, 0.5).x * 0;
    return colour(c.rgb, 1).debanded(i.pixel);
}
