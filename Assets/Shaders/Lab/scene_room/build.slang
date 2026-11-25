// A room recorded with xanadu.scene: a box on a plinth that thumps on every beat, a ball
// melted on it, a hoop that flashes each bar, pillars in the corners and a ring of pins that
// bob in turn. The graph records it once a frame, shoots it a pixel at a time into two images,
// and presents them on the item.
import xanadu;

[Buffer(256)] RWStructuredBuffer<scene.Part> parts;
[Buffer(129)] RWStructuredBuffer<scene.Object> objects;
[Image(rgba16f)] RWTexture2D<float4> shown;
[Image(r32f)] RWTexture2D<float> shownDepth;

[Options("lit|steps|tile lists|normals")] uniform int view;

[Default(colours.hotPink)] uniform colour tint;
/// How hard the box thumps on the beat.
[Default(0.25)] uniform float thump;
/// Pins round the plinth.
[Default(12)] uniform int pins;

[Pass(1, over: 1)]
void record(uint i)
{
    Scene s = Scene(parts, objects);
    float hit = beats.pulse();

    // Walls, floor and ceiling: the inside of a block.
    s.add(Block(float3(2600, 1000, 2600)).moved(float3(0, -200, 0)).inverted(), Material(colours.slateGray, 0));
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

/// A ring of real triangles (xanadu.mesh), turning on the beat in a corner.
Torus ring() { return Torus(160, 40).turned(float3(1.1, beat * 0.5, 0.3)).moved(float3(-620, 60, 380)); }

[Draw(96, 32, shade: ringLook)]
ItemVertex ringDrawn(ItemVertex v)
{
    v.place(ring().at(v.uv), eye(v.rest.size));
    return v;
}

float4 ringLook(ItemInput i, Canvas canvas) { return ring().lit(i.uv, eye(canvas.size), colours.orange); }

/// The room, marched where it is nearer than what was drawn.
[Pixels(8, 8)]
void shoot(ItemInput i, Canvas canvas)
{
    Camera camera = eye(canvas.size);
    // A closed room keeps the sun out: a lamp hangs under the ceiling instead.
    Lighting light = Lighting().sunless().lamp(float3(-350, -550, -300), colour(1.5, 1.35, 1.2), 1200);
    light.sky = colour(0.18, 0.2, 0.28);
    light.ground = colour(0.1, 0.08, 0.07);
    float drawn = shownDepth[group.pixel];
    colour c = scene.shot(parts, objects, camera, canvas, light, view);
    if (c.a > 0 && traced.depth < drawn)
    {
        shown[group.pixel] = c;
        shownDepth[group.pixel] = traced.depth;
    }
}

void graph()
{
    clear(shown);
    clear(shownDepth, 1e30);
    record();
    ringDrawn(shown, shownDepth);
    shoot();
    present(shown, shownDepth);
}
