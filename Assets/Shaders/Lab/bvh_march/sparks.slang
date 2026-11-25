// Each copy of a quad is a spark on a tilted ring that threads through the galaxy and the
// pillars, turned to face the eye. Its depth is the march's, so the blobs hide it; it is
// [Translucent], so the glows blend over each other rather than cutting holes in those behind.
// Its glow is hottest at its heart; its hue by which one it is.
import xanadu;

/// Spark c where it is now, in the galaxy's pixels, and its half size.
float4 sparkAt(uint c)
{
    float ring = 2.5 + 7.5 * frac(c * 0.7548776662);
    float a = frac(c * 0.6180339887) * 2.0 * PI + time * (0.6 + 1.2 / ring);
    float tilt = (frac(c * 0.5698402910) - 0.5) * 0.9;
    float y = 4.2 + sin(a) * ring * tilt + sin(time * 2.0 + c) * 0.15;
    return float4(cos(a) * ring * U, (LIFT - y) * U, sin(a) * ring * U, (0.05 + 0.05 * frac(c * 0.3247)) * U);
}

[Translucent]
[Draw(1, 1, shade: sparkLook, copies: SPARKS)]
ItemVertex spark(ItemVertex v)
{
    Camera camera = eyeOn(v.rest);
    float4 spark = sparkAt(copy);
    float ahead = max(camera.depth(spark.xyz), 1.0);
    float2 centre = v.rest.position + camera.projected(spark.xyz);
    v.position = centre + (v.uv * 2 - 1) * spark.w * camera.focal / ahead;
    v.depth = ahead - camera.distance;
    return v;
}

float4 sparkLook(ItemInput i)
{
    float r = length(i.uv * 2 - 1);
    float glow = saturate(1.0 - r);
    glow = glow * glow * (0.4 + 2.0 * pow(glow, 6.0));
    colour tint = colours.hue(frac(copy * 0.0011 + 0.1));
    return colour(lerp(colours.white, tint, 0.45).rgb, saturate(glow));
}
