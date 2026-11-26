// The canvas's two draws, each drawn at many places in a job (the host names the range of copies, a copy a placed
// primitive): `prepass`, the job's opaque rects front to back, depth written, so what lies under them costs nothing
// after; then `ordered`, each run of placed primitives at its place among the job's other steps, depth tested.
// Boxes, discs, rings and textured quads are shaped here by signed distance, so their edges are smooth at any turn or
// scale without being cut into triangles; free triangles carry their own corners. Each primitive's depth is its place
// in the frame, kept exactly. A run's eight textures and the glyph pages are the host's (Rendering/Passes/CanvasStack.cs).
import xanadu.shape;

static const uint KindDisc = 1, KindTexture = 2, KindHalo = 4;
static const uint FlagVertical = 1 << 8;
/// A triangle whose corners are a quadratic Bézier's start, control and end, trimmed to the curve here (Loop–Blinn:
/// u² − v); the CPU never samples it. With FlagConcave it keeps the side towards the control, not towards the chord.
static const uint FlagCurve = 1 << 10, FlagConcave = 1 << 11;

[[vk::binding(3, 2)]] Sampler2D texture0;
[[vk::binding(4, 2)]] Sampler2D texture1;
[[vk::binding(5, 2)]] Sampler2D texture2;
[[vk::binding(6, 2)]] Sampler2D texture3;
[[vk::binding(7, 2)]] Sampler2D texture4;
[[vk::binding(8, 2)]] Sampler2D texture5;
[[vk::binding(9, 2)]] Sampler2D texture6;
[[vk::binding(10, 2)]] Sampler2D texture7;
/// Every text atlas page as a layer, so glyphs need no slot of their run.
[[vk::binding(11, 2)]] Sampler2DArray glyphPages;

/// A primitive's corner: the grid's corner (`v.uv`) on its box, grown a pixel all round for a shaped kind's smoothed
/// edge, or a triangle's own corners (three, then a fold to nothing).
ItemVertex cornerOf(ItemVertex v, Drawn p)
{
    uint kind = p.more.z & 0xFF;
    v.depth = p.extra.x;
    if (kind == KindSkip)
    {
        v.position = float2(-16.0);
        return v;
    }

    if (kind == KindTriangle)
    {
        uint k = v.index % 6;
        uint at = k < 3 ? k : 0;
        v.position = at == 0 ? p.a.xy : at == 1 ? p.a.zw : p.b.xy;
        v.uv = at == 0 ? p.uv.xy : at == 1 ? p.uv.zw : p.b.zw;
        v.colour = at == 0 ? unpackColour(p.colours.x, p.colours.y)
            : at == 1 ? unpackColour(p.colours.z, p.colours.w)
            : unpackColour(p.more.x, p.more.y);
        // A curve triangle's canonical coordinates: start (0, 0), control (½, 0), end (1, 1), the curve where u² = v.
        v.local = at == 0 ? float2(0.0) : at == 1 ? float2(0.5, 0.0) : float2(1.0);
        return v;
    }

    float2 c = v.uv * 2.0 - 1.0;
    float2 axisX = p.a.zw, axisY = p.b.xy;
    float2 half = float2(length(axisX), length(axisY));
    float pad = kind == KindTexture || kind == KindHalo || (p.more.z & FlagExact) != 0 ? 0.0 : 1.0;
    float2 grown = half + pad;
    float2 dirX = axisX / max(half.x, 1e-6), dirY = axisY / max(half.y, 1e-6);
    v.position = p.a.xy + dirX * (c.x * grown.x) + dirY * (c.y * grown.y);
    v.local = c * grown;
    float2 t = saturate(c * grown / max(half, 1e-6) * 0.5 + 0.5);
    v.uv = lerp(p.uv.xy, p.uv.zw, t);
    // A halo's uv runs past its region over the pad; the shade keeps reads inside the region.
    if (kind == KindHalo)
        v.uv = lerp(p.uv.xy, p.uv.zw, c * half / max(half - p.b.z, 1e-6) * 0.5 + 0.5);
    // A two-colour box is a ramp along x, or along y when flagged.
    float along = (p.more.z & FlagVertical) != 0 ? t.y : t.x;
    v.colour = lerp(unpackColour(p.colours.x, p.colours.y), unpackColour(p.colours.z, p.colours.w), along);
    return v;
}

float4 sampleSlot(uint slot, float2 uv)
{
    if ((slot & GlyphLayer) != 0)
        return glyphPages.Sample(float3(uv, float(slot & ~GlyphLayer)));

    switch (slot)
    {
    case 0: return texture0.Sample(uv);
    case 1: return texture1.Sample(uv);
    case 2: return texture2.Sample(uv);
    case 3: return texture3.Sample(uv);
    case 4: return texture4.Sample(uv);
    case 5: return texture5.Sample(uv);
    case 6: return texture6.Sample(uv);
    case 7: return texture7.Sample(uv);
    default: return float4(1.0);
    }
}

/// A halo: its region's alpha under a gaussian reaching three σ each way (reads outside the region are clear), times
/// its intensity, clamped. Its σ rides in the stroke, its pad in the radius.
float4 haloOf(Drawn p, float2 half, float2 uv, float4 colour)
{
    float sigma = max(p.b.w, 0.5);
    float2 inner = max(half - p.b.z, 1e-6);
    float2 uvPerPixel = (p.uv.zw - p.uv.xy) / (inner * 2.0);
    // Taps at most two pixels apart, so the bilinear reads between them leave no gaps (wider spacing shows as a grid
    // on thin strokes), out to three σ.
    float spacing = min(0.6 * sigma, 2.0);
    int reach = min((int)ceil(3.0 * sigma / spacing), 12);
    float sum = 0.0, weights = 0.0;
    for (int y = -reach; y <= reach; y++)
    {
        for (int x = -reach; x <= reach; x++)
        {
            float2 offset = float2(x, y) * spacing;
            float w = exp(-0.5 * dot(offset, offset) / (sigma * sigma));
            float2 at = uv + offset * uvPerPixel;
            bool inside = all(at >= p.uv.xy) && all(at <= p.uv.zw);
            sum += w * (inside ? sampleSlot(p.more.w, at).a : 0.0);
            weights += w;
        }
    }

    colour.a *= saturate(sum / weights * (float(p.more.z >> 16) / 1000.0));
    return colour;
}

[Translucent] [OrderDepth] [Draw(1, 1, shade: primitiveLook, blend: mix)]
ItemVertex ordered(ItemVertex v) { return cornerOf(v, placed[copy]); }

float4 primitiveLook(ItemInput i)
{
    Drawn p = placed[copy];
    // The curve's distance in pixels, from u² − v over its screen gradient; taken before any branch so the
    // derivatives are defined everywhere.
    float2 dx = ddx(i.local), dy = ddy(i.local);
    float curve = i.local.x * i.local.x - i.local.y;
    float2 slope = float2(2.0 * i.local.x * dx.x - dx.y, 2.0 * i.local.x * dy.x - dy.y);
    float across = curve / max(length(slope), 1e-6);
    float2 at = i.pixel;
    if (at.x < p.clip.x || at.y < p.clip.y || at.x >= p.clip.z || at.y >= p.clip.w)
        return float4(0.0);

    uint kind = p.more.z & 0xFF;
    float2 half = float2(length(p.a.zw), length(p.b.xy));
    float radius = p.b.z, stroke = p.b.w;
    if (kind == KindHalo)
        return haloOf(p, half, i.uv, i.vertexColour);

    float cover = 1.0;
    if (kind == KindTriangle && (p.more.z & FlagCurve) != 0)
        cover = (p.more.z & FlagConcave) != 0 ? saturate(0.5 + across) : saturate(0.5 - across);
    else if (kind == KindRect)
    {
        float d = Box(half * 2.0, radius).local(i.local);
        cover = stroke > 0 ? sdf.filled(d) - sdf.filled(d + stroke) : sdf.filled(d);
    }
    else if (kind == KindDisc)
    {
        // An ellipse measured as a circle in its own unit space, scaled back by its smaller side.
        float d = Circle(1.0).local(i.local / max(half, 1e-6)) * min(half.x, half.y);
        cover = stroke > 0 ? sdf.filled(d) - sdf.filled(d + stroke) : sdf.filled(d);
    }

    float4 colour = i.vertexColour;
    if (p.more.w != NoTexture)
        colour *= sampleSlot(p.more.w, i.uv);
    colour.a *= cover;
    return colour;
}

/// The opaque pre-pass: pixel-aligned plain rects, clipped on the CPU, so nothing stands between them and early depth
/// rejection.
[Opaque] [OrderDepth] [Draw(1, 1, shade: opaqueLook)]
ItemVertex prepass(ItemVertex v) { return cornerOf(v, opaque[copy]); }

float4 opaqueLook(ItemInput i) { return float4(i.vertexColour.rgb, 1.0); }
