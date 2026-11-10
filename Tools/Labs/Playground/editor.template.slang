// A Xanadu shader: shade() returns each pixel's colour. Faded names in calls are hints.
import xanadu;

uniform float time;
uniform float2 centre;

float4 shade(ItemInput i)
{
    float2 p = i.centred - centre;
    let box = Box(float2(80.0), 16.0).move(float2(140.0, 0.0));
    let blob = Circle(120.0 + 20.0 * sin(time * 2.0)).melt(box, 40.0);

    float3 sky = lerp(color.midnightBlue.rgb, color.darkSlateBlue.rgb, i.uv.y);
    float3 colour = lerp(sky, color.gold.rgb, blob.fill(p, 2.0));
    return float4(colour, 1.0);
}
