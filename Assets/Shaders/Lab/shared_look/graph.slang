// The reading half of a pair: shows what another material wrote into two [Shared] resources.
// Alone it shows the cleared heat (dark) and no rings. Open it with --beside=Lab/shared_paint:
// that stack is drawn after this one, but writes what this one reads, so its passes run first.
import xanadu;

[Shared] Image<float> heat;
[Shared] [Size(8)] List<float4> spots;
Image<float4> shown;

[Pixels(8, 8)]
void look(ItemInput i, Canvas canvas)
{
    float field = heat[uint2(i.pixel)];
    float3 tone = lerp(colours.midnightBlue.rgb, colours.hotPink.rgb, saturate(field * 0.5));
    tone = lerp(tone, float3(1), saturate(field - 2) * 0.5);
    // A ring round every spot the writer left, in its hue.
    for (int k = 0; k < 8; k++)
    {
        float4 spot = spots[k];
        if (spot.z <= 0)
            continue;
        float ring = sdf.filled(abs(length(i.pixel - spot.xy) - spot.z) - 2, 1);
        tone = lerp(tone, colours.hotPink.hueShifted(spot.w).rgb, ring);
    }
    shown[group.pixel] = float4(tone, 1);
}

void graph()
{
    look();
    present(shown);
}
