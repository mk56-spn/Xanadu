// SHADE: the field over the background, a trail segment's soft line, or a particle in its look.
import xanadu;

float4 shade(ItemInput i)
{
    Drawn d = drawnOf(copy);
    if (d.kind == FIELD)
    {
        float shown = 1 - exp(-fieldIn(lists[PARITY] * CELLS, i.local) * gain);
        return float4(background.rgb + tint.rgb * shown * tint.a, 1);
    }

    float a = d.kind == SEGMENT
        ? sdf.filled(abs(i.local.y) - i.uv.y, 1.2)
        : particles.cover(emitterOf(pool[d.slot].emitter).look, i.local, i.uv.x, i.uv.y);
    // The vertex colour alone: uv carries the sizes here, not a place on the item's texture.
    return float4(i.vertexColour.rgb, i.vertexColour.a * a);
}
