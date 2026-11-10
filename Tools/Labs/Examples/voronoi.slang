// Voronoi: each cell owns a wandering point; a pixel takes the nearest one's
// colour, and the gap to the second nearest draws the borders.
import xanadu;

[Default(110)] uniform float cellSize;
[Default(0.5)] uniform float speed;

float4 shade(ItemInput i)
{
    float2 q = i.canvas.centred / cellSize;
    float2 cell = floor(q), inside = frac(q);

    float nearest = 8, second = 8;
    float2 nearestCell = cell;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 n = float2(x, y);
            float2 point = 0.5 + 0.4 * sin(time * speed + TAU * random2(cell + n));
            float d = length(n + point - inside);
            if (d < nearest)
            {
                second = nearest;
                nearest = d;
                nearestCell = cell + n;
            }
            else if (d < second)
                second = d;
        }
    }

    colour fill = colours.hsv(random(nearestCell), 0.45, 0.9);
    // The border: where the two nearest are about as near (in pixels, so it stays a fixed width).
    float edge = (second - nearest) * cellSize;
    float border = smoothstep(2, 0.5, edge);
    float dot = smoothstep(5, 3.5, nearest * cellSize);
    colour c = lerp(fill * (0.6 + 0.4 * nearest), colours.midnightBlue, border);
    return lerp(c, colours.white, dot);
}
