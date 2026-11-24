// Library tour: 2D things made 3D (xanadu.mesh): library shapes raised as reliefs, a spline
// spun into a goblet, a spline stood up as a wall. All from the one grid, nine pieces.
import xanadu;

static const int2 COUNT = int2(3, 3);

Camera camera(float scale) { return Camera(1400 * scale).orbited(0, 0.35); }
float3 slot(int k) { return float3((float(k % 3) - 1) * 420, (float(k / 3) - 1) * 270, 0); }

Spline goblet()
{
    // x out from the axis, y down: rim, bowl, stem, foot.
    return Spline().added(float2(70, -100)).added(float2(60, -40)).added(float2(12, 10)).added(float2(10, 60)).added(float2(60, 100));
}

Spline wall() { return Spline().added(float2(-140, 30)).added(float2(-60, -40)).added(float2(20, 40)).added(float2(140, -20)); }

float3 tilt() { return float3(0.6, sin(time * 0.6) * 0.5, 0); }

float3 shaped(int k, float2 uv)
{
    float3 tilt = tilt();
    float2 size = float2(300, 200);
    switch (k)
    {
        case 0: return Star(5, 80, 0.5).rotated(time * 0.3).relief(size, 30, 16).turned(tilt).moved(slot(k)).at(uv);
        case 1: return Heart(150).relief(size, 40, 30).turned(tilt).moved(slot(k)).at(uv);
        case 2: return Digits(floor(time) % 100, 110, 22, 2).relief(size, 25, 10).turned(tilt).moved(slot(k)).at(uv);
        case 3: return goblet().lathed().turned(float3(0.25, time * 0.5, 0)).moved(slot(k)).at(uv);
        case 4: return wall().extruded(120).turned(float3(0.6, time * 0.4, 0)).moved(slot(k)).at(uv);
        case 5: return Truchet(60, 14).relief(size, 18, 8).turned(tilt).moved(slot(k)).at(uv);
        case 6: return Circle(60).hollowed(14).or(Box(float2(20, 160), 8)).relief(size, 30, 12).turned(tilt).moved(slot(k)).at(uv);
        case 7: return Hexagons(70, 8).relief(size, 20, 6).turned(tilt).moved(slot(k)).at(uv);
        default: return Arrow(220, 40, 70).rotated(sin(time) * 0.4).relief(size, 30, 14).turned(tilt).moved(slot(k)).at(uv);
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
    if (piece.seam || (k == 3 && !mesh.facing()))
        return colours.transparent;
    float scale = i.rest.size.y / 810;
    float2 uv = piece.uv;
    float3 p = shaped(k, uv) * scale;
    float3 n = normalOf(k, uv);
    float3 toEye = normalize(camera(scale).eye - p);
    if (dot(n, toEye) < 0)
        n = -n;
    // Raised parts gold, the sheet underneath grey: the height above the sheet's own plane.
    float3 up = rotate3(float3(0, 0, -1), tilt());
    float raised = saturate(dot(shaped(k, uv) - slot(k), up) / 12);
    colour base = k == 3 || k == 4 ? colours.oklch(0.7, 0.12, float(k) / 9) : colours.lightSlateGray.mixed(colours.gold, raised);
    float3 key = normalize(float3(-0.5, -0.8, -0.6));
    return colour(base.rgb * (0.2 + 0.8 * lights.diffuse(n, key)) + lights.specular(n, key, toEye, 50) * 0.5 + lights.rim(n, toEye, 3) * 0.1, 1);
}
