// Library tour: The tilings and pattern shapes (hex, Truchet, dots, waves, zigzag, bricks,
// triangles), then the maths: shaping curves, a Julia set (complex.), a log-polar
// warp and polar coordinates.
import xanadu;

float4 shade(ItemInput i, Canvas canvas)
{
    Step panel = steps.split(12, 2);
    if (panel.gap)
        return panel.backdrop;
    float2 cellUv = panel.uv;
    int k = panel.index;
    float2 p = panel.canvas.centred;
    colour c = colours.midnightBlue;
    switch (k)
    {
        case 0: { Tile t = pattern.hex(p, 40); c = colours.viridis(random(t.id)); c.paint(Hexagons(40, 2).filled(p), colours.black); break; }
        case 1: c.paint(Truchet(30, 4).filled(p), colours.gold); break;
        case 2: c.paint(Dots(16, 5, true).filled(p), colours.white); break;
        case 3: c.paint(Waves(80, 10, 3, 24).filled(p), colours.cyan); break;
        case 4: c.paint(Zigzag(40, 8, 3, 24).filled(p), colours.hotPink); break;
        case 5: { Tile t = pattern.bricks(p, float2(50, 20)); c = colours.firebrick.brightened(0.7 + 0.5 * random(t.id)); c.paint(Bricks(float2(50, 20), 3).filled(p), colours.lightGray); break; }
        case 6: { Tile t = pattern.triangles(p, 40); c = colours.turbo(random(t.id)); c.paint(sdf.stroked(length(t.local) - 4, 2), colours.white); break; }
        case 7: { float x = cellUv.x; float y = 1 - cellUv.y; float d = 1e5;
                  d = min(d, abs(y - shaping.impulse(x, 8)));
                  d = min(d, abs(y - shaping.pulse(x, 0.5, 0.2)));
                  d = min(d, abs(y - shaping.gain(x, 0.8)));
                  d = min(d, abs(y - shaping.parabola(x, 2)));
                  d = min(d, abs(y - shaping.smoothFloor(x * 4) / 4));
                  c.paint(1 - aastep(0.006, d), colours.white); break; }
        case 8: { float2 z = p / 70, cc = float2(-0.8, 0.156); int n = 0;
                  for (; n < 80 && dot(z, z) < 4; n++) z = complex.square(z) + cc;
                  c = colours.magma(float(n) / 80); break; }
        case 9: { float2 w = complex.log(p / 20); w.x -= time * 0.5; c.paint(Checker(0.5).filled(w * float2(1, 6 / TAU) * 2, 0.02), colours.white); break; }
        case 10: { float2 q = mul(rotationMatrix(0.3), p); float2 pol = polar(q); c = colours.hsv(wrap(pol.y / TAU, 0, 1), 0.7, linearstep(0, 80, pol.x)); c.paint(Circle(4).filled(fromPolar(pol) - q), colours.white); break; }
        case 11: { float v = damped(0, 1, 2, 0.5) + unlerp(0, 10, 5) + snapped(13, 5) + shaping.bias(0.5, 0.3) + shaping.pcurve(0.3, 2, 1) + shaping.expStep(0.5, 3) + shaping.almostIdentity(0.1, 0.2, 0.05) + shaping.sinc(0.5, 2) + complex.mul(p, p).x + complex.div(p, p).x + complex.inverse(p).x + complex.conjugate(p).x + complex.exp(p).x + complex.pow(p, 2.5).x;
                  c = colour(float3(frac(v * 0.001))); break; }
    }
    return c;
}
