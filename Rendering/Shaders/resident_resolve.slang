// A mount's tree kept on the GPU (Docs/host.md §5.3). Records stay
// between frames; the CPU sends only the slots that changed (applyPatches).
// Each frame resolve walks every drawn slot's ancestors, as the CPU walk
// did, and writes the instance primitive_expand.slang places it with.
//
// SDL_GPU's compute layout: read-only storage set 0, read-write set 1, uniforms set 2.

struct Node
{
    float4 basis;       // local x axis, y axis
    float4 origin;      // local origin xy, opacity, unused
    float4 clip;        // its children's cut in its own space: min x, y, max x, y
    float4 itemBasis;
    float4 itemOrigin;
    float4 itemTint;
    float4 itemClip;
    uint4 info;         // parent, flags, pool start, count
};

struct Patch
{
    uint4 slot;
    Node node;
};

struct Entry
{
    uint slot;
    uint out;
    uint slotBase;
    uint piece;
};

struct Piece
{
    uint outBase;
    uint depthBase;
    uint opaqueBase;
    uint unused;
};

struct Instance
{
    float4 basis;
    float4 origin;
    float4 tint;
    float4 clip;
    uint4 range;
    uint4 more;
};

struct PatchUniforms
{
    uint count;
    uint pad0, pad1, pad2;
};

struct ResolveUniforms
{
    float4 mountBasis;
    float4 mountOrigin;   // xy, unused
    float4 mountTint;
    float4 mountClip;     // pixels: min x, y, max x, y
    uint count;
    uint instanceBase;
    uint pad0, pad1;
};

static const uint NoParent = 0xFFFFFFFF;
static const uint FlagClips = 1, FlagItemClips = 2, FlagHidden = 4;
static const uint AllowOpaque = 1, ResidentSlots = 2;
static const uint MaxDepth = 64;

// ---- Resolve ----------------------------------------------------------------

[[vk::binding(0, 0)]] StructuredBuffer<Node> nodes;
[[vk::binding(1, 0)]] StructuredBuffer<Entry> entries;
[[vk::binding(2, 0)]] StructuredBuffer<Piece> pieces;
[[vk::binding(0, 1)]] RWStructuredBuffer<Instance> instances;
[[vk::binding(0, 2)]] ConstantBuffer<ResolveUniforms> resolving;

struct Affine
{
    float4 basis;
    float2 origin;
};

Affine affine(float4 basis, float2 origin)
{
    Affine a;
    a.basis = basis;
    a.origin = origin;
    return a;
}

float2 apply(Affine a, float2 p) { return a.origin + a.basis.xy * p.x + a.basis.zw * p.y; }
float2 turn(Affine a, float2 v) { return a.basis.xy * v.x + a.basis.zw * v.y; }

/// a then b, as Transform2D's a * b: b's points placed by b, then by a.
Affine compose(Affine a, Affine b)
{
    return affine(float4(turn(a, b.basis.xy), turn(a, b.basis.zw)), apply(a, b.origin));
}

/// The box around a rect (min x, y, max x, y) once placed.
float4 bounds(Affine a, float4 r)
{
    float2 p0 = apply(a, r.xy), p1 = apply(a, float2(r.z, r.y)), p2 = apply(a, r.zw), p3 = apply(a, float2(r.x, r.w));
    return float4(min(min(p0, p1), min(p2, p3)), max(max(p0, p1), max(p2, p3)));
}

/// Rect2.Intersection: empty unless they overlap with room to spare.
bool intersect(inout float4 a, float4 b)
{
    if (!(a.x < b.z && b.x < a.z && a.y < b.w && b.y < a.w))
        return false;
    a = float4(max(a.xy, b.xy), min(a.zw, b.zw));
    return true;
}

/// The renderer's pixel rect: floored and ceiled, then cut to the other.
float4 pixelCut(float4 pixels, float4 box)
{
    float4 snapped = float4(floor(box.xy), ceil(box.zw));
    return float4(max(pixels.xy, snapped.xy), min(pixels.zw, snapped.zw));
}

[shader("compute")]
[numthreads(64, 1, 1)]
void resolve(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= resolving.count)
        return;

    Entry e = entries[id.x];
    Node n = nodes[e.slot];
    Piece piece = pieces[e.piece];

    uint chain[MaxDepth];
    uint depth = 0;
    uint p = n.info.x;
    while (p != NoParent && depth < MaxDepth)
    {
        chain[depth++] = p;
        p = nodes[p].info.x;
    }

    // Down from the top: each ancestor's place, fade and the cut it gives its children, in the mount's space.
    Affine world = affine(float4(1, 0, 0, 1), float2(0, 0));
    float alpha = 1;
    bool cut = false, gone = false;
    float4 box = float4(0, 0, 0, 0);
    for (int k = int(depth) - 1; k >= 0; k--)
    {
        Node a = nodes[chain[k]];
        world = compose(world, affine(a.basis, a.origin.xy));
        alpha *= a.origin.z;
        if ((a.info.y & FlagClips) != 0)
        {
            float4 own = bounds(world, a.clip);
            if (!cut)
                box = own;
            else if (!intersect(box, own))
                gone = true;
            cut = true;
        }
    }

    world = compose(world, affine(n.basis, n.origin.xy));
    alpha *= n.origin.z;

    Affine mount = affine(resolving.mountBasis, resolving.mountOrigin.xy);
    Affine placed = compose(compose(mount, world), affine(n.itemBasis, n.itemOrigin.xy));
    float4 tint = resolving.mountTint * float4(1, 1, 1, alpha) * n.itemTint;

    float4 clip = resolving.mountClip;
    if (cut)
        clip = pixelCut(clip, bounds(mount, box));
    if ((n.info.y & FlagItemClips) != 0)
        clip = pixelCut(clip, bounds(placed, n.itemClip));
    if (gone || (n.info.y & FlagHidden) != 0 || tint.a <= 0 || clip.z <= clip.x || clip.w <= clip.y)
        clip = float4(-1e9, -1e9, -1e9, -1e9);

    float2 x = placed.basis.xy, y = placed.basis.zw;
    Instance at;
    at.basis = placed.basis;
    at.origin = float4(placed.origin, sqrt(abs(x.x * y.y - x.y * y.x)), 0);
    at.tint = tint;
    at.clip = clip;
    uint out = piece.outBase + e.out;
    at.range = uint4(n.info.z, n.info.w, out, e.slotBase);
    at.more = uint4(AllowOpaque | ResidentSlots, piece.opaqueBase + e.out, 0, piece.depthBase + e.out);
    instances[resolving.instanceBase + id.x] = at;
}
