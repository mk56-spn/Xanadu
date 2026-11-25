// Open water in triangles (xanadu.mesh), in four steps: the flat sheet, the Gerstner waves
// shaping it, lit, then coloured as water (deep blue, sky reflected at grazing angles, foam on
// the crests, haze far off). The waves move, so the four are rebuilt into the mesh pool every
// frame and drawn by one counted draw.
import xanadu;

static const uint2 CELLS = uint2(48, 32);
static const uint EACH = 24;                     // pages a 48 x 32 grid takes (mesh.pagesFor)
static const uint STEPS = 4;

[Default(16)] uniform float waveHeight;
[Default(0.5)] uniform float choppiness;

[Pages(STEPS * EACH)] Meshes pool;

// Laid out for a quad 810 px tall; `s` fits it to the real one.
Camera camera(float s) { return Camera(900 * s).orbited(0, 0.3); }
float3 slot(uint k) { return float3((float(k % 2) - 0.5) * 640, (floor(float(k) / 2) - 0.5) * 420 + 80, 0); }

Ocean sea() { return Ocean(float2(460, 420), waveHeight, 240, choppiness, 0.3); }

Placement3 placed(uint k, float s)
{
    Placement3 p;
    p.offset = slot(k) * s;
    p.angles = float3(0);
    p.scale = s;
    return p;
}

[Pass(128, over: STEPS * EACH * 128)]
void make(uint i)
{
    MeshPage p = pool.page(i, EACH);
    uint k = p.object;
    if (k == 0)
        pool.grid(Sheet(float2(460, 420)).turned(float3(PI / 2, 0, 0)), p, CELLS);
    else
        pool.grid(sea(), p, CELLS);
}

[Draw(64, 1, shade: look, copies: STEPS * EACH)]
ItemVertex shown(ItemVertex v)
{
    float s = v.rest.size.y / 810;
    return pool.pulled(v, placed(pool.objectOf(copy), s), camera(s));
}

float4 look(ItemInput i)
{
    uint k = pool.objectOf(copy);
    float s = i.rest.size.y / 810;

    // 1 and 2: the bare mesh, flat and then shaped by the waves.
    if (k < 2)
    {
        colour c = colours.steelBlue.withAlpha(0.6);
        c.paint(mesh.wireframe(i.uv, float2(CELLS)), colours.white);
        return c;
    }

    float3 toEye = normalize(camera(s).eye - i.world);
    float3 n = normalize(i.normal);
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
    float foam = sea().crestAt(i.uv) * (0.6 + 0.4 * noise.value(i.uv * 80 + time));
    rgb = lerp(rgb, float3(0.9, 0.95, 1), foam);
    rgb = lerp(rgb, sky, camera(s).fog(i.world, 900 * s, 1300 * s));
    return colour(rgb, 1);
}

void graph()
{
    make();
    shown(frame);
}
