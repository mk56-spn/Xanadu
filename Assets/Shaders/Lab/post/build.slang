// A full-screen pass over what is already drawn under the item: post-processing. `bright` reads
// the canvas so far (sampleScreen, a blurred mip) at half size and keeps what is past `threshold`;
// `grade` reads it again at full size with the canvas depth, adds the bloom, tints by depth, and
// rings a lens round the pointer-free centre. Reading the canvas so far makes both passes run
// mid-frame: the item's pass breaks for them, and the board says so. Stretch the item over the
// whole screen and it is a full-screen post pass.
import xanadu;

[Scale(0.5)] Image<float4> bloom;
Image<float4> shown;

/// How bright the canvas must be to bloom.
[Default(0.55)] uniform float threshold;
/// How strongly the bloom adds back.
[Default(1.2)] uniform float strength;
/// Tint by the canvas depth: nearer things (drawn later) warmer.
[Default(0.25)] uniform float depthTint;

[Pixels(8, 8, over: bloom)]
void bright(ItemInput i)
{
    float3 c = sampleScreen(i.screenUv, 3).rgb;
    bloom[group.pixel] = float4(max(c - threshold, 0) / (1 - threshold), 1);
}

[Pixels(8, 8)]
void grade(ItemInput i, Canvas canvas)
{
    float3 c = sampleScreen(i.screenUv).rgb;
    float near = 1 - sampleDepth(i.screenUv);
    c = lerp(c, c * float3(1.1, 0.95, 0.8), saturate(near) * depthTint);
    c += bloom[group.pixel / 2].rgb * strength;
    // A lens in the middle: the canvas under it read through a bend, its rim lit.
    float r = length(canvas.centred) / (min(canvas.size.x, canvas.size.y) * 0.3);
    if (r < 1)
        c = sampleScreen(i.screenUv - canvas.centred / frame.screen * 0.25 * (1 - r * r)).rgb * 1.1;
    c += float3(0.6, 0.8, 1) * exp(-abs(r - 1) * 40) * 0.6;
    shown[group.pixel] = float4(c, 1);
}

void graph()
{
    bright();
    grade();
    present(shown);
}
