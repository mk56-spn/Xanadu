// A very basic mesh shader: no vertex buffer and no grid, each copy one workgroup that makes its own
// triangles. Thread k places corner k (0 the middle, then the star's points and dips round it) and joins
// triangle k (the middle and two neighbours on the rim). The device layer runs it as a real mesh stage
// (HostCore/Native/device_layer.c); SDL_GPU has none.
import xanadu;

static const uint COPIES = 5;

/// Turns a second, in radians.
[Default(0.8)] uniform float spin;
/// How deep the star's dips go: 0 a star of lines, 1 a disc.
[Default(0.45)] uniform float dips;

[MeshDraw(13, 12, shade: look, copies: COPIES)]
MeshPart star(MeshPart m)
{
    uint k = m.thread;
    float2 size = m.corner.rest.size;
    // A row of stars across the item, every other one turning the other way.
    float2 middle = float2((copy + 0.5) / COPIES, 0.5);
    float reach = 0.45 * min(size.x / COPIES, size.y) * (k % 2 == 1 ? 1.0 : dips);
    float turn = time * spin * (copy % 2 == 0 ? 1.0 : -1.0) + float(k) * 3.14159265 / 6.0;
    m.at(k == 0 ? middle : middle + reach * float2(cos(turn), sin(turn)) / size);
    float hue = float(copy) / COPIES + float(k) / 40.0;
    m.corner.colour = k == 0 ? float4(1) : float4(colours.hue(hue).rgb, 1);
    m.triangle = uint3(0, 1 + k, 1 + (k + 1) % 12);
    return m;
}

float4 look(ItemInput i) { return i.colour; }

void graph() { star(frame); }
