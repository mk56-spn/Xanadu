// The GRAPH board's wires: the shader tool's render-graph board draws its backing, then this,
// then its boxes. The host hands it the board's wires, cut into straight pieces, as a [Host] buffer
// in pixels of the board's area from its top-left corner (`area` is the area's size in those
// pixels), already panned and zoomed. Each piece is one copy of a quad drawn onto the frame: a line
// with a faint glow, fading from its writer's colour to its reader's, carrying dots from writer to
// reader (both ways for a read-write).
import xanadu;

static const uint WIRES = 512;

/// One straight piece of a wire, a to b, and how far along its wire a is.
struct Wire
{
    float2 a;
    float2 b;
    float along;    // the wire's length up to a
    float total;    // the whole wire's length
    float both;     // 1 when it points both ways
    float spare;
    float4 from;    // the colour at the wire's start
    float4 to;      // and at its end
};

[Host] [Size(WIRES)] List<Wire> wires;

[Default(0)] uniform int wireCount;
/// The board's area, in the pixels the buffers are in.
[Default(1, 1)] uniform float2 area;
/// The board's zoom: lines and dots scale with the boxes.
[Default(1)] uniform float zoom;

/// Pixels a second a dot travels, and the room between two dots, at zoom 1.
static const float SPEED = 140;
static const float APART = 110;

/// How far a piece's quad reaches past its line, at zoom 1.
static const float WIRE_REACH = 14;

/// A point in the buffers' pixels, on the target.
float2 onTarget(ItemVertex v, float2 p) { return v.rest.corner + p * v.rest.size / area; }

/// A dot's radius, and how far its faint glow reaches past it, at zoom 1.
static const float DOT = 4;
static const float DOT_GLOW = 2;

/// Where along its wire the dot nearest distance `s` is, for dots moving towards larger `s`.
float nearestDot(float s)
{
    float apart = APART * zoom;
    float phase = (s - time * SPEED * zoom) / apart;
    return s - (frac(phase + 0.5) - 0.5) * apart;
}

void graph()
{
    wire(frame);
}
