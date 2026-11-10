// Tunnel: polar coordinates turned into a texture lookup, angle round the
// wall and 1 / radius into the distance, with a checker scrolling along it.
import xanadu;

[Default(0.4)] uniform float speed;
[Default(0.15)] uniform float twist;

float4 shade(ItemInput i)
{
    float2 p = i.canvas.centred / (i.canvas.size.y * 0.5);
    float radius = length(p);
    float angle = atan2(p.y, p.x) / TAU + 0.5;

    // Round the wall and down the tunnel, in checker squares.
    float2 wall = float2(angle * 16 + time * twist * 16, 2 / radius + time * speed * 4);
    float2 cell = floor(wall);
    float check = mod(cell.x + cell.y, 2);
    // Squares soften towards the centre, where they get smaller than a pixel.
    float2 f = frac(wall) - 0.5;
    // The angle jumps from 1 back to 0 on the left, and fwidth with it: take the
    // width from a copy that wraps somewhere else, whichever is smaller.
    float2 width = fwidth(wall);
    width.x = min(width.x, fwidth(frac(angle + 0.5) * 16));
    float2 edges = smoothstep(0.5, 0.5 - width, abs(f));
    float lines = 1 - min(edges.x, edges.y);

    colour wallColour = lerp(colours.darkSlateBlue, colours.orchid, check);
    wallColour.rgb = lerp(wallColour.rgb, colours.white.rgb, lines * 0.6);
    // Fog: the far end (small radius) fades out.
    float fog = smoothstep(0.0, 0.6, radius);
    return colour(lerp(colours.black.rgb, wallColour.rgb, fog), 1);
}
