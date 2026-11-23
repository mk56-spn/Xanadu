// A chord's line: a straight stroke in one colour, clear at its start and
// reaching full colour line_shape.x of the way along, held to its end, so notes
// come in out of nothing (0 for a line solid all along, as the judgement line
// is). Drawn on a quad the drawer stretches from the line's start (uv.x 0) to
// its end (uv.x 1), as wide as the stroke.
import xanadu;

[Default(1, 1, 1, 1)] uniform float4 line_tint;
// How far along it reaches full colour (0..1).
[Default(0.45, 0, 0, 0)] uniform float4 line_shape;

float4 shade(ItemInput i)
{
    float fade = line_shape.x > 0.0 ? saturate(i.uv.x / line_shape.x) : 1.0;
    return float4(line_tint.rgb * i.colour.rgb, line_tint.a * fade * i.colour.a);
}
