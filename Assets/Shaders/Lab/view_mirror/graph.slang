// A [View] image: the canvas drawn again, before this graph runs, from the subtree the material names.
// The shader tool names the preview's backdrop (and what hangs under it), flipped top to bottom about the
// preview's middle, so the lower half reads its own reflection: water, rippled and tinted. The top half
// is left clear, so the backdrop shows as it is. This item never draws into its own view.
import xanadu;

[View] Image<float4> mirrored;
Image<float4> shown;

[Pixels(8, 8)]
void water(ItemInput i, Canvas canvas)
{
    if (canvas.centred.y < 0)
        return;
    // Deeper water ripples more, and darker.
    float depth = canvas.centred.y / (canvas.size.y * 0.5);
    float2 wobble = float2(sin(canvas.centred.y * 0.11 - time * 2.6) * 7, sin(canvas.centred.x * 0.05 + time * 1.7) * 3) * (0.3 + depth);
    uint w = 0, h = 0;
    mirrored.GetDimensions(w, h);
    float2 at = clamp(i.pixel + wobble, float2(0), float2(w - 1, h - 1));
    float4 seen = mirrored[uint2(at)];
    float3 sea = colours.midnightBlue.rgb;
    float3 tone = lerp(seen.rgb, sea, 0.25 + 0.5 * depth);
    // A bright line where the water meets the air.
    tone += sdf.filled(canvas.centred.y - 1.5, 1.5) * 0.35;
    shown[group.pixel] = float4(tone, 1);
}

void graph()
{
    water();
    present(shown);
}
