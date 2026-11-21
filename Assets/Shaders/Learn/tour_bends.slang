// Library tour: the 2D corner moves for deform (xanadu.mesh), one per piece of the grid:
// wave, twist, bulge, squash, sway, shake, jitter, tilt, page curl. Each piece is moved
// to its own place first, then bent as if it were the whole quad.
import xanadu;

static const int2 COUNT = int2(3, 3);

ItemVertex deform(ItemVertex v)
{
    Piece piece = mesh.split(v.uv, COUNT);
    // Make this piece's corner look like a corner of a whole quad of the piece's size.
    float2 size = v.rest.size / float2(COUNT) * 0.8;
    float2 middle = v.rest.position + (float2(piece.at) + 0.5 - float2(COUNT) * 0.5) * v.rest.size / float2(COUNT);
    ItemVertex q = v;
    q.rest.position = middle;
    q.rest.size = size;
    q.position = middle + (piece.uv - 0.5) * size;
    switch (piece.index)
    {
        case 0: q.wave(10, 120, time); break;
        case 1: q.twist(sin(time) * 1.5); break;
        case 2: q.bulge(float2(0), 90, sin01(time * 2) * 1.6); break;
        case 3: q.squash(0.25 * sin(time * 4)); break;
        case 4: q.sway(sin(time * 2) * 50); break;
        case 5: q.shake(5); break;
        case 6: q.jitter(3); break;
        case 7: q.tilt(sin(time) * 1.1); break;
        default: q.curl(sin01(time * 0.8) * 0.9, 25); break;
    }
    v.position = q.position;
    return v;
}

float4 shade(ItemInput i)
{
    Piece piece = mesh.split(i.uv, COUNT);
    if (piece.seam)
        return colours.transparent;
    colour c = colours.oklch(0.65, 0.12, float(piece.index) / 9);
    c.paint(Checker(1.0 / 8).filled(piece.uv - 0.5, 0.005) * 0.4, colours.white);
    c.paint(mesh.wireframe(piece.uv, float2(31, 17)) * 0.3, colours.black);
    if (piece.index == 8 && !mesh.facing())
        c = colours.ivory.brightened(0.8);   // the back of the page
    return c;
}
