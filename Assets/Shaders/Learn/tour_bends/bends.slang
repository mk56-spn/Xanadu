// Library tour: the 2D corner moves for deform (xanadu.mesh): wave, twist, bulge, squash, sway,
// shake, jitter, tilt, page curl. One draw of nine copies, each a grid of its own: a copy is
// moved to its own place first, then bent as if it were the whole quad.
import xanadu;

static const float2 CELLS = float2(31, 17);

[Draw(31, 17, shade: look, copies: 9)]
ItemVertex bent(ItemVertex v)
{
    // Make this copy's corner look like a corner of a whole quad of the copy's size.
    float2 at = float2(copy % 3, copy / 3);
    float2 size = v.rest.size / 3 * 0.8;
    ItemVertex q = v;
    q.rest.position = v.rest.position + (at - 1) * v.rest.size / 3;
    q.rest.size = size;
    q.position = q.rest.position + (v.uv - 0.5) * size;
    switch (copy)
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
    v.depth = q.depth;
    return v;
}

float4 look(ItemInput i)
{
    colour c = colours.oklch(0.65, 0.12, float(copy) / 9);
    c.paint(Checker(1.0 / 8).filled(i.uv - 0.5, 0.005) * 0.4, colours.white);
    c.paint(mesh.wireframe(i.uv, CELLS) * 0.3, colours.black);
    if (copy == 8 && !mesh.facing())
        c = colours.ivory.brightened(0.8);   // the back of the page
    return c;
}

void graph() { bent(frame); }
