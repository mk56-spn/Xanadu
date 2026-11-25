// From a grid of triangles to a lit planet, in nine steps (xanadu.mesh). Each step is its own
// object in the mesh pool, built once ([Cached]) and turned live by the draw; read them left
// to right, top to bottom.
import xanadu;

static const uint2 CELLS = uint2(32, 16);
static const uint EACH = 8;                      // pages a 32 x 16 grid takes (mesh.pagesFor)
static const uint STEPS = 9;

[Cached] [Buffer(STEPS * EACH * 128)] RWStructuredBuffer<MeshVertex> corners;
[Cached] [Buffer(STEPS * EACH * 128)] RWStructuredBuffer<uint> triangles;
[Cached] [Buffer(STEPS * EACH)] RWStructuredBuffer<Meshlet> meshlets;

// Laid out for a quad 810 px tall; `scale` fits it to the real one.
Camera camera(float scale) { return Camera(1500 * scale).orbited(0, 0.2); }
float3 slot(uint k) { return float3((float(k % 3) - 1) * 420, (floor(float(k) / 3) - 1) * 270, 0); }

/// Step 2: a sheet bent part way round a cylinder (u half a turn), a surface of our own.
struct Bent : ISurface
{
    Placement3 placement3 = Placement3();
    float3 local(float2 uv)
    {
        float3 flat = Sheet(float2(300, 200)).at(uv);
        float3 round = Cylinder(110, 200).at(float2(0.5 + (uv.x - 0.5) * 0.5, uv.y));
        return lerp(flat, round, 0.8);
    }
}

/// Step k turns from step 3 on; the first two lie still.
Placement3 placed(uint k, float scale)
{
    Placement3 p;
    p.offset = slot(k) * scale;
    p.angles = k < 2 ? float3(0) : float3(0.35, time * 0.4, 0);
    p.scale = scale;
    return p;
}

[Pass(128, over: STEPS * EACH * 128)]
void make(uint i)
{
    uint k = i / (EACH * 128), t = i % (EACH * 128), first = k * EACH;
    // 1. The grid as it comes: a flat sheet, uv 0..1 across it.
    if (k == 0)
        mesh.grid(Sheet(float2(300, 200)), t, CELLS, first, k, corners, triangles, meshlets);
    // 2. Bent: u now goes part way round a cylinder.
    else if (k == 1)
        mesh.grid(Bent(), t, CELLS, first, k, corners, triangles, meshlets);
    // 3. All the way round: a cylinder, u a whole turn.
    else if (k == 2)
        mesh.grid(Cylinder(100, 200), t, CELLS, first, k, corners, triangles, meshlets);
    // 4. v from pole to pole: a sphere. 5. Coloured: depth hides its far side. 6. Lit. 7. Shiny, with a rim.
    else if (k < 8)
        mesh.grid(Sphere(110), t, CELLS, first, k, corners, triangles, meshlets);
    // 8. 9. Painted and given mountains: pushed out along its normal by noise.
    else
        mesh.grid(Sphere(110).lumpy(7, 70), t, CELLS, first, k, corners, triangles, meshlets);
}

[Draw(64, 1, shade: look, copies: STEPS * EACH)]
ItemVertex shown(ItemVertex v)
{
    float scale = v.rest.size.y / 810;
    return mesh.pulled(v, placed(meshlets[copy].object, scale), camera(scale), corners, triangles, meshlets);
}

float4 look(ItemInput i)
{
    uint k = meshlets[copy].object;
    float2 uv = i.uv;
    float wire = mesh.wireframe(uv, float2(CELLS));

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
    float3 n = normalize(i.normal);
    float3 toEye = normalize(camera(scale).eye - i.world);
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

void graph()
{
    make();
    shown(frame);
}
