// Library tour: Every shape added in the 2026-11-21 library pass, drawn as its distance field
// (sdf.shown: blue inside, orange outside, a contour every few pixels), then the joins.
import xanadu;

float4 shade(ItemInput i, Canvas canvas)
{
    Step panel = steps.split(20, 2);
    if (panel.gap)
        return panel.backdrop;
    float2 cellUv = panel.uv;
    int k = panel.index;
    float2 p = panel.canvas.centred;
    float d = 1e5;
    switch (k)
    {
        case 0: d = Ellipse(float2(60, 30)).distance(p); break;
        case 1: d = Polygon(5, 45).distance(p); break;
        case 2: d = Polygon(6, 45).distance(p); break;
        case 3: d = Star(5, 50).distance(p); break;
        case 4: d = Star(8, 50, 0.3).distance(p); break;
        case 5: d = Rhombus(float2(100, 70)).distance(p); break;
        case 6: d = Trapezoid(90, 40, 60).distance(p); break;
        case 7: d = Spike(70, 80).distance(p); break;
        case 8: d = Pie(50, 2.2).distance(p); break;
        case 9: d = Heart(80).distance(p); break;
        case 10: d = Vesica(50, 30).distance(p); break;
        case 11: d = Saltire(80, 12).distance(p); break;
        case 12: d = TaperedLine(float2(-40, 20), float2(40, -20), 40, 8).distance(p); break;
        case 13: d = Arch(float2(-50, 30), float2(0, -70), float2(50, 30), 0).distance(p); break;
        case 14: { Outline h; h.add(float2(-40, 40)); h.add(float2(40, 40)); h.add(float2(40, -10)); h.add(float2(0, -50)); h.add(float2(-40, -10)); d = h.distance(p); break; }
        case 15: d = Circle(25).elongated(float2(50, 0)).distance(p); break;
        case 16: d = Box(float2(20), 3).tiled(32, float2(1, 1)).distance(p); break;
        case 17: d = sdf.stairs(Box(float2(70, 20)).distance(p), Box(float2(20, 70)).distance(p), 15, 3); break;
        case 18: d = Circle(35).xor(Box(float2(50)).moved(float2(20, 20))).distance(p); break;
        case 19: d = Box(float2(70, 16)).swirled(0.03).distance(p); break;
    }
    return sdf.shown(d, 30);
}
