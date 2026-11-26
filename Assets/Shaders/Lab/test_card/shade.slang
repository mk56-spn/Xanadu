// Something busy to judge blurs and screen reads by: open a stack with --under=Lab/test_card. Fine stripes
// in the corners, a pixel checker, thin rings, and small bright dots on dark (where blocky or ringing blurs
// show first), all on a slow hue sweep.
import xanadu;

float4 shade(Canvas canvas)
{
    float2 p = canvas.centred;
    float2 q = p / canvas.size + 0.5;
    float3 c = colours.midnightBlue.rgb * 0.6;
    // Stripes, finer towards the left; a one-pixel checker on the right.
    if (q.y < 0.25)
        c = lerp(c, colours.hue(q.x).rgb, step(0.5, frac(p.x / lerp(2, 24, q.x))));
    else if (q.y > 0.75)
        c = lerp(c, float3(1), step(0.5, frac((floor(p.x) + floor(p.y)) * 0.5)) * step(0.5, q.x));
    // Thin rings round the middle.
    c = lerp(c, colours.hotPink.rgb, sdf.filled(abs(frac(length(p) / 40) - 0.5) * 40 - 0.75, 1) * step(0.25, q.y) * step(q.y, 0.75));
    // A grid of small bright dots.
    float2 cell = frac(p / 64) * 64 - 32;
    c = lerp(c, float3(1, 0.95, 0.6) * 2, sdf.filled(length(cell) - 3, 1));
    return float4(c, 1);
}
