// Library tour: 3D from the triangle grid (xanadu.mesh). The preview's one grid is split
// into twelve pieces (mesh.split); deform shapes each into a surface and shade lights it.
// Every corner is placed once, and pixels only colour.
import xanadu;

// Laid out for a quad 810 px tall; `scale` fits it to the real one (both stages pass theirs).
Camera camera(float scale) { return Camera(1400 * scale).orbited(0, 0.25); }

static const int2 COUNT = int2(4, 3);

// Where each piece sits, in pixels on the screen at depth 0.
float3 slot(int k) { return float3((float(k % 4) - 1.5) * 330, (float(k / 4) - 1) * 270, 0); }

Bezier3 spine() { return Bezier3(float3(-110, 60, 0), float3(-60, -140, -80), float3(60, 140, 80), float3(110, -60, 0)); }

Bezier vase() { return Bezier(float2(40, -90), float2(130, -40), float2(10, 40), float2(70, 90)); }

/// Piece k's surface, placed, at uv: one switch for both stages.
float3 positionOf(int k, float2 uv, float scale)
{
    return shaped(k, uv) * scale;
}

float3 shaped(int k, float2 uv)
{
    float3 spin = float3(0.25, time * 0.5, 0);
    switch (k)
    {
        case 0: return Sphere(100).turned(spin).moved(slot(k)).at(uv);
        case 1: return Torus(85, 30).turned(float3(1.1, time * 0.5, 0)).moved(slot(k)).at(uv);
        case 2: return Cylinder(70, 170).turned(spin).moved(slot(k)).at(uv);
        case 3: return Cylinder(85, 170, 0, 1).turned(spin).moved(slot(k)).at(uv);
        case 4: return Sheet(float2(240, 170)).waving(18, 1.5, time).turned(float3(0.3, sin(time) * 0.6, 0)).moved(slot(k)).at(uv);
        case 5: return vase().lathed().turned(spin).moved(slot(k)).at(uv);
        case 6: return spine().ribbon(50, 1).turned(float3(0.2, sin(time * 0.5) * 0.5, 0)).moved(slot(k)).at(uv);
        case 7: return spine().tube(22, 0.3).turned(float3(0.2, sin(time * 0.5) * 0.5, 0)).moved(slot(k)).at(uv);
        case 8: return Terrain(float2(280, 240), 70, 90, time * 60).turned(float3(0.45, 0, 0)).moved(slot(k) + float3(0, 40, 0)).at(uv);
        case 9: return Sphere(95).lumpy(16, 60, time * 0.5).turned(spin).moved(slot(k)).at(uv);
        case 10: return Sphere(100).turned(spin).moved(slot(k)).at(uv);
        default: return Torus(80, 34).turned(float3(0.5, time * 0.7, time * 0.3)).moved(slot(k)).at(uv);
    }
}

/// The surface's normal at uv, from the same positions.
float3 normalOf(int k, float2 uv, float scale)
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
    // Closed surfaces hide their far side; sheets, ribbons and terrain show both.
    bool closed = k != 4 && k != 6 && k != 8;
    if (piece.seam || (closed && !mesh.facing()))
        return colours.transparent;

    float2 uv = piece.uv;
    float scale = i.rest.size.y / 810;
    colour base = colours.oklch(0.7, 0.13, float(k) / 12);
    // Something painted on in uv, so the shaping shows.
    base.paint(Checker(1.0 / 8).filled((uv - 0.5) * float2(2, 1), 0.01) * 0.35, colours.white);
    if (k == 8)
        base = colours.inferno(0.3 + 0.7 * saturate(-shaped(k, uv).y / 140 + 0.3));

    float3 p = positionOf(k, uv, scale);
    float3 n = normalOf(k, uv, scale);
    float3 toEye = normalize(camera(scale).eye - p);
    if (dot(n, toEye) < 0)
        n = -n;
    float3 key = normalize(float3(-0.5, -0.8, -0.6));
    if (k == 10)   // cel shading
        return colour(base.rgb * (0.3 + 0.7 * lights.toon(n, key, 3)) + lights.rim(n, toEye, 4) * 0.4, 1);
    float light = 0.2 + 0.8 * lights.diffuse(n, key);
    return colour(base.rgb * light + lights.specular(n, key, toEye, 40) * 0.4 + lights.rim(n, toEye, 3) * 0.15, 1);
}
