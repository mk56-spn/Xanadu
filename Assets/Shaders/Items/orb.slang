// An orb: a bright core with a white centre in a soft glow, brighter while it is
// ridden, and while it waits for its head a ring closing onto it. The drawer
// sets orb_tint (the orb's colour: grey once missed) and orb_state from the orb
// each frame. Drawn on a quad with room for the glow and the ring. Sizes are
// item units, made screen pixels with xif_unit.
import xanadu;

// Screen pixels per item unit; the drawer sets it.
[Default(1)] uniform float xif_unit;
// The core's radius (item units).
[Default(22)] uniform float orb_radius;
[Default(1, 0.82, 0.35, 1)] uniform colour orb_tint;
// How shown it is (0..1), lit while ridden (0 or 1), its approach (1 far, 0 at its head; below 0 none).
[Default(1, 0, -1, 0)] uniform float4 orb_state;

float4 shade(ItemInput i)
{
    float unit = max(xif_unit, 1e-4);
    float radius = orb_radius * unit;
    float shown = orb_state.x;
    float lit = orb_state.y;

    colour c = colours.transparent;
    c.paint(Radial(radius * 2.6), orb_tint.withAlpha(lerp(0.3, 0.55, lit) * shown), orb_tint.withAlpha(0));
    c.paint(Circle(radius), orb_tint.withAlpha(shown));
    c.paint(Circle(radius * 0.5), colours.white.withAlpha(lerp(0.6, 0.95, lit) * shown));

    // The ring closing onto it before its head, 3 pixels wide.
    float left = orb_state.z;
    if (left >= 0 && left <= 1)
        c.paint(Circle(radius * (1.2 + 3 * left)).stroked(3), orb_tint.withAlpha(shown * (1 - left)));

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}
