// Stained glass, built in six steps from Voronoi cells (pattern.cells).
import xanadu;

[Default(70)] uniform float cellSize;

float4 shade(ItemInput i)
{
    Step panel = steps.split(6);
    if (panel.gap)
        return panel.backdrop;

    // 1. Scattered points, one per tile: how far the nearest is (Worley's F1). Dark on a point.
    Cell cell = pattern.cells(cellSize, 1, time * 0.6);
    if (panel.stopsAt(0))
        return panel.marked(colour(float3(cell.nearest / cellSize)));

    // 2. Each pixel belongs to its nearest point's cell: colour by the cell's id.
    colour glass = colours.oklch(0.62, 0.16, random(cell.id));
    if (panel.stopsAt(1))
        return panel.marked(glass);

    // 3. The walls: cell.edge is the exact distance to the border, so the lead is even.
    colour c = glass;
    c.paint(sdf.stroked(cell.edge, 5), colours.black.brightened(0.1));
    if (panel.stopsAt(2))
        return panel.marked(c);

    // 4. Thicker in the middle of each pane, thinner glass at the rim: brighter near the lead.
    c.rgb *= 0.75 + 0.5 * (1 - saturate(cell.edge / (cellSize * 0.4)));
    c.paint(sdf.stroked(cell.edge, 5), colours.black.brightened(0.1));
    if (panel.stopsAt(3))
        return panel.marked(c);

    // 5. Texture in the glass: fine noise, a little different in every pane.
    float grain = Perlin(12, random(cell.id) * 9).amount();
    c.rgb *= 0.85 + 0.3 * grain;
    c.paint(sdf.stroked(cell.edge, 5), colours.black.brightened(0.1));
    if (panel.stopsAt(4))
        return panel.marked(c);

    // 6. Light through it: a bright band sweeping across, added as light.
    float sweep = shaping.pulse(panel.uv.x + panel.uv.y * 0.3, frac(time * 0.2) * 1.6 - 0.2, 0.25);
    c.paint(sweep * (1 - sdf.stroked(cell.edge, 5)), glass.brightened(1.6), blend.add);
    return panel.marked(c);
}
