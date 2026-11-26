// A room recorded with xanadu.scene: a box on a plinth that thumps on every beat, a ball
// melted on it, a hoop that flashes each bar, pillars in the corners and a ring of pins that
// bob in turn, with a mesh torus from the pool in the same room. One shared scene: the torus
// is drawn and the room marched (only where it is nearer) into one depth and one id image;
// one pass then rebuilds each pixel's surface from whoever made it and lights it once, in the
// room's shadows; present shows the result on the item.
import xanadu;

static const uint PARTS = 256;
static const uint OBJECTS = 129;
[Size(PARTS)] List<scene.Part> parts;
[Size(OBJECTS)] List<scene.Object> objects;
[Clear(1e30)] Image<float> depth;
Image<uint> ids;
Image<float4> shown;
/// How far each 8 x 8 block's cone got: where its pixels' marches start.
[Scale(0.125)] Image<float> coarse;
/// Shadows (x sun, y the lamp) and occlusion (w) at half size: ShadowMask.
[Scale(0.5)] Image<float4> shadowed;
/// The lamp's shadow map all round: six faces, how far the meshes are from the lamp each way.
[Clear(1e30)] Image<float> lampDepth;

static const uint RING = 16;                     // pages the torus's 32 x 32 grid takes (mesh.pagesFor)
[Cached] [Size(RING)] Meshes pool;

[Default(colours.hotPink)] uniform colour tint;
/// How hard the box thumps on the beat.
[Default(0.25)] uniform float thump;
/// Pins round the plinth.
[Default(12)] uniform int pins;
/// Ambient occlusion: creases and contact darkened.
[Default(1)] uniform bool ao;

[Pass(1, over: 1)]
void record(uint i)
{
    Scene s = Scene(parts, PARTS, objects, OBJECTS);
    float hit = beats.pulse();

    // Walls, floor and ceiling: the inside of a block.
    s.add(Room(float3(2600, 1000, 2600)).moved(float3(0, 300, 0)), Material(colours.slateGray, 0));
    // A pillar in each corner: one moved to a corner, mirrored in x and z.
    s.add(Pillar(50, 1000).moved(float3(1000, -200, 1000)).mirrored(float3(1, 0, 1)), colours.lightGray);
    // The plinth, with a bowl cut into its top.
    s.add(Pillar(150, 120).rounded(6).moved(float3(0, 240, 0)), Material(colours.white, 60));
    s.cut(Orb(120).moved(float3(0, 150, 0)), 8);
    // The box: turning, thumping bigger on each beat, a ball melted on top of it.
    s.add(Block(170, 14).turned(float3(0.35, beat * 0.4, 0.2)).scaled(1 + thump * hit).moved(float3(0, 30 - 40 * hit, 0)), Material(tint, 70));
    s.melt(Orb(55).moved(float3(0, -100 - 60 * hit, 0)), Material(colours.gold, 90), 40);
    // A hoop round it, glowing on the first beat of each bar.
    float bar = beats.pulse(4, 0.5);
    s.add(Hoop(300, 7).turned(float3(0.5, 0, 0.25)).moved(float3(0, 10, 0)), Material(colours.cyan, 0, 0.3 + 2.5 * bar));
    // Pins in a ring on the floor, each bobbing on its own beat.
    for (int k = 0; k < pins; k++)
    {
        float a = k * TAU / pins;
        // A wave running round the ring once a bar.
        float bob = saturate(1 - abs(frac(beat / 4 - float(k) / pins + 0.5) - 0.5) * pins * 0.5);
        s.add(Pill(22, 60).moved(float3(cos(a) * 520, 245 - 80 * bob, sin(a) * 520)), Material(colours.hue(float(k) / pins), 50, bob));
    }
    s.finish();
}

/// The camera, swinging slowly round the room: the same for the mesh and the march.
Camera eye(float2 size) { return Camera(1100, size.y * 0.9).orbited(sin(time * 0.25) * 0.7, 0.3); }

/// A closed room keeps the sun out: a lamp hangs under the ceiling instead.
static const float3 LAMP = float3(-350, -550, -300);

Lighting lighting()
{
    Lighting light = Lighting().sunless().lamp(LAMP, colour(1.5, 1.35, 1.2), 1200);
    light.sky = colour(0.18, 0.2, 0.28);
    light.ground = colour(0.1, 0.08, 0.07);
    light.occlusion = ao ? 1 : 0;
    return light;
}

Visibility seen() { return Visibility(depth, ids); }

/// A ring of real triangles (xanadu.mesh's pool), built once, turning on the beat in a corner.
Placement3 ring()
{
    Placement3 p = Placement3();
    p.offset = float3(-620, 60, 380);
    p.angles = float3(1.1, beat * 0.5, 0.3);
    return p;
}

[Pass(128, over: RING * 128)]
void make(uint i) { pool.grid(Torus(160, 40), i, uint2(32), 0, 0); }

[Draw(64, 1, shade: ringSeen, copies: RING)]
ItemVertex ringDrawn(ItemVertex v) { return pool.pulled(v, ring(), eye(v.rest.size)); }

IdTexels ringSeen(ItemInput i) { return Visibility.texels(i.depth, makers.mesh(copy, drawnTriangle)); }

/// The torus again, from the lamp, into each face of its shadow map: so it shadows the room as the room shadows it.
[Draw(64, 1, shade: fromLamp, copies: RING * 6)]
ItemVertex ringFromLamp(ItemVertex v) { return LampShadow.pulled(v, ring(), LAMP, RING, pool); }

float fromLamp(ItemInput i) { return LampShadow.depthOf(i, RING); }

/// How far each 8 x 8 block of pixels can march before anything comes near: a cone each, so
/// every pixel's own march starts there (coarse to fine).
[Pixels(8, 8, over: coarse)]
void cone(ItemInput i, Canvas canvas) { coarse[group.pixel] = scene.coneDepth(parts, objects, eye(canvas.size), canvas, lighting(), 8); }

/// The scene, marched from where its cone got to, and only where it is nearer than the torus.
[Pixels(8, 8)]
void shoot(ItemInput i, Canvas canvas) { scene.shoot(parts, objects, eye(canvas.size), canvas, lighting(), seen(), coarse[group.pixel / 8]); }

/// Whoever made what a pixel shows, its surface rebuilt.
Surface surfaceOf(Seen s)
{
    return s.maker == makers.MESH
        ? Surface(s.onMesh(ring(), pool).normal, Material(colours.orange, 40))
        : scene.surface(parts, objects, s);
}

/// Shadows and occlusion at half size (they are soft): the room's marched shadows, the torus's
/// from the lamp's map, and occlusion from both the room's field and the depth.
[Pixels(8, 8, over: shadowed)]
void shadowing(ItemInput i, Canvas canvas)
{
    Camera camera = eye(canvas.size);
    Lighting light = lighting();
    uint2 under = Visibility.under(group.pixel, 2);
    float2 centred = Visibility.underCentred(canvas.centred, 2);
    Seen s = seen().at(under, camera, centred);
    let casters = both(both(scene.shadows(parts, objects, camera, canvas, light), LampShadow(Lighting.LAMP, lampDepth, LAMP, canvas.size)),
        ScreenOcclusion(depth, camera, float2(under), centred, canvas.size));
    shadowed[group.pixel] = ShadowMask.of(light, s, surfaceOf(s), casters);
}

/// Every pixel's surface rebuilt from whoever made it, then lit once, its shadows and occlusion
/// read back from the half-size mask.
[Pixels(8, 8)]
void illuminate(ItemInput i, Canvas canvas)
{
    Camera camera = eye(canvas.size);
    Seen s = seen().at(group.pixel, camera, canvas);
    shown[group.pixel] = lighting().deferred(s, surfaceOf(s), camera, ShadowMask(shadowed, depth, group.pixel, 2, s.depth));
}

void graph()
{
    record();
    make();
    ringDrawn(depth, ids);
    ringFromLamp(lampDepth);
    cone();
    shoot();
    shadowing();
    illuminate();
    present(shown, depth);
}
