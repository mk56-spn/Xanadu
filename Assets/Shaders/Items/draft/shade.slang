// An updraft: a column of air fading up from its emitter past the hover, wisps
// rising through it on the song's clock, a short bar where it holds the body
// (bobbing on the beat), and the emitter itself, a slab with vents. The drawer
// sets draft_state from the draft each frame (the song's seconds, where the
// hover is, how bright its bar is). The quad runs from the top of the column
// down to the slab's foot; the draft's place (the vent) is the middle of the
// slab. Sizes are item units, made screen pixels with xif_unit.
import xanadu;

// Screen pixels per item unit; the drawer sets it.
[Default(1)] uniform float xif_unit;
// Half the emitter's width, how far the column reaches above the vent, the slab's height (item units).
[Default(180, 750, 24, 0)] uniform float4 draft_box;
[Default(0.55, 0.88, 1, 1)] uniform colour draft_tint;
// The song's seconds, the hover's height from the vent (item units, up is negative), the hover bar's alpha.
[Default(0, -600, 0.35, 0)] uniform float4 draft_state;

static const int Wisps = 7;

float4 shade(ItemInput i)
{
    float unit = max(xif_unit, 1e-4);
    float halfWidth = draft_box.x * unit, reach = draft_box.y * unit, slabHeight = draft_box.z * unit;
    // The vent, from the quad's middle: half way between the column's top and the slab's foot.
    float2 vent = float2(0, (reach - slabHeight * 0.5) * 0.5);
    float line = max(3 * unit, 1.5);

    colour c = colours.transparent;

    // The column: air fading out on its way up, past the hover.
    float2 column = vent - float2(0, reach * 0.5);
    float foot = Linear(reach).rotated(PI / 2).moved(column).amount();
    c.paint(Box(float2(halfWidth * 2, reach)).moved(column).filled(), draft_tint.withAlpha(0.16 * foot));

    // Wisps rising through it, each on its own lane and phase, by song time.
    for (int k = 0; k < Wisps; k++)
    {
        float risen = saw(draft_state.x / 0.9 + k * 0.618);
        float2 start = vent + float2(halfWidth * (2 * saw(k * 0.37 + 0.1) - 1), -reach * risen);
        float length = (30 + 40 * (1 - risen)) * unit;
        c.paint(Line(start, start - float2(0, length), line), draft_tint.withAlpha(0.5 * (1 - risen)));
    }

    // The hover: a short bar where the body is held.
    float2 hover = vent + float2(0, draft_state.y * unit);
    c.paint(Line(hover - float2(halfWidth * 0.5, 0), hover + float2(halfWidth * 0.5, 0), line), draft_tint.withAlpha(draft_state.z));

    // The emitter: a slab, outlined, with five vents across it.
    float edge = max(2 * unit, 1);
    c.paint(Box(float2(halfWidth * 2, slabHeight)).moved(vent), Look(colour(draft_tint.rgb * float3(0.5, 0.5, 0.6)), draft_tint.withAlpha(1), edge));
    float ventHalf = slabHeight * 0.5 - 3;
    c.paint(Line(float2(0, -ventHalf), float2(0, ventHalf), edge).tiled(float2(halfWidth / 3, 1e5), float2(2, 0)).moved(vent),
        draft_tint.withAlpha(0.8));

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}
