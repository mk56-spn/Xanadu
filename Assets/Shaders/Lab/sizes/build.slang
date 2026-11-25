// Images of other sizes than the item's box. `palette` is fixed (64 × 64) and the material's:
// a [Pass] paints it once a frame, however many items show the stack, and every item reads it.
// `glow` is a quarter of the box: soft blobs worked out a texel at a time over it
// ([Pixels(…, over: glow)]), each texel standing for 4 × 4 pixels. `shown` is the box: sharp
// rings drawn at full size over the glow, read back at a quarter. A waving band of triangles is
// drawn straight onto the frame after them, its shade reading the glow by name at the pixel's
// texel. `coarse` presents the glow alone, stretched over the box.
import xanadu;

[Image(rgba16f, 64, 64)] RWTexture2D<float4> palette;
[Image(rgba16f, scale: 0.25)] RWTexture2D<float4> glow;
[Image(rgba16f)] RWTexture2D<float4> shown;

/// Show the quarter-size glow alone, stretched over the box.
[Default(0)] uniform int coarse;
/// Blobs drifting round the item.
[Default(6)] uniform int blobs;

/// One texel of the palette a thread: a ramp of hues, turning on the beat.
[Pass(64, over: 64 * 64)]
void paint(uint i)
{
    uint2 at = uint2(i % 64, i >> 6);
    float shift = floor(beat) * 0.07;
    palette[at] = float4(colours.hotPink.hueShifted(float(at.x) / 64 + shift).rgb * (0.4 + 0.6 * float(at.y) / 63), 1);
}

/// Where blob `k` is, in pixels from the item's centre.
float2 blob(int k, float2 size)
{
    float a = k * TAU / max(blobs, 1) + time * (0.3 + 0.07 * k);
    return float2(cos(a), sin(a * 1.3)) * size * 0.32;
}

[Pixels(8, 8, over: glow)]
void soft(ItemInput i, Canvas canvas)
{
    float field = 0;
    for (int k = 0; k < blobs; k++)
        field += 2600 / (dot(canvas.centred - blob(k, canvas.size), canvas.centred - blob(k, canvas.size)) + 1);
    float4 tone = palette[uint2(uint(saturate(field * 0.25) * 63), uint(saturate(field) * 63))];
    glow[group.pixel] = tone * saturate(field) + float4(colours.midnightBlue.rgb, 1) * (1 - saturate(field));
}

/// A band across the middle, waving.
[Draw(48, 8, shade: sheetLook)]
ItemVertex sheetDrawn(ItemVertex v)
{
    float2 size = v.rest.size;
    v.position = v.rest.corner + float2(v.uv.x * size.x, size.y * (0.4 + 0.2 * v.uv.y) + sin(v.uv.x * 9 + time * 2.5) * size.y * 0.12);
    return v;
}

/// The glow under it, turned inside out, in stripes.
float4 sheetLook(ItemInput i)
{
    float4 under = glow[group.pixel / 4];
    float stripe = step(0.5, frac(i.uv.x * 16));
    return float4(1 - under.rgb, 0.8) * stripe;
}

[Pixels(8, 8)]
void rings(ItemInput i, Canvas canvas)
{
    float4 under = glow[group.pixel / 4];
    float ring = 0;
    for (int k = 0; k < blobs; k++)
        ring = max(ring, sdf.filled(abs(length(canvas.centred - blob(k, canvas.size)) - 40 - 12 * beats.pulse()) - 2, 1));
    shown[group.pixel] = lerp(under, float4(1, 1, 1, 1), ring);
}

void graph()
{
    paint();
    soft();
    if (coarse != 0)
        present(glow);
    else
    {
        rings();
        present(shown);
        sheetDrawn(frame);
    }
}
