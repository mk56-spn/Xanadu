// Open water in triangles (xanadu.mesh), in four steps across the grid: the flat sheet, the
// Gerstner waves shaping it, lit, then coloured as water (deep blue, sky reflected at grazing
// angles, foam on the crests, haze far off).
import xanadu;

static const int2 COUNT = int2(2, 2);

[Default(16)] uniform float waveHeight;
[Default(0.5)] uniform float choppiness;

Camera camera(float s) { return Camera(900 * s).orbited(0, 0.3); }
float3 slot(int k) { return float3((float(k % 2) - 0.5) * 640, (float(k / 2) - 0.5) * 420 + 80, 0); }

float3 shaped(int k, float2 uv)
{
    Ocean sea = Ocean(float2(460, 420), waveHeight, 240, choppiness, 0.3);
    if (k == 0)
        return Sheet(float2(460, 420)).turned(float3(PI / 2, 0, 0)).moved(slot(k)).at(uv);
    return sea.moved(slot(k)).at(uv);
}

float4 shade(ItemInput i)
{
    Piece piece = mesh.split(i.uv, COUNT);
    int k = piece.index;
    if (piece.seam)
        return colours.transparent;
    float2 uv = piece.uv;
    float s = i.rest.size.y / 810;

    // 1 and 2: the bare mesh, flat and then shaped by the waves.
    if (k < 2)
    {
        colour c = colours.steelBlue.withAlpha(0.6);
        c.paint(mesh.wireframe(uv, float2(47, 26)), colours.white);
        return c;
    }

    const float e = 1e-3;
    float3 p = shaped(k, uv) * s;
    float3 n = normalize(cross(shaped(k, uv + float2(0, e)) - shaped(k, uv - float2(0, e)), shaped(k, uv + float2(e, 0)) - shaped(k, uv - float2(e, 0))));
    float3 toEye = normalize(camera(s).eye - p);
    if (dot(n, toEye) < 0)
        n = -n;
    float3 sun = normalize(float3(-0.4, -0.6, 0.7));

    // 3: lit plainly, to see the shape.
    if (k == 2)
        return colour(float3(0.6) * (0.2 + 0.8 * lights.diffuse(n, sun)), 1);

    // 4: water. Deep colour underneath, the sky mirrored where you look across it (Fresnel), the
    // sun's glint, foam on the crests, haze far off.
    float3 deep = float3(0.02, 0.12, 0.2), sky = float3(0.55, 0.7, 0.85);
    float fresnel = 0.04 + 0.96 * pow(1 - saturate(dot(n, toEye)), 5);
    float3 rgb = lerp(deep * (0.5 + 0.5 * lights.wrapped(n, sun, 0.6)), sky, fresnel);
    rgb += lights.specular(n, sun, toEye, 90) * 0.6 * float3(1, 0.95, 0.8);
    Ocean sea = Ocean(float2(460, 420), waveHeight, 240, choppiness, 0.3);
    float foam = sea.crestAt(uv) * (0.6 + 0.4 * noise.value(uv * 80 + time));
    rgb = lerp(rgb, float3(0.9, 0.95, 1), foam);
    rgb = lerp(rgb, sky, camera(s).fog(p, 900 * s, 1300 * s));
    return colour(rgb, 1);
}
