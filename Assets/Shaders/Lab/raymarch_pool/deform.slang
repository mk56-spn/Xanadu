// DEFORM: once per sphere. The exact box its outline takes on screen, grown by BLEND (a
// melt bends the surface that far out), at its depth, so nearer spheres draw over farther.
// A box, not a tighter octagon: the corners it wastes are rays that miss the sphere, which
// shade discards after one test, while an octagon's eight inner edges shade the costly
// middle twice along each edge (2 × 2 pixel quads). Measured 2026-11-24: the box is faster.
import xanadu;

ItemVertex deform(ItemVertex v)
{
    Ball reach = Ball(item.ball).grown(BLEND);
    float2 corner = camera().outlined(reach).at(v.uv);
    v.position = v.rest.position + corner;
    // The screen point travels to shade, which builds its ray from it.
    v.local = corner;
    v.depth = camera().depth(reach);
    return v;
}
