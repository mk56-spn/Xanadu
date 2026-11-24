// A chord's line: a straight stroke in one colour, clear at its start and
// reaching full colour line_shape.x of the way along, held to its end, so notes
// come in out of nothing (0 for a line solid all along, as the judgement line
// is). Drawn on a quad the drawer stretches from the line's start (its left
// edge) to its end, as wide as the stroke.
import xanadu;

[Default(1, 1, 1, 1)] uniform colour line_tint;
// How far along it reaches full colour (0..1).
[Default(0.45, 0, 0, 0)] uniform float4 line_shape;

float4 shade(ItemInput i)
{
    float reach = i.canvas.size.x * line_shape.x;
    colour c = line_tint;
    if (reach > 0)
        c = Linear(reach).anchored(anchor.left).moved(float2(reach * 0.5, 0)).coloured(line_tint.withAlpha(0), line_tint);
    return c * i.colour;
}
