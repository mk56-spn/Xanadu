// A round dot filling its quad, in dot_tint: a chord line's landing mark.
import xanadu;

[Default(1, 1, 1, 1)] uniform colour dot_tint;

float4 shade(ItemInput i)
{
    colour c = colours.transparent;
    c.paint(Circle(min(i.canvas.size.x, i.canvas.size.y) * 0.5).antialiased(), dot_tint);

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}
