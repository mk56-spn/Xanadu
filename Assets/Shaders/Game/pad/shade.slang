// A pad: a low glowing slab with three chevrons pointing up. When it fires
// (pad_state.x, the spring: 1 as it fires, down to 0) it flashes, the chevrons
// spring up off it and a column of light shoots up. A timed pad shows a ring
// closing onto it over its approach (pad_state.y: 1 far, 0 at its time; below 0
// none), and in the editor a ring marking it as timed (pad_state.z: the ring's
// alpha; 0 none). The drawer sets pad_state from the pad each frame. Drawn on a
// quad with room above for the column and round it for the rings. Sizes are
// item units, made screen pixels with `unit`.
import xanadu;

// The slab's half width and half height (item units).
[Default(75, 14)] uniform float2 pad_half;
[Default(1, 0.35, 0.75, 1)] uniform colour pad_tint;
// The spring, the approach and the editor's ring, as above.
[Default(0, -1, 0, 0)] uniform float4 pad_state;

float4 shade(ItemInput i)
{
    float2 half = pad_half * unit;
    float spring = saturate(pad_state.x);

    colour c = colours.transparent;

    // Glow under it, brighter as it fires.
    c.paint(Radial(float2(half.x * 1.4, half.y * 3)), pad_tint.withAlpha(0.25 + 0.4 * spring), pad_tint.withAlpha(0));

    // The column of light it launches up off its top, fading towards its own top.
    if (spring > 0)
    {
        float height = half.x * 4 * (1 - spring * spring);
        float2 middle = float2(0, -half.y - height * 0.5);
        // 0 at the column's top, 1 at its foot on the slab.
        float foot = Linear(height).rotated(PI / 2).moved(middle).amount();
        c.paint(Box(float2(half.x, height)).moved(middle).filled(), pad_tint.withAlpha(0.6 * spring * foot));
    }

    // The slab with its bright border, and a white flash over it as it fires.
    let slab = Box(half * 2).measured();
    c.paint(slab, Look(pad_tint.withAlpha(0.9), colours.white.withAlpha(0.7 + 0.3 * spring), i.pixels(2, 1)));
    c.paint(slab, colours.white.withAlpha(0.7 * spring));

    // Three chevrons over it, springing up as it fires.
    float lift = half.y * (1.2 + 3 * spring);
    float2 wing = float2(half.x * 0.22, half.y * 0.9);
    for (int k = 0; k < 3; k++)
    {
        float2 tip = float2((k - 1) * half.x * 0.55, -half.y - lift * (0.6 + 0.25 * k));
        Polyline chevron = Polyline(i.pixels(3, 2)).added(tip + float2(-wing.x, wing.y)).added(tip).added(tip + wing);
        c.paint(chevron, colours.white.withAlpha(0.55 + 0.45 * spring));
    }

    // The editor's ring marking a timed pad.
    if (pad_state.z > 0)
        c.paint(Circle(half.x * 1.3).stroked(2), pad_tint.withAlpha(pad_state.z));

    // The approach: a ring closing onto it, thickening and brightening as its time comes.
    float t = pad_state.y;
    if (t > 0 && t <= 1)
        c.paint(Circle(half.x * (1.1 + 2.2 * t)).stroked(2 + 3 * (1 - t)), colours.white.withAlpha(0.9 * (1 - t)));

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}
