// The general purpose shape: one signed distance picked from a list, filled,
// outlined, with a blurred copy underneath (a drop shadow, or a glow with no
// offset and a bright colour). Draw it on a quad with room for the outline
// and the shadow. Sizes are in item units, made screen pixels with xif_unit.
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

[Default(1)] uniform float xif_unit;
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
[Default(0.35, 0.6, 1, 1)] uniform float4 sdf_fill;
// The outline's colour.
[Default(1, 1, 1, 1)] uniform float4 sdf_outline;
// The outline's width (item units, 0 for none) and where it sits: 0 inside the edge, 0.5 on it, 1 outside.
[Default(6, 0.5)] uniform float2 sdf_outline_width;
// The blurred copy's colour: dark for a shadow, bright for a glow.
[Default(0, 0, 0, 0.55)] uniform float4 sdf_shadow;
// The blurred copy's offset (item units, y down).
[Default(10, 14)] uniform float2 sdf_shadow_offset;
// How far the blurred copy blurs (item units) and how much it grows first.
[Default(18, 0)] uniform float2 sdf_shadow_blur;

/// The picked shape's distance at p, in item units, before growing.
float shapeDistance(float2 p)
{
    float2 size = max(sdf_size, float2(1e-3));
    float4 k = sdf_shape_params;
    float half = min(size.x, size.y) * 0.5;
    int shape = int(round(sdf_shape));
    switch (shape)
    {
    case 0: return Circle(half).local(p);
    case 1: return Box(size, clamp(k.x, 0.0, half)).local(p);
    case 2: return Ellipse(size * 0.5).local(p);
    case 3: return Triangle(half).local(p);
    case 4: return Polygon(max(3.0, round(k.x)), half).local(p);
    case 5: return Star(max(3.0, round(k.x)), half, saturate(k.y)).local(p);
    case 6: return Rhombus(size).local(p);
    case 7: return Cross(float2(half * 2.0, half * 2.0 * max(saturate(k.x), 0.05)), max(k.y, 0.0)).local(p);
    case 8: return Heart(min(size.x, size.y)).local(p);
    case 9: return Arc(half - max(k.x, 0.0) * 0.5, max(k.x, 0.0), saturate(k.y) * PI).local(p);
    case 10: return Moon(half, half * saturate(k.x), half * saturate(k.y) * 2.0).local(p);
    case 11: return Squircle(size, max(k.x, 2.0)).local(p);
    case 12: return Egg(half, saturate(k.x)).local(p);
    case 13: return Pie(half, saturate(k.x) * PI).local(p);
    case 14: return Trapezoid(size.x, size.x * saturate(k.x), size.y).local(p);
    case 15: return Vesica(half, half * saturate(k.x) * 0.95).local(p);
    default: return Circle(half).local(p);
    }
}

/// The shape's distance at p (item units from the quad's centre), turned and grown.
float distanceAt(float2 p)
{
    float a = radians(sdf_turn);
    float s = sin(a), c = cos(a);
    // Undo the clockwise turn (y down) to measure in the shape's own space.
    float2 q = float2(c * p.x + s * p.y, -s * p.x + c * p.y);
    return shapeDistance(q) - sdf_grow;
}

float4 shade(ItemInput i)
{
    float unit = max(xif_unit, 1e-4);
    float px = 1.0 / unit;
    float2 p = i.canvas.centred / unit;
    colour c = colours.transparent;

    // Underneath: the shape again, moved, grown and blurred.
    float blur = max(sdf_shadow_blur.x, px);
    float shadow = distanceAt(p - sdf_shadow_offset) - sdf_shadow_blur.y;
    c.paint(1.0 - smoothstep(-blur, blur, shadow), sdf_shadow);

    float d = distanceAt(p);
    c.paint(sdf.filled(d, px), sdf_fill);

    // The outline, slid from just inside the edge to just outside it.
    float width = sdf_outline_width.x;
    if (width > 0.0)
    {
        float centre = (saturate(sdf_outline_width.y) - 0.5) * width;
        c.paint(sdf.stroked(d - centre, width, px), sdf_outline);
    }

    // The vertex colour carries self modulate and fades.
    return float4(c.rgb * i.colour.rgb, c.a * i.colour.a);
}
