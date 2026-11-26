// Dual Kawase blur (Marius Bjørge, ARM, SIGGRAPH 2015): what is under the item, halved four times and
// doubled back four times, each step a handful of bilinear taps round the texel. The blur's width comes
// from the chain, not from the taps, so a wide blur costs about what a narrow one does; most of the work
// is at small sizes. Left of the split it is the chain; right of it, the screen copy's own Gaussian mips
// at about the same width, to compare.
import xanadu;

[Scale(0.5)] Image<float4> down1;
[Scale(0.25)] Image<float4> down2;
[Scale(0.125)] Image<float4> down3;
[Scale(0.0625)] Image<float4> down4;
[Scale(0.125)] Image<float4> up3;
[Scale(0.25)] Image<float4> up2;
[Scale(0.5)] Image<float4> up1;
Image<float4> shown;

/// How far the taps reach, in texels of the image read: 1 is the paper's; more is wider and rougher.
[Default(1)] uniform float offset;
/// Where the comparison starts, across the item: 1 shows the chain alone.
[Default(0.5)] uniform float split;
/// The screen mip the right side reads.
[Default(4)] uniform float mip;

/// The texels an image covers on this item: the box at its scale, rounded up.
float2 extent(Canvas canvas, float scale) { return max(ceil(canvas.size * scale), 1.0); }

/// The down step's taps, in texels of the image read: the middle four times, and four diagonals a texel out.
float4 down(Sampled image, float2 at, float2 size)
{
    float h = offset;
    return (image.at(at, size) * 4
        + image.at(at + float2(-h, -h), size) + image.at(at + float2(h, -h), size)
        + image.at(at + float2(-h, h), size) + image.at(at + float2(h, h), size)) / 8;
}

/// The up step's taps: four a texel out along the axes, and four diagonals half a texel out, weighted twice.
float4 up(Sampled image, float2 at, float2 size)
{
    float h = offset * 0.5;
    return (image.at(at + float2(-2 * h, 0), size) + image.at(at + float2(2 * h, 0), size)
        + image.at(at + float2(0, -2 * h), size) + image.at(at + float2(0, 2 * h), size)
        + (image.at(at + float2(-h, -h), size) + image.at(at + float2(h, -h), size)
        + image.at(at + float2(-h, h), size) + image.at(at + float2(h, h), size)) * 2) / 12;
}

/// Where a step down reads, and a step up: its own texel's middle, in texels of the image before it.
float2 below(uint2 texel) { return (float2(texel) + 0.5) * 2; }
float2 above(uint2 texel) { return (float2(texel) + 0.5) / 2; }

/// The first step reads the screen itself, a pixel out round the texel's middle.
[Pixels(8, 8, over: down1)]
void first(ItemInput i)
{
    float2 px = 1 / frame.screen;
    float h = offset;
    float2 uv = i.screenUv;
    down1[group.pixel] = (sampleScreen(uv) * 4
        + sampleScreen(uv + float2(-h, -h) * px) + sampleScreen(uv + float2(h, -h) * px)
        + sampleScreen(uv + float2(-h, h) * px) + sampleScreen(uv + float2(h, h) * px)) / 8;
}

// Each step reads the image before it through its sampler: sampled(name) hands it to the helpers.
[Pixels(8, 8, over: down2)]
void second(ItemInput i, Canvas canvas) { down2[group.pixel] = down(sampled(down1), below(group.pixel), extent(canvas, 0.5)); }

[Pixels(8, 8, over: down3)]
void third(ItemInput i, Canvas canvas) { down3[group.pixel] = down(sampled(down2), below(group.pixel), extent(canvas, 0.25)); }

[Pixels(8, 8, over: down4)]
void fourth(ItemInput i, Canvas canvas) { down4[group.pixel] = down(sampled(down3), below(group.pixel), extent(canvas, 0.125)); }

[Pixels(8, 8, over: up3)]
void rise3(ItemInput i, Canvas canvas) { up3[group.pixel] = up(sampled(down4), above(group.pixel), extent(canvas, 0.0625)); }

[Pixels(8, 8, over: up2)]
void rise2(ItemInput i, Canvas canvas) { up2[group.pixel] = up(sampled(up3), above(group.pixel), extent(canvas, 0.125)); }

[Pixels(8, 8, over: up1)]
void rise1(ItemInput i, Canvas canvas) { up1[group.pixel] = up(sampled(up2), above(group.pixel), extent(canvas, 0.25)); }

/// The last step up, at full size; right of the split, the Gaussian mip instead, and a line between.
[Pixels(8, 8)]
void last(ItemInput i, Canvas canvas)
{
    float across = (canvas.centred.x / canvas.size.x) + 0.5;
    float4 c = across < split
        ? up(sampled(up1), above(group.pixel), extent(canvas, 0.5))
        : sampleScreen(i.screenUv, mip);
    c.rgb += sdf.filled(abs(across - split) * canvas.size.x - 1, 1) * 0.6;
    shown[group.pixel] = float4(c.rgb, 1);
}

void graph()
{
    first();
    second();
    third();
    fourth();
    rise3();
    rise2();
    rise1();
    last();
    present(shown);
}
