// From a grid of triangles to a lit planet, in nine steps (xanadu.mesh). The preview's
// one grid is split into nine pieces; each piece is the planet as it was after that
// step, read left to right, top to bottom.
import xanadu;

static const int2 COUNT = int2(3, 3);
static const float2 CELLS = float2(31, 17);    // each piece's own cells: 96 / 3 - 1 across, 54 / 3 - 1 down

// Laid out for a quad 810 px tall; `scale` fits it to the real one.
Camera camera(float scale) { return Camera(1500 * scale).orbited(0, 0.2); }
float3 slot(int k) { return float3((float(k % 3) - 1) * 420, (float(k / 3) - 1) * 270, 0); }

/// Step k's surface at uv, before the scale.
float3 shaped(int k, float2 uv)
{
    float3 spin = float3(0.35, time * 0.4, 0);
    // 1. The grid as it comes: a flat sheet, uv 0..1 across it.
    if (k == 0)
        return Sheet(float2(300, 200)).moved(slot(k)).at(uv);
    // 2. Bent: u now goes part way round a cylinder.
    if (k == 1)
    {
        float3 flat = Sheet(float2(300, 200)).at(uv);
        float3 round = Cylinder(110, 200).at(float2(0.5 + (uv.x - 0.5) * 0.5, uv.y));
        return lerp(flat, round, 0.8) + slot(k);
    }
    // 3. All the way round: a cylinder, u a whole turn.
    if (k == 2)
        return Cylinder(100, 200).turned(spin).moved(slot(k)).at(uv);
    // 4. v from pole to pole: a sphere. Its far side shows through (nothing sorts the triangles).
    // 5. Its far side hidden (mesh.facing in shade). 6. Lit. 7. Shiny, with a rim.
    if (k < 8)
        return Sphere(110).turned(spin).moved(slot(k)).at(uv);
    // 8. 9. Painted and given mountains: pushed out along its normal by noise.
    return Sphere(110).lumpy(7, 70).turned(spin).moved(slot(k)).at(uv);
}

float3 normalOf(int k, float2 uv)
{
    const float e = 1e-3;
    float3 du = shaped(k, uv + float2(e, 0)) - shaped(k, uv - float2(e, 0));
    float3 dv = shaped(k, uv + float2(0, e)) - shaped(k, uv - float2(0, e));
    return normalize(cross(dv, du));
}

ItemVertex deform(ItemVertex v)
{
    Piece piece = mesh.split(v.uv, COUNT);
    float scale = v.rest.size.y / 810;
    v.position = v.rest.position + camera(scale).projected(shaped(piece.index, piece.uv) * scale);
    return v;
}

float4 shade(ItemInput i)
{
    Piece piece = mesh.split(i.uv, COUNT);
    int k = piece.index;
    if (piece.seam)
        return colours.transparent;
    if (k >= 4 && !mesh.facing())
        return colours.transparent;
    float2 uv = piece.uv;
    float wire = mesh.wireframe(uv, CELLS);

    // u as hue (it wraps round smoothly) and v as brightness: where each uv went.
    colour uvColour = colours.rainbow(uv.x).brightened(1.1 - 0.6 * uv.y);

    // Steps 1 to 4: the bare mesh, its triangles drawn.
    if (k < 4)
    {
        colour c = uvColour.withAlpha(0.85);
        c.paint(wire, colours.white);
        return c;
    }

    float scale = i.rest.size.y / 810;
    float3 p = shaped(k, uv) * scale;
    float3 n = normalOf(k, uv);
    float3 toEye = normalize(camera(scale).eye - p);
    float3 sun = normalize(float3(-0.6, -0.5, -0.6));

    // The planet's colour, from 3D noise on the ball itself (no seam where uv wraps).
    float3 onBall = Sphere(110).at(uv) / 110;
    float land = noise.fbm(onBall * 1.5 + 3.1, 5);
    colour ground = land > 0.52 ? colours.forestGreen.mixed(colours.tan, saturate((land - 0.52) * 6)) : colours.royalBlue.mixed(colours.navy, saturate((0.52 - land) * 5));
    colour base = k < 7 ? uvColour : ground;

    // 5: unlit, triangles still drawn.
    if (k == 4)
    {
        colour c = base;
        c.paint(wire, colours.white.withAlpha(0.6));
        return c;
    }
    float diffuse = lights.diffuse(n, sun);
    // 6: lit by a sun from the upper left.
    if (k == 5)
        return colour(base.rgb * (0.15 + 0.85 * diffuse), 1);
    float3 rgb = base.rgb * (0.15 + 0.85 * diffuse) + lights.specular(n, sun, toEye, 60) * 0.5;
    // 7: shiny, a rim of atmosphere.
    if (k == 6)
        return colour(rgb + lights.rim(n, toEye, 3) * colours.skyBlue.rgb * 0.6, 1);
    // 8: painted from noise; the sea shines, the land doesn't.
    rgb = ground.rgb * (0.12 + 0.88 * diffuse) + (land > 0.52 ? 0 : lights.specular(n, sun, toEye, 80) * 0.6);
    rgb += lights.rim(n, toEye, 3) * colours.skyBlue.rgb * 0.6;
    if (k == 7)
        return colour(rgb, 1);
    // 9: mountains (the lumpy surface), drifting clouds, the night side's city lights.
    float clouds = smoothstep(0.55, 0.7, noise.fbm(onBall * 3 + float3(time * 0.05, 0, 0), 4));
    rgb = lerp(rgb, float3(0.95) * (0.2 + 0.8 * diffuse), clouds * 0.8);
    float night = 1 - smoothstep(0, 0.2, dot(n, sun));
    rgb += float3(1, 0.8, 0.4) * night * step(0.53, land) * step(0.8, noise.value(onBall * 60)) * 0.8;
    return colour(rgb, 1);
}
