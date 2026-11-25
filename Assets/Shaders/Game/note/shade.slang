// A key note: a flat rectangle in its key's colour with a bright edge, filling
// its quad. The drawer sets note_tint (the key's colour, faded and greyed as
// the note is). Sizes are item units, made screen pixels with `unit`.
import xanadu;

// The note's colour, alpha included.
[Default(1, 1, 1, 1)] uniform colour note_tint;

float4 shade(ItemInput i)
{
    // Its edge: two item units, never under a pixel.
    float edge = i.pixels(2, 1);

    colour c = colours.transparent;
    c.paint(Box(i.canvas.size), Look(note_tint, colours.white.withAlpha(note_tint.a * 0.8), edge));

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}
