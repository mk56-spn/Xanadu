// A bar of light up and down the field through a note just hit, so the hit
// lands with some weight: the note's colour, soft at its sides, a white core
// at first. It narrows and fades fast: a beat struck, not a light left on.
// The game draws one quad per hit, a column centred on the note as wide as
// the bar starts, and hands it the note's place in the chart. Everything else
// comes from the chart (`notes`): when it was hit, its colour.
import xanadu;

// The note this bar is for: its index in the chart.
uniform float hit_note;

float4 shade(Canvas canvas)
{
    Note note = notes[int(hit_note + 0.5)];
    Clip life = clock.since(note).clip(0, 0.26);
    Clip narrowing = clock.since(note).clip(0, 0.26).eased(eases.outQuad);
    if (!note.hit || life.finished)
        return colours.transparent;

    float fade = 1 - life.progress;
    fade *= fade;
    float half = canvas.size.x / 2 * (1 - 0.7 * narrowing.progress);
    float core = max(1.0, half * 0.18);

    colour c = colours.transparent;
    // The glow: full down the middle, nothing at its sides.
    c.paint(Box(float2(half, canvas.size.y)).filled(half), note.colour.withAlpha(note.colour.a * 0.55 * fade));
    c.paint(Box(float2(core, canvas.size.y)), colours.white.withAlpha(0.8 * fade));
    return c;
}
