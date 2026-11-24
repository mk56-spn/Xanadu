import xanadu;

[Default(colours.gold)] uniform colour tint;

float4 shade(ItemInput i, Canvas canvas)
{
    colour sky = lerp(colours.midnightBlue, colours.darkSlateBlue, i.uv.y);
    let blob = Circle(120 + 20 * sin(time * 2));
    return lerp(sky, tint, blob.filled(canvas.centred, 2));
}
