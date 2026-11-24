// Library tour: the 2D corner moves for deform (xanadu.mesh), one per piece of the grid:
// wave, twist, bulge, squash, sway, shake, jitter, tilt, page curl. Each piece is moved
// to its own place first, then bent as if it were the whole quad.
import xanadu;

static const int2 COUNT = int2(3, 3);

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
