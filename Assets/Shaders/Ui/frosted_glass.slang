// Blurs whatever is drawn behind a translucent panel, so text on it stays
// readable: the screen read at a blurrier mip, with the panel's own colour laid
// over it by its alpha. The host builds the screen's mips through a Gaussian
// filter, so one read of a low level is already smooth.
import xanadu;

[Default(3)] uniform float lod;

float4 shade(ItemInput i)
{
    float3 behind = sampleScreen(i.screenUv, lod).rgb;
    return float4(lerp(behind, i.colour.rgb, i.colour.a), 1.0);
}
