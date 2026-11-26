// The writing half of a pair: two [Shared] resources, the frame's, and nothing drawn of its own.
// `heat` is the screen's size: a field of blobs drifting over this item's box. `spots` says where
// each blob is on the screen this frame. Lab/shared_look shows them; open it with
// --beside=Lab/shared_paint (this stack drawn after it) and the runner still runs this one first.
import xanadu;

[Shared] Image<float> heat;
[Shared] [Size(8)] List<float4> spots;

static const int BLOBS = 6;

/// Where blob `k` is, in pixels from the item's centre.
float2 blob(int k, float2 size)
{
    float a = k * TAU / BLOBS + time * (0.4 + 0.09 * k);
    return float2(cos(a), sin(a * 1.3)) * size * 0.32;
}

[Pixels(8, 8)]
void paint(ItemInput i, Canvas canvas)
{
    float field = 0;
    for (int k = 0; k < BLOBS; k++)
    {
        float2 d = canvas.centred - blob(k, canvas.size);
        field += 2600 / (dot(d, d) + 1);
    }
    heat[uint2(i.pixel)] = field;
    // A thread a blob says where it is on the screen: its place, its ring's radius, its hue.
    if (group.pixel.y == 0 && group.pixel.x < BLOBS)
    {
        int k = int(group.pixel.x);
        spots[k] = float4(i.pixel - canvas.centred + blob(k, canvas.size), 36 + 10 * k, float(k) / BLOBS);
    }
}

void graph() { paint(); }
