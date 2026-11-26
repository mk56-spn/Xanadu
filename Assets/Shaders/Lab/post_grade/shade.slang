// A post material: the renderer draws it over the whole frame after the tree (CanvasRenderer.AddPost), reading
// the frame so far. Open any stack with --post=Lab/post_grade: a warm grade, a vignette and faint scanlines.
import xanadu;

[Default(0.6)] uniform float vignette;

float4 shade(ItemInput i, Canvas canvas)
{
    float3 c = sampleScreen(i.screenUv).rgb;
    c = lerp(c, c * float3(1.08, 0.98, 0.86), 0.8);
    float2 d = i.screenUv - 0.5;
    c *= 1 - vignette * saturate(dot(d, d) * 2.2);
    c *= 0.94 + 0.06 * step(0.5, frac(i.pixel.y * 0.5));
    return float4(c, 1);
}
