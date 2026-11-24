// The player's beam into the judgement line: a soft wide line under a bright
// core that thin and fade as it ages, and a flash across its end where it
// strikes, widening. Drawn on a quad stretched from the beam's start (its left
// edge) to just past its end, beam_shape.y pixels either side. Works in screen
// pixels: the drawer sets the beam's length, its age and its colour.
import xanadu;

// The beam's length and the quad's half width (pixels), how far through its life it is (0..1).
[Default(100, 40, 0, 0)] uniform float4 beam_shape;
[Default(1, 1, 1, 1)] uniform colour beam_tint;

float4 shade(ItemInput i)
{
    float length = beam_shape.x;
    float t = saturate(beam_shape.z);
    float fade = 1 - t;

    // From the quad's left edge along the beam; its end, where it strikes.
    float2 middle = float2(length * 0.5, 0);
    float2 end = float2(length, 0);

    colour c = colours.transparent;
    c.paint(Box(float2(length, 14 * (1 - t * 0.6))).anchored(anchor.left).moved(middle), beam_tint.withAlpha(0.35 * fade));
    c.paint(Box(float2(length, 4 * (1 - t * 0.5))).anchored(anchor.left).moved(middle), colours.white.withAlpha(fade));
    // The flash where it strikes: a bar across its end, 6 pixels deep, widening as it fades.
    c.paint(Box(float2(6, (10 + 26 * t) * 2)).anchored(anchor.left).moved(end), beam_tint.withAlpha(fade));

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}
