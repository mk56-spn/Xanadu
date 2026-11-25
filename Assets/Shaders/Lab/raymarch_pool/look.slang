// Each sphere a copy of a quad: The exact box its outline takes on screen, grown by BLEND (a
// melt bends the surface that far out), at its depth, so nearer spheres draw over farther.
// A box, not a tighter octagon: the corners it wastes are rays that miss the sphere, which
// shade discards after one test, while an octagon's eight inner edges shade the costly
// middle twice along each edge (2 × 2 pixel quads). Measured 2026-11-24: the box is faster.
import xanadu;

[Draw(1, 1, shade: sphereLook, copies: SPHERES)]
ItemVertex sphere(ItemVertex v)
{
    Ball reach = Ball(spheres[copy].ball).grown(BLEND);
    float2 corner = camera().outlined(reach).at(v.uv);
    v.position = v.rest.position + corner;
    // The screen point travels to shade, which builds its ray from it.
    v.local = corner;
    v.depth = camera().depth(reach);
    return v;
}

// The ray through this pixel, marched only through this sphere (grown by BLEND),
// against it and the spheres it melts into. Outside it, the surface is another sphere's
// to draw, in its own quad: a ray that misses it draws nothing, at once.

// Show each sphere's quad faintly, to see what the pixels cost.
uniform bool show_quads;

/// This sphere and the ones it melts into, melted: distance, normal and colour together.
struct Cloud : IField
{
    Sample sampled(float3 p)
    {
        Sphere item = spheres[copy];
        Melt melt = Melt(BLEND);
        melt.add(Ball(item.ball), p, hueOf(item.index));
        for (uint k = 0; k < item.melts; k++)
            melt.add(Ball(item.near[k]), p, hueOf(item.ids[k]));
        return melt.sample;
    }
};

float4 sphereLook(ItemInput i)
{
    colour miss = show_quads ? colour(1, 1, 1, 0.06) : colours.transparent;
    Ray ray = camera().ray(i.local);
    Span inside = Ball(spheres[copy].ball).grown(BLEND).entered(ray);
    if (inside.missed)
        return miss;
    Hit hit = ray.marched(Cloud(), inside);
    return hit.found ? hit.lit(camera()) : miss;
}
