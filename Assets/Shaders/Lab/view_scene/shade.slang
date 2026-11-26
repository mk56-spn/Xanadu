// Something for Lab/view_mirror to reflect: open that stack with --beside=Lab/view_scene. A sun off to the
// left and a row of hills, all in the top half; the bottom half is clear, so the water drawn under it shows.
import xanadu;

float4 shade(Canvas canvas)
{
    float2 p = canvas.centred;
    if (p.y >= 0)
        return float4(0);
    float2 sun = float2(-canvas.size.x * 0.25, -canvas.size.y * 0.3);
    float lit = sdf.filled(length(p - sun) - canvas.size.y * 0.09, 1.5);
    float hills = -canvas.size.y * 0.08 + sin(p.x * 0.012) * canvas.size.y * 0.05 + sin(p.x * 0.031 + 1.3) * canvas.size.y * 0.02;
    float ground = sdf.filled(hills - p.y, 1.5);
    float3 tone = lerp(colours.hotPink.rgb, colours.midnightBlue.rgb * 1.6, ground);
    return float4(tone, max(lit, ground));
}
