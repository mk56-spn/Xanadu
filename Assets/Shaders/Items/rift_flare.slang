// The rift's release flare: a white column up and down the field in a blue
// spread, thinning fast. Drawn on a quad the drawer stretches over the column:
// the spread fills it, fading to nothing at its sides; the white core is its
// middle sixth. rift_flare.x is how bright it still is (1 to 0).
import xanadu;

[Default(1, 0, 0, 0)] uniform float4 rift_flare;

static const colour RiftBlue = colour(0.4, 0.65, 1);

float4 shade(ItemInput i)
{
    float bright = saturate(rift_flare.x);
    float2 size = i.canvas.size;

    colour c = colours.transparent;
    // The spread: strongest down the middle, gone at the sides.
    c.paint(Reflected(size.x), RiftBlue.withAlpha(0.5 * bright), RiftBlue.withAlpha(0));
    // The core, as tall as the quad and then some, so its ends never show.
    c.paint(Box(float2(size.x / 6, size.y * 2)), colours.white.withAlpha(0.9 * bright));

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}
