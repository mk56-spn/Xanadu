// TRACE: trace runs on every pixel of the quad as compute, in tiles of [Tile(w, h)]
// threads. A tile's threads may share groupshared memory and wait for each other
// (GroupMemoryBarrierWithGroupSync): load what the tile needs once, then all read it.
// group says where a thread sits: group.thread in its tile, group.place the tile's.
// traced.depth (pixels into the screen) lets the stack's own triangles drawn after
// it hide behind it. It lands first; DEFORM and SHADE draw over it.
import xanadu;

groupshared float wave[8];

[Tile(8, 8)]
float4 trace(ItemInput i, Canvas canvas)
{
    // One thread a column loads, everyone reads.
    if (group.thread.y == 0)
        wave[group.thread.x] = sin(time * 2 + (group.place.x * 8 + group.thread.x) * 0.05) * 60;
    GroupMemoryBarrierWithGroupSync();
    float d = canvas.centred.y - wave[group.thread.x];
    traced.depth = d;
    return lerp(colours.midnightBlue, colours.skyBlue, saturate(0.5 - d / 200));
}
