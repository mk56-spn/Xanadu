// Library tour: more 3D from the grid (xanadu.mesh): tubes along a spline and a helix,
// superquadrics from cube to star, a Bézier patch, a sheet folding into a ball, and a 2D spline.
import xanadu;

static const int2 COUNT = int2(4, 3);

// Laid out for a quad 810 px tall; `scale` fits it to the real one.
Camera camera(float scale) { return Camera(1400 * scale).orbited(0, 0.3); }
float3 slot(int k) { return float3((float(k % 4) - 1.5) * 330, (float(k / 4) - 1) * 270, 0); }

Spline3 path()
{
    Spline3 p;
    p.add(float3(-120, 40, 0)); p.add(float3(-60, -60, 60)); p.add(float3(0, 50, -40));
    p.add(float3(60, -50, 40)); p.add(float3(120, 30, 0));
    return p;
}

float3 shaped(int k, float2 uv)
{
    float3 spin = float3(0.3, time * 0.5, 0);
    switch (k)
    {
        case 0: return path().tube(16, 0.4).turned(float3(0.2, sin(time * 0.5) * 0.4, 0)).moved(slot(k)).at(uv);
        case 1: return Helix(60, 200, 3).tube(12).turned(spin).moved(slot(k)).at(uv);
        case 2: return path().ribbon(40, 1.5).turned(float3(0.2, sin(time * 0.5) * 0.4, 0)).moved(slot(k)).at(uv);
        case 3: return Helix(70, 160, 1.5).ribbon(30, 3).turned(spin).moved(slot(k)).at(uv);
        case 4: return Superquadric(100, 0.15, 0.15).turned(spin).moved(slot(k)).at(uv);
        case 5: return Superquadric(100, 1, 0.3).turned(spin).moved(slot(k)).at(uv);
        case 6: return Superquadric(100, 2, 2).turned(spin).moved(slot(k)).at(uv);
        case 7: return Superquadric(95, 3, 3).turned(spin).moved(slot(k)).at(uv);
        case 8: return Patch(float2(240, 200)).raised(1, 1, float3(0, 0, -150)).raised(2, 2, float3(0, 0, 120)).raised(3, 0, float3(0, 0, -80))
                    .turned(float3(0.5, sin(time * 0.5) * 0.6, 0)).moved(slot(k)).at(uv);
        case 9: return Sheet(float2(260, 200)).morphed(Sphere(100), sin01(time * 1.5)).turned(float3(0.2, time * 0.3, 0)).moved(slot(k)).at(uv);
        case 10: return Cylinder(70, 180).morphed(Torus(80, 30).turned(float3(PI / 2, 0, 0)), sin01(time)).turned(spin).moved(slot(k)).at(uv);
        default: return Sheet(float2(260, 200)).moved(slot(k)).at(uv);
    }
}

float3 normalOf(int k, float2 uv)
{
    const float e = 1e-3;
    float3 du = shaped(k, uv + float2(e, 0)) - shaped(k, uv - float2(e, 0));
    float3 dv = shaped(k, uv + float2(0, e)) - shaped(k, uv - float2(0, e));
    return normalize(cross(dv, du));
}

float4 shade(ItemInput i)
{
    Piece piece = mesh.split(i.uv, COUNT);
    int k = piece.index;
    if (piece.seam)
        return colours.transparent;
    float2 uv = piece.uv;
    // The last piece stays flat: a 2D spline drawn through five points, with its points.
    if (k == 11)
    {
        float2 p = (uv - 0.5) * float2(260, 200);
        colour c = colours.midnightBlue.withAlpha(0.9);
        let s = Spline(4).added(float2(-110, 60)).added(float2(-50, -70)).added(float2(10, 40)).added(float2(60, -60)).added(float2(110, 20));
        c.paint(s.filled(p), colours.gold);
        for (int n = 0; n < 5; n++)
            c.paint(Circle(6).moved(s.points[n]).stroked(p, 2), colours.white);
        return c;
    }
    bool closed = k >= 4 && k <= 7 || k == 1 || k == 0;
    if (closed && !mesh.facing())
        return colours.transparent;

    float scale = i.rest.size.y / 810;
    colour base = colours.oklch(0.72, 0.13, float(k) / 12);
    base.paint(Checker(1.0 / 8).filled((uv - 0.5) * float2(2, 1), 0.01) * 0.3, colours.white);
    float3 p = shaped(k, uv) * scale;
    float3 n = normalOf(k, uv);
    float3 toEye = normalize(camera(scale).eye - p);
    if (dot(n, toEye) < 0)
        n = -n;
    float3 key = normalize(float3(-0.5, -0.8, -0.6));
    return colour(base.rgb * (0.2 + 0.8 * lights.diffuse(n, key)) + lights.specular(n, key, toEye, 40) * 0.4 + lights.rim(n, toEye, 3) * 0.15, 1);
}
