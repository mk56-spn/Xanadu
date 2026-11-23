// An updraft: a column of air fading up from its emitter past the hover, wisps
// rising through it on the song's clock, a short bar where it holds the body
// (bobbing on the beat), and the emitter itself, a slab with vents. The drawer
// sets draft_state from the draft each frame (the song's seconds, where the
// hover is, how bright its bar is). The quad runs from the top of the column
// down to the slab's foot; the draft's place (the vent) is the middle of the
// slab. Sizes are in item units (playfield units), made screen pixels with xif_unit.
import xanadu;

[Default(1)] uniform float xif_unit;
// Half the emitter's width, how far the column reaches above the vent, the slab's height (item units).
[Default(180, 750, 24, 0)] uniform float4 draft_box;
// The draft's colour.
[Default(0.55, 0.88, 1, 1)] uniform float4 draft_tint;
// The song's seconds, the hover's height from the vent (item units, up is negative), the hover bar's alpha.
[Default(0, -600, 0.35, 0)] uniform float4 draft_state;

static const int Wisps = 7;

float4 shade(ItemInput i)
{
    float unit = max(xif_unit, 1e-4);
    float px = 1.0 / unit;
    float hw = draft_box.x, reach = draft_box.y, eh = draft_box.z;
    // From the vent: the quad's middle sits half way between the column's top and the slab's foot.
    float2 p = i.canvas.centred / unit + float2(0.0, (eh * 0.5 - reach) * 0.5);
    float3 tint = draft_tint.rgb;
    colour c = colours.transparent;

    // The column: air fading out on its way up, past the hover.
    if (abs(p.x) <= hw && p.y <= 0.0 && p.y >= -reach)
        c.paint(1.0, float4(tint, 0.16 * (1.0 + p.y / reach)));

    // Wisps rising through it, each on its own lane and phase, by song time.
    float line = max(1.5 * px, 3.0);
    for (int k = 0; k < Wisps; k++)
    {
        float f = frac(draft_state.x / 0.9 + k * 0.618);
        float x = -hw + hw * 2.0 * frac(k * 0.37 + 0.1);
        float y = -reach * f;
        float length = 30.0 + 40.0 * (1.0 - f);
        float d = Line(float2(x, y), float2(x, y - length)).local(p);
        c.paint(sdf.filled(d - line * 0.5, px), float4(tint, 0.5 * (1.0 - f)));
    }

    // The hover: a short bar where the body is held.
    float hover = Line(float2(-hw * 0.5, draft_state.y), float2(hw * 0.5, draft_state.y)).local(p);
    c.paint(sdf.filled(hover - line * 0.5, px), float4(tint, draft_state.z));

    // The emitter: a slab with vents, outlined.
    float slab = Box(float2(hw * 2.0, eh)).local(p);
    c.paint(sdf.filled(slab, px), float4(tint * float3(0.5, 0.5, 0.6), 1.0));
    float vent = max(1.0 * px, 2.0);
    float vents = 1e9;
    for (int k = 1; k < 6; k++)
    {
        float x = -hw + hw * 2.0 * k / 6.0;
        vents = min(vents, Line(float2(x, -eh * 0.5 + 3.0 * px), float2(x, eh * 0.5 - 3.0 * px)).local(p));
    }
    c.paint(sdf.filled(vents - vent * 0.5, px), float4(tint, 0.8));
    c.paint(sdf.stroked(slab + vent * 0.5, vent, px), float4(tint, 1.0));

    // The vertex colour carries self modulate and fades.
    return float4(c.rgb * i.colour.rgb, c.a * i.colour.a);
}
