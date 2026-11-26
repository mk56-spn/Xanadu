// GPU text effects, the basics: HarfBuzz shapes the line on the CPU and every glyph becomes one quad of
// the item; this stack then moves and colours the glyphs on the GPU, each on its own. A glyph's quad at
// rest (v.rest) is the glyph's own box, so its middle is the glyph's place in the line.
// Open it with --text="SOME WORDS" to draw a line instead of the plain quad.
import xanadu;

[Default(14)] uniform float height;
[Default(0.25)] uniform float tilt;
[Default(3)] uniform float speed;

/// Each glyph bobs on a wave running along the line and leans with its slope; the whole glyph moves as one.
ItemVertex deform(ItemVertex v)
{
    float phase = v.rest.position.x * 0.018 - time * speed;
    float2 from = v.position - v.rest.position;
    v.position = v.rest.position + rotate(from, cos(phase) * tilt) + float2(0, sin(phase) * height);
    return v;
}

/// The glyph's coverage, tinted by a rainbow sweeping across the screen.
float4 shade(ItemInput i)
{
    colour c = sampleItem(i.uv);
    c.rgb *= colours.hue(saw(i.screenUv.x * 0.8 - time * 0.3)).rgb;
    return c;
}
