// A card that folds into a ball on every beat and springs back flat (xanadu.mesh morphed,
// beats.bounce). Its shape changes every frame, so it is rebuilt into the mesh pool each frame
// and drawn by one counted draw. Play the shader tool's timeline to see it move: it runs on
// the beat, as it would in a level.
import xanadu;

[Default(colours.hotPink)] uniform colour tint;
[Default(2)] uniform float every;

static const uint2 CELLS = uint2(64, 40);
static const uint PAGES = 40;                    // pages a 64 x 40 grid takes (mesh.pagesFor)

[Buffer(PAGES * 128)] RWStructuredBuffer<MeshVertex> corners;
[Buffer(PAGES * 128)] RWStructuredBuffer<uint> triangles;
[Buffer(PAGES)] RWStructuredBuffer<Meshlet> meshlets;

Camera camera(float s) { return Camera(1100 * s).orbited(sin(time * 0.3) * 0.3, 0.2); }

/// How folded it is: snaps into the ball right on the beat (overshooting a little), holds,
/// then eases back flat by half way to the next.
float folded()
{
    float t = beats.phase(every);
    float closing = ease.outBack(saturate(t * 8));
    float opening = 1 - ease.inOutCubic(saturate((t - 0.2) / 0.3));
    return max(closing * opening, 0);
}

[Pass(128, over: PAGES * 128)]
void make(uint i)
{
    let card = Sheet(float2(560, 320)).waving(10, 1, time);
    mesh.grid(card.morphed(Sphere(150), folded()), i, CELLS, 0, 0, corners, triangles, meshlets);
}

[Draw(64, 1, shade: look, copies: PAGES)]
ItemVertex shown(ItemVertex v)
{
    float s = v.rest.size.y / 810;
    Placement3 p = Placement3();
    p.angles = float3(0.2, sin(time * 0.5) * 0.5, 0);
    p.scale = s;
    return mesh.pulled(v, p, camera(s), corners, triangles, meshlets);
}

float4 look(ItemInput i)
{
    float s = i.rest.size.y / 810;
    // The card's face: stripes and a ring, painted on in uv, so they wrap round the ball.
    colour c = tint.mixed(colours.indigo, i.uv.y);
    c.paint(Stripes(0.06).filled(i.uv * float2(1.75, 1), 0.004) * 0.35, colours.white);
    c.paint(Circle(0.18).hollowed(0.02).filled((i.uv - 0.5) * float2(1.75, 1), 0.004), colours.white);

    // Depth lets the ball's near side cover its far side by itself; the card's back, facing
    // away from the eye, just shows paler.
    float3 n = normalize(i.normal);
    float3 toEye = normalize(camera(s).eye - i.world);
    if (dot(n, toEye) < 0)
    {
        c = c.greyed().brightened(0.5);
        n = -n;
    }
    float3 key = normalize(float3(-0.5, -0.8, -0.6));
    float3 rgb = c.rgb * (0.25 + 0.75 * lights.diffuse(n, key)) + lights.specular(n, key, toEye, 40) * 0.4;
    return colour(rgb, 1);
}

void graph()
{
    make();
    shown(frame);
}
