// DEFORM: each copy of the quad is a spark on a tilted ring that threads through the galaxy and
// the pillars, turned to face the eye. Its depth is the march's, so the blobs hide it; it is
// [Translucent], so the glows blend over each other rather than cutting holes in those behind.
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
ItemVertex deform(ItemVertex v)
{
    Camera camera = eyeOn(v.rest);
    float4 spark = sparkAt(copy);
    float ahead = max(camera.depth(spark.xyz), 1.0);
    float2 centre = v.rest.position + camera.projected(spark.xyz);
    v.position = centre + (v.uv * 2 - 1) * spark.w * camera.focal / ahead;
    v.depth = ahead - camera.distance;
    return v;
}
