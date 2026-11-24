// A key note: a flat rectangle in its key's colour with a bright edge, filling
// its quad. The drawer sets note_tint (the key's colour, faded and greyed as
// the note is). Sizes are item units, made screen pixels with xif_unit.
import xanadu;

// Screen pixels per item unit; the drawer sets it.
[Default(1)] uniform float xif_unit;
// The note's colour, alpha included.
[Default(1, 1, 1, 1)] uniform colour note_tint;

float4 shade(ItemInput i)
{
    float unit = max(xif_unit, 1e-4);
    // Its edge: two item units, never under a pixel.
    float edge = max(2 * unit, 1);

    colour c = colours.transparent;
    c.paint(Box(i.canvas.size), Look(note_tint, colours.white.withAlpha(note_tint.a * 0.8), edge));

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}
