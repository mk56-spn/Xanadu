// Library tour: every kind of noise in xanadu.noise, one per panel: value, Perlin, simplex, Worley,
// then fractal, ridged, billowed and warped, the plain functions, Voronoi cells and curl.
import xanadu;

float4 shade(ItemInput i, Canvas canvas)
{
    Step panel = steps.split(12, 2);
    if (panel.gap)
        return panel.backdrop;
    float2 cellUv = panel.uv;
    int k = panel.index;
    float2 p = panel.canvas.centred;
    float v = 0;
    colour c = colours.black;
    switch (k)
    {
        case 0: v = ValueNoise(30).amount(p); break;
        case 1: v = Perlin(30).amount(p); break;
        case 2: v = Simplex(30).amount(p); break;
        case 3: v = Worley(30).amount(p); break;
        case 4: v = Perlin(80).fractal(6).amount(p); break;
        case 5: v = Simplex(80).ridged(6).amount(p); break;
        case 6: v = Perlin(60).billowed(5).amount(p); break;
        case 7: v = Simplex(60).warped(Perlin(120).fractal(4), 60).amount(p); break;
        case 8: v = noise.warped(p / 120); break;
        case 9: { Cell cell = pattern.cells(p, 40); c = colours.hsv(random(cell.id), 0.6, 0.9); c.paint(sdf.stroked(cell.edge, 2), colours.black); return c; }
        case 10: { float2 f = noise.curl(p / 60); return colour(f * 0.5 + 0.5, 0.5); }
        case 11: v = noise.fbm(p / 60) ; break;
    }
    return colour(float3(v));
}
