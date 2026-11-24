// Tints white text along a line in the item's own space, from color_from at
// point_from to color_to at point_to, clamped beyond either end. The text's own
// colour still supplies the alpha, so fades keep working.
import xanadu;

[Default(1, 1, 1, 1)] uniform float4 color_from;
[Default(1, 1, 1, 1)] uniform float4 color_to;
uniform float2 point_from;
[Default(1, 0)] uniform float2 point_to;

float4 shade(ItemInput i)
{
    float2 line = point_to - point_from;
    float t = saturate(dot(i.local - point_from, line) / dot(line, line));
    float4 tint = lerp(color_from, color_to, t);
    return float4(tint.rgb * i.colour.rgb, tint.a * i.colour.a);
}
