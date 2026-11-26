// Every note look on a playfield in one counted draw: key notes, dash bars, chord lines and their
// landing dots. The host hands the looks as a [Host] list each frame (Drawers' NoteCopies), each a quad
// in the item's own units from the run's rect (`area` is that rect's size in those units) with its
// kind, colour and shape; this draws one copy of a quad per look, in list order, later over earlier.
import xanadu;

static const uint LOOKS = 2048;

static const uint KEY = 0;
static const uint DASH = 1;
static const uint LINE = 2;
static const uint DOT = 3;

/// One look: its quad (two sides and the corner where uv is (0, 0)), what it is, item units per
/// unit of its own sizes, its colour, and its shape (a dash: bar size xy, diamond half diagonal z;
/// a line: how far along it reaches full colour, x).
struct Shown
{
    float2 xAxis;
    float2 yAxis;
    float2 corner;
    uint kind;
    float scale;
    float4 tint;
    float4 shape;
};

[Host] [Size(LOOKS)] List<Shown> looks;

[Default(0)] uniform int lookCount;
/// The run's rect, in the units the looks are in.
[Default(1, 1)] uniform float2 area;

[Draw(1, 1, shade: looked, copies: lookCount)]
ItemVertex placed(ItemVertex v)
{
    Shown l = looks[copy];
    float2 toTarget = v.rest.size / area;
    float2 p = l.corner + l.xAxis * v.uv.x + l.yAxis * v.uv.y;
    v.position = v.rest.corner + p * toTarget;
    v.local = v.uv;
    return v;
}

/// The look's quad under this pixel, in target pixels: uv's change across the screen, inverted.
Canvas quadOf(ItemInput i)
{
    float2 perX = ddx(i.local), perY = ddy(i.local);
    float det = perX.x * perY.y - perY.x * perX.y;
    if (abs(det) < 1e-12)
        return noCanvas(i.pixel);
    float2 xAxis = float2(perY.y, -perX.y) / det;
    float2 yAxis = float2(-perY.x, perX.x) / det;
    return canvasFrom(xAxis, yAxis, i.pixel - xAxis * i.local.x - yAxis * i.local.y, i.local);
}

float4 looked(ItemInput i)
{
    Shown l = looks[copy];
    Canvas q = quadOf(i);
    shading.canvas = q;
    // Target pixels per unit of the look's own sizes.
    float px = l.scale * q.size.x / max(length(l.xAxis), 1e-6);

    colour c = colours.transparent;
    switch (l.kind)
    {
        case KEY:
        {
            // Its edge: two units, never under a pixel.
            float edge = max(2 * px, 1);
            c.paint(Box(q.size), Look(l.tint, colours.white.withAlpha(l.tint.a * 0.8), edge));
            break;
        }
        case DASH:
        {
            // At least four pixels to its corners, so it stays a diamond when small.
            float corner = max(l.shape.z * px, 4);
            c.paint(Box(l.shape.xy * px), l.tint);
            c.paint(Rhombus(float2(corner * 2)).stroked(2), colours.white.withAlpha(l.tint.a));
            break;
        }
        case LINE:
        {
            float reach = q.size.x * l.shape.x;
            c = l.tint;
            if (reach > 0)
                c = Linear(reach).anchored(anchor.left).moved(float2(reach * 0.5, 0)).coloured(l.tint.withAlpha(0), l.tint);
            break;
        }
        default:
            c.paint(Circle(min(q.size.x, q.size.y) * 0.5).antialiased(), l.tint);
            break;
    }
    return c * i.colour;
}

void graph()
{
    placed(frame);
}
