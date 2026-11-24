// SHADE: the ray through this pixel, marched only through this sphere (grown by BLEND),
// against it and the spheres it melts into. Outside it, the surface is another sphere's
// to draw, in its own quad: a ray that misses it draws nothing, at once.
import xanadu;

// Show each sphere's quad faintly, to see what the pixels cost.
uniform bool show_quads;

/// This sphere and the ones it melts into, melted: distance, normal and colour together.
struct Cloud : IField
{
    Sample sampled(float3 p)
    {
        Melt melt = Melt(BLEND);
        melt.add(Ball(item.ball), p, hueOf(item.index));
        for (uint k = 0; k < item.melts; k++)
            melt.add(Ball(item.near[k]), p, hueOf(item.ids[k]));
        return melt.sample;
    }
};

float4 shade(ItemInput i)
{
    colour miss = show_quads ? colour(1, 1, 1, 0.06) : colours.transparent;
    Ray ray = camera().ray(i.local);
    Span inside = Ball(item.ball).grown(BLEND).entered(ray);
    if (inside.missed)
        return miss;
    Hit hit = ray.marched(Cloud(), inside);
    return hit.found ? hit.lit(camera()) : miss;
}
