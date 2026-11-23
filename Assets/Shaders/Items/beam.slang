// The player's beam into the judgement line: a soft wide line under a bright
// core that thin and fade as it ages, and a flash across its end where it
// strikes, widening. Drawn on a quad stretched from the beam's start (uv.x 0)
// to just past its end, beam_shape.y pixels either side (uv.y 0 to 1). Works
// in screen pixels: the drawer sets the beam's length, its age and its colour.
import xanadu;

// The beam's length and the quad's half width (pixels), how far through its life it is (0..1).
[Default(100, 40, 0, 0)] uniform float4 beam_shape;
// The beam's colour.
[Default(1, 1, 1, 1)] uniform float4 beam_tint;

float4 shade(ItemInput i)
{
    float length = beam_shape.x;
    float t = saturate(beam_shape.z);
    float fade = 1.0 - t;
    float along = i.uv.x * (length + 4.0);
    float across = abs(i.uv.y * 2.0 - 1.0) * beam_shape.y;
    float3 tint = beam_tint.rgb;
    colour c = colours.transparent;

    // The beam runs from its start to its end, square at both.
    float onBeam = step(0.0, along) * step(along, length);
    c.paint(onBeam * sdf.filled(across - 7.0 * (1.0 - t * 0.6)), float4(tint, 0.35 * fade));
    c.paint(onBeam * sdf.filled(across - 2.0 * (1.0 - t * 0.5)), float4(1.0, 1.0, 1.0, fade));

    // The flash where it strikes: a bar across its end, 6 pixels deep, widening as it fades.
    float flash = Box(float2(6.0, (10.0 + 26.0 * t) * 2.0)).local(float2(along - length, across));
    c.paint(sdf.filled(flash), float4(tint, fade));

    // The vertex colour carries self modulate and fades.
    return float4(c.rgb * i.colour.rgb, c.a * i.colour.a);
}
