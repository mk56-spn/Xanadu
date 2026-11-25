// A pixel pass in tiles of 8 × 8 threads. A tile's threads may share groupshared memory and wait
// for each other (GroupMemoryBarrierWithGroupSync): load what the tile needs once, then all read
// it. `group` says where a thread sits: group.thread in its tile, group.place the tile's. It
// writes a colour and a depth (pixels into the screen) a pixel, and present shows them, the
// depth putting it in front of or behind what else the item draws.
import xanadu;

[Image(rgba16f)] RWTexture2D<float4> shown;
[Image(r32f)] RWTexture2D<float> depth;

groupshared float wave[8];

[Pixels(8, 8)]
void sea(ItemInput i, Canvas canvas)
{
    // One thread a column loads, everyone reads.
    if (group.thread.y == 0)
        wave[group.thread.x] = sin(time * 2 + (group.place.x * 8 + group.thread.x) * 0.05) * 60;
    GroupMemoryBarrierWithGroupSync();
    float d = canvas.centred.y - wave[group.thread.x];
    depth[group.pixel] = d;
    shown[group.pixel] = lerp(colours.midnightBlue, colours.skyBlue, saturate(0.5 - d / 200));
}

void graph()
{
    sea();
    present(shown, depth);
}
