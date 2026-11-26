// Every instance's primitives placed for the frame, a group of 64 an instance: in pixels, tinted, clipped, their
// depth and run slot, in order. A primitive outside its clip becomes a skip (the vertex stage folds it away), so
// order needs no scan. Plain opaque rects leave the ordered list for the pre-pass list: a slot per placed primitive,
// holding its opaque cut or a skip, drawn whole.

static const uint KindRect = 0, KindTriangle = 3, KindSkip = 0xFF;
static const uint FlagExact = 1 << 9;
static const uint NoTexture = 0xFFFF;
/// A slot with this bit already names a glyph page's layer: it passes through unmapped.
static const uint GlyphLayer = 0x8000;

float4 unpackColour(uint rg, uint ba)
{
    return float4(f16tof32(rg & 0xFFFF), f16tof32(rg >> 16), f16tof32(ba & 0xFFFF), f16tof32(ba >> 16));
}

uint2 packColour(float4 c)
{
    return uint2(f32tof16(c.r) | (f32tof16(c.g) << 16), f32tof16(c.b) | (f32tof16(c.a) << 16));
}

bool whole(float v) { return abs(v - round(v)) < 1e-3; }

[Pass(64, groups: instanceCount)]
void expand(uint i)
{
    Placing at = instances[i / 64];
    Affine place = Affine(at.basis, at.origin.xy);
    uint count = at.range.y;
    for (uint k = i % 64; k < count; k += 64)
    {
        Drawn p = pool[at.range.x + k];
        uint kind = p.more.z & 0xFF;
        float2 lo, hi;
        if (kind == KindTriangle)
        {
            float2 a = place.apply(p.a.xy), b = place.apply(p.a.zw), c = place.apply(p.b.xy);
            p.a = float4(a, b);
            p.b = float4(c, p.b.zw);
            lo = min(a, min(b, c));
            hi = max(a, max(b, c));
        }
        else
        {
            float2 centre = place.apply(p.a.xy);
            float2 x = place.turn(p.a.zw), y = place.turn(p.b.xy);
            p.a = float4(centre, x);
            p.b = float4(y, p.b.zw * at.origin.z);
            float2 reach = abs(x) + abs(y);
            lo = centre - reach;
            hi = centre + reach;
        }

        p.colours.xy = packColour(unpackColour(p.colours.x, p.colours.y) * at.tint);
        p.colours.zw = packColour(unpackColour(p.colours.z, p.colours.w) * at.tint);
        p.more.xy = packColour(unpackColour(p.more.x, p.more.y) * at.tint);
        p.clip = at.clip;
        p.extra = float4(1.0 - float(at.more.w + k + 1) * DepthStep, 0, 0, 0);
        uint local = p.more.w;
        if (local != NoTexture && (local & GlyphLayer) == 0)
            p.more.w = (at.more.x & ResidentSlots) != 0 ? runSlots[at.range.w + local] : slots[at.range.w + local];

        uint out = at.range.z + k;
        // A pixel of slack for a smoothed edge.
        bool outside = hi.x + 1 <= at.clip.x || hi.y + 1 <= at.clip.y || lo.x - 1 >= at.clip.z || lo.y - 1 >= at.clip.w;
        float4 c0 = unpackColour(p.colours.x, p.colours.y);
        bool solid = kind == KindRect && local == NoTexture && p.b.z == 0 && p.b.w == 0
            && p.a.w == 0 && p.b.x == 0 && c0.a >= 1.0 && all(p.colours.xy == p.colours.zw)
            && whole(lo.x) && whole(lo.y) && whole(hi.x) && whole(hi.y);

        Drawn cut = p;
        cut.more.z = KindSkip;
        if (!outside && solid && (at.more.x & AllowOpaque) != 0)
        {
            // Cut to the clip here, so the pre-pass never needs to discard.
            float2 cutLo = max(lo, at.clip.xy), cutHi = min(hi, at.clip.zw);
            if (all(cutHi > cutLo))
            {
                cut.a = float4((cutLo + cutHi) * 0.5, (cutHi.x - cutLo.x) * 0.5, 0);
                cut.b = float4(0, (cutHi.y - cutLo.y) * 0.5, 0, 0);
                cut.more.z = KindRect | FlagExact;
            }

            outside = true;
        }

        opaque[at.more.y + k] = cut;
        if (outside)
            p.more.z = KindSkip;
        placed[out] = p;
    }
}
