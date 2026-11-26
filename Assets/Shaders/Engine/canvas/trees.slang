// The resident trees: the CPU sends only the slots that changed (patchNodes); each frame every drawn slot walks its
// ancestors, as the CPU walk did, and writes the instance `expand` places it with, after the CPU's (resolve).

/// A 2D place: two axes and an origin (Transform2D).
struct Affine
{
    float4 basis;
    float2 origin;

    __init(float4 basis, float2 origin)
    {
        this.basis = basis;
        this.origin = origin;
    }

    float2 apply(float2 p) { return origin + basis.xy * p.x + basis.zw * p.y; }

    float2 turn(float2 v) { return basis.xy * v.x + basis.zw * v.y; }

    /// This, then `inner`: inner's points placed by inner, then by this (Transform2D's this * inner).
    Affine then(Affine inner) { return Affine(float4(turn(inner.basis.xy), turn(inner.basis.zw)), apply(inner.origin)); }

    /// The box around a rect (min x, y, max x, y) once placed.
    float4 bounds(float4 r)
    {
        float2 p0 = apply(r.xy), p1 = apply(float2(r.z, r.y)), p2 = apply(r.zw), p3 = apply(float2(r.x, r.w));
        return float4(min(min(p0, p1), min(p2, p3)), max(max(p0, p1), max(p2, p3)));
    }
};

/// Rect2.Intersection: false unless they overlap with room to spare.
bool cutInto(inout float4 a, float4 b)
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

[Pass(64, over: patchCount)]
void patchNodes(uint i)
{
    NodePatch p = patches[i];
    nodes[p.slot.x] = p.node;
}

static const uint MaxDepth = 64;

[Pass(64, over: entryCount)]
void resolve(uint i)
{
    TreeEntry e = entries[i];
    if (e.tree >= treeCount)
        return;
    TreeMount tree = trees[e.tree];
    // Not drawn this frame, or an entry its tree no longer holds.
    if (tree.more.z == 0 || i < tree.bases.y || i >= tree.bases.y + tree.bases.z)
        return;

    uint nodeBase = tree.bases.x;
    TreeNode n = nodes[nodeBase + e.slot];
    TreePiece piece = pieces[tree.more.x + e.piece];

    uint chain[MaxDepth];
    uint depth = 0;
    uint p = n.info.x;
    while (p != NoParent && depth < MaxDepth)
    {
        chain[depth++] = p;
        p = nodes[nodeBase + p].info.x;
    }

    // Down from the top: each ancestor's place, fade and the cut it gives its children, in the mount's space.
    Affine world = Affine(float4(1, 0, 0, 1), float2(0, 0));
    float alpha = 1;
    bool cut = false, gone = false;
    float4 box = float4(0, 0, 0, 0);
    for (int k = int(depth) - 1; k >= 0; k--)
    {
        TreeNode a = nodes[nodeBase + chain[k]];
        world = world.then(Affine(a.basis, a.origin.xy));
        alpha *= a.origin.z;
        if ((a.info.y & NodeClips) != 0)
        {
            float4 own = world.bounds(a.clip);
            if (!cut)
                box = own;
            else if (!cutInto(box, own))
                gone = true;
            cut = true;
        }
    }

    world = world.then(Affine(n.basis, n.origin.xy));
    alpha *= n.origin.z;

    Affine mount = Affine(tree.basis, tree.origin.xy);
    Affine at = mount.then(world).then(Affine(n.itemBasis, n.itemOrigin.xy));
    float4 tint = tree.tint * float4(1, 1, 1, alpha) * n.itemTint;

    float4 clip = tree.clip;
    if (cut)
        clip = pixelCut(clip, mount.bounds(box));
    if ((n.info.y & NodeItemClips) != 0)
        clip = pixelCut(clip, at.bounds(n.itemClip));
    if (gone || (n.info.y & NodeHidden) != 0 || tint.a <= 0 || clip.z <= clip.x || clip.w <= clip.y)
        clip = float4(-1e9, -1e9, -1e9, -1e9);

    float2 x = at.basis.xy, y = at.basis.zw;
    Placing placing;
    placing.basis = at.basis;
    placing.origin = float4(at.origin, sqrt(abs(x.x * y.y - x.y * y.x)), 0);
    placing.tint = tint;
    placing.clip = clip;
    placing.range = uint4(n.info.z, n.info.w, piece.outBase + e.out, tree.more.y + e.slotBase);
    placing.more = uint4(AllowOpaque | ResidentSlots, piece.opaqueBase + e.out, 0, piece.depthBase + e.out);
    instances[tree.bases.w + (i - tree.bases.y)] = placing;
}
