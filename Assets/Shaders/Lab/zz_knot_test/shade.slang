import xanadu;
Camera camera(float s) { return Camera(1400 * s).orbited(0, 0.35); }
float4 shade(ItemInput i)
{
    float s = i.rest.size.y / 810;
    colour base = colours.oklch(0.72, 0.14, 0.05);
    base.paint(Checker(1.0 / 12).filled(float2(i.uv.x, 0.01), 0.004) * 0.35, colours.white);
    base.paint(mesh.wireframe(i.uv, float2(192, 108)) * 0.5, colours.black);
    return TorusKnot(240, 96, 2, 3).tube(36).turned(float3(0.25, 0, 0)).scaled(s).lit(i.uv, camera(s), base);
}
