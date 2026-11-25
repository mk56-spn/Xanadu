// The general purpose shape: one shape picked from a list, filled, outlined,
// with a blurred copy underneath (a drop shadow, or a glow with no offset and a
// bright colour). Draw it on a quad with room for the outline and the shadow.
// Sizes are item units, made screen pixels with `unit`.
//
// sdf_shape picks the shape (by name in the editor); sdf_shape_params mean different things per shape
// (unused ones are ignored). Width and height come from sdf_size.
//   0 circle      x: -
//   1 box         x: corner radius
//   2 ellipse     x: -
//   3 triangle    x: -
//   4 polygon     x: sides
//   5 star        x: points   y: sharpness (0..1)
//   6 rhombus     x: -
//   7 cross       x: arm thickness (0..1 of its span)   y: corner radius
//   8 heart       x: -
//   9 ring        x: thickness   y: gap (0..1, open at the bottom)
//  10 moon        x: bite (0..1)   y: bite offset (0..1)
//  11 squircle    x: power (2 = ellipse, higher = squarer)
//  12 egg         x: tip (0..1)
//  13 pie         x: opening (0..1 of a turn)
//  14 trapezoid   x: top width (0..1 of the bottom)
//  15 vesica      x: thinness (0..1)
import xanadu;

// Which shape: picked by name in the editor.
[Options("circle|box|ellipse|triangle|polygon|star|rhombus|cross|heart|ring|moon|squircle|egg|pie|trapezoid|vesica")]
[Default(1)] uniform float sdf_shape;
// Width and height (item units).
[Default(160, 110)] uniform float2 sdf_size;
// The shape's own numbers (see the list at the top).
[Default(24, 0.6, 0, 0)] uniform float4 sdf_shape_params;
// Turned by this many degrees, clockwise.
[Default(0)] uniform float sdf_turn;
// Grows (above 0) or shrinks the shape by this much (item units); rounds sharp corners as it grows.
[Default(0)] uniform float sdf_grow;
// The fill's colour; alpha 0 for an outline only.
[Default(0.35, 0.6, 1, 1)] uniform colour sdf_fill;
[Default(1, 1, 1, 1)] uniform colour sdf_outline;
// The outline's width (item units, 0 for none) and where it sits: 0 inside the edge, 0.5 on it, 1 outside.
[Default(6, 0.5)] uniform float2 sdf_outline_width;
// The blurred copy's colour: dark for a shadow, bright for a glow.
[Default(0, 0, 0, 0.55)] uniform colour sdf_shadow;
// The blurred copy's offset (item units, y down).
[Default(10, 14)] uniform float2 sdf_shadow_offset;
// How far the blurred copy blurs (item units) and how much it grows first.
[Default(18, 0)] uniform float2 sdf_shadow_blur;

/// The picked shape, turned and grown, measured at `at` (pixels from the quad's middle).
float distanceAt(float2 at)
{
    float2 size = max(sdf_size * unit, float2(1e-3));
    float4 k = sdf_shape_params;
    float half = min(size.x, size.y) * 0.5;
    float turn = radians(sdf_turn);
    float grow = sdf_grow * unit;
    switch (int(round(sdf_shape)))
    {
    case 1: return Box(size, clamp(k.x * unit, 0, half)).rotated(turn).grown(grow).distance(at);
    case 2: return Ellipse(size * 0.5).rotated(turn).grown(grow).distance(at);
    case 3: return Triangle(half).rotated(turn).grown(grow).distance(at);
    case 4: return Polygon(max(3, round(k.x)), half).rotated(turn).grown(grow).distance(at);
    case 5: return Star(max(3, round(k.x)), half, saturate(k.y)).rotated(turn).grown(grow).distance(at);
    case 6: return Rhombus(size).rotated(turn).grown(grow).distance(at);
    case 7: return Cross(float2(half * 2, half * 2 * max(saturate(k.x), 0.05)), max(k.y * unit, 0)).rotated(turn).grown(grow).distance(at);
    case 8: return Heart(min(size.x, size.y)).rotated(turn).grown(grow).distance(at);
    case 9: return Arc(half - max(k.x * unit, 0) * 0.5, max(k.x * unit, 0), saturate(k.y) * PI).rotated(turn).grown(grow).distance(at);
    case 10: return Moon(half, half * saturate(k.x), half * saturate(k.y) * 2).rotated(turn).grown(grow).distance(at);
    case 11: return Squircle(size, max(k.x, 2)).rotated(turn).grown(grow).distance(at);
    case 12: return Egg(half, saturate(k.x)).rotated(turn).grown(grow).distance(at);
    case 13: return Pie(half, saturate(k.x) * PI).rotated(turn).grown(grow).distance(at);
    case 14: return Trapezoid(size.x, size.x * saturate(k.x), size.y).rotated(turn).grown(grow).distance(at);
    case 15: return Vesica(half, half * saturate(k.x) * 0.95).rotated(turn).grown(grow).distance(at);
    default: return Circle(half).rotated(turn).grown(grow).distance(at);
    }
}

float4 shade(ItemInput i)
{
    float2 at = i.canvas.centred;
    colour c = colours.transparent;

    // Underneath: the shape again, moved, grown and blurred.
    float shadow = distanceAt(at - sdf_shadow_offset * unit) - sdf_shadow_blur.y * unit;
    c.paint(sdf.filled(shadow, max(sdf_shadow_blur.x * unit, 1) * 2), sdf_shadow);

    float d = distanceAt(at);
    c.paint(sdf.filled(d), sdf_fill);

    // The outline, slid from just inside the edge to just outside it.
    float width = sdf_outline_width.x * unit;
    if (width > 0)
        c.paint(sdf.stroked(d - (saturate(sdf_outline_width.y) - 0.5) * width, width), sdf_outline);

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}
