// A key note: a flat rectangle in its key's colour with a bright edge, filling
// its quad. The drawer sets note_tint (the key's colour, faded and greyed as
// the note is). Sizes are in item units (playfield units), made screen pixels
// with xif_unit.
import xanadu;

[Default(1)] uniform float xif_unit;
// The note's colour, alpha included.
[Default(1, 1, 1, 1)] uniform float4 note_tint;

float4 shade(ItemInput i)
{
    float unit = max(xif_unit, 1e-4);
    float px = 1.0 / unit;
    float2 p = i.canvas.centred / unit;
    float2 size = i.canvas.size / unit;
    float d = Box(size).local(p);
    float edge = max(1.0 * px, 2.0);
    colour c = colours.transparent;
    c.paint(sdf.filled(d, px), note_tint);
    c.paint(sdf.stroked(d + edge * 0.5, edge, px), float4(1.0, 1.0, 1.0, note_tint.a * 0.8));
    return float4(c.rgb * i.colour.rgb, c.a * i.colour.a);
}
