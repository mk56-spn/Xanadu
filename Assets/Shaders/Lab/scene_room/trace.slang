// The room through a camera swinging slowly round it, lit by a lamp under the ceiling.
import xanadu;

[Options("lit|steps|tile lists|normals")] uniform int view;

[Tile(8, 8)]
float4 trace(ItemInput i, Canvas canvas)
{
    Camera camera = Camera(1100, canvas.size.y * 0.9).orbited(sin(time * 0.25) * 0.7, 0.3);
    // A closed room keeps the sun out: a lamp hangs under the ceiling instead.
    Lighting light = Lighting().sunless().lamp(float3(-350, -550, -300), colour(1.5, 1.35, 1.2), 1200);
    light.sky = colour(0.18, 0.2, 0.28);
    light.ground = colour(0.1, 0.08, 0.07);
    return scene.shot(parts, objects, camera, canvas, light, view);
}
