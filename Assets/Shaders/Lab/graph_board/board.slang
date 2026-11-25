// The GRAPH board's wires: the shader tool's render-graph board draws its backing, then this,
// then its boxes and arrowheads. The host hands it the board's wires, cut into straight pieces,
// and the boxes to glow under, as [Host] buffers in pixels of the board's area from its top-left
// corner (`area` is the area's size in those pixels), already panned and zoomed. Each piece is
// one copy of a quad drawn onto the frame: a line with a faint glow, carrying pulses from writer
// to reader (both ways for a read-write). Each hot box is a copy of another quad, glowing round it.
import xanadu;

static const uint WIRES = 512;
static const uint HOT = 64;

/// One straight piece of a wire, a to b, and how far along its wire a is.
struct Wire
{
    float2 a;
    float2 b;
    float along;    // the wire's length up to a
    float total;    // the whole wire's length
    float both;     // 1 when it points both ways
    float spare;
};

/// A box glowing under the board: its corners and its glow's colour, alpha how strong.
struct Hot
{
    float2 low;
    float2 high;
    float4 tone;
};

[Host] [Size(WIRES)] List<Wire> wires;
[Host] [Size(HOT)] List<Hot> hot;

[Default(0)] uniform int wireCount;
[Default(0)] uniform int hotCount;
/// The board's area, in the pixels the buffers are in.
[Default(1, 1)] uniform float2 area;
/// The board's zoom: lines, pulses and glows scale with the boxes.
[Default(1)] uniform float zoom;
[Default(0.5, 0.55, 0.6, 1)] uniform colour wireTone;
[Default(0.4, 0.7, 1, 1)] uniform colour pulse;

/// Pixels a second a pulse travels, and the room between two pulses, at zoom 1.
static const float SPEED = 140;
static const float APART = 110;

/// How far a piece's quad reaches past its line, and a hot box's past the box, at zoom 1.
static const float WIRE_REACH = 14;
static const float HOT_REACH = 40;

/// A point in the buffers' pixels, on the target.
float2 onTarget(ItemVertex v, float2 p) { return v.rest.corner + p * v.rest.size / area; }

/// A pulse's light at distance `s` along its wire: a bright head at the front, a tail behind.
float pulses(float s)
{
    return pow(frac((s - time * SPEED * zoom) / (APART * zoom)), 8);
}

void graph()
{
    hotDrawn(frame);
    wire(frame);
}
