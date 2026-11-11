// A Xanadu shader: shade() returns each pixel's colour. Faded names in calls are hints.
import xanadu;

[Default(40)] uniform float melt;
[Default(colours.gold)] uniform colour tint;

float4 shade(ItemInput i, Canvas canvas)
{
    float2 p = canvas.centred - mouse;
    let box = Box(float2(160), 16).moved(float2(140, 0));
    let blob = Circle(120 + 20 * sin(time * 2)).melted(box, melt);

    colour sky = lerp(colours.midnightBlue, colours.darkSlateBlue, i.uv.y);
    return lerp(sky, tint, blob.filled(p, 2));
}
