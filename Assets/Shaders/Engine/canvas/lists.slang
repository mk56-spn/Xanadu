// The canvas (Docs/host.md §5.3, Docs/render-graph.md "The resident UI on the graph"): everything plain the
// renderer draws, as one stack it runs itself. What it keeps: every recording's primitives in their own space
// (`pool`), the resident trees' records, and each frame the instances the CPU and the trees place them with.
// The renderer sizes every list and writes the [Host] ones (Rendering/Passes/CanvasStack.cs).
import xanadu.math;

/// One primitive: its shape, uv, clip, colours, kind, texture slot and depth (Rendering/Primitive.cs, GpuPrimitive).
struct Drawn
{
    float4 a;       // box: centre xy, half axis x; triangle: corner 0, corner 1
    float4 b;       // box: half axis y, corner radius, stroke; triangle: corner 2, uv 2
    float4 uv;
    float4 clip;    // pixels: min x, y, max x, y
    uint4 colours;  // colour 0 and 1 as half4 pairs
    uint4 more;     // colour 2 as a half4 pair; kind and flags; texture slot
    float4 extra;   // depth, then the CPU's own
};

/// One recording drawn: where it is placed, tinted and cut, where its primitives go, its run slots and depth.
struct Placing
{
    float4 basis;   // x axis, y axis
    float4 origin;  // origin xy, scale (for radii and strokes), unused
    float4 tint;
    float4 clip;    // pixels: min x, y, max x, y
    uint4 range;    // first in the pool, count, first out, first texture slot
    uint4 more;     // flags, first pre-pass slot, unused, depth base
};

/// A resident tree's slot: its node (place, fade, the cut it gives its children) and its item's own.
struct TreeNode
{
    float4 basis;       // local x axis, y axis
    float4 origin;      // local origin xy, opacity, unused
    float4 clip;        // its children's cut in its own space: min x, y, max x, y
    float4 itemBasis;
    float4 itemOrigin;
    float4 itemTint;
    float4 itemClip;
    uint4 info;         // parent (in the tree), flags, pool start, count
};

/// A changed slot, by its place in `nodes`.
struct NodePatch
{
    uint4 slot;
    TreeNode node;
};

/// A drawn slot in its tree's order: which slot, where its primitives land in its piece, its run slots, its piece and tree.
struct TreeEntry
{
    uint slot;
    uint out;
    uint slotBase;
    uint piece;
    uint tree;
    uint pad0, pad1, pad2;
};

/// A segment's place in this frame: where its primitives go, its depth and its pre-pass slots.
struct TreePiece
{
    uint outBase;
    uint depthBase;
    uint opaqueBase;
    uint unused;
};

/// A tree this frame: the mount's place, tint and clip, and where its ranges sit in the shared lists.
struct TreeMount
{
    float4 basis;
    float4 origin;  // xy, unused
    float4 tint;
    float4 clip;    // pixels: min x, y, max x, y
    uint4 bases;    // first node, first entry, entries, first instance
    uint4 more;     // first piece, first run slot, drawn this frame, unused
};

[Host] List<Drawn> pool;
[Host] List<Placing> instances;
[Host] List<uint> slots;
List<TreeNode> nodes;
[Host] List<NodePatch> patches;
[Host] List<TreeEntry> entries;
[Host] List<uint> runSlots;
[Host] List<TreePiece> pieces;
[Host] List<TreeMount> trees;

/// The frame's primitives in pixels and draw order, and the pre-pass slots (a plain opaque rect cut to its clip, or a skip).
List<Drawn> placed;
List<Drawn> opaque;

[Default(0)] uniform uint patchCount;
/// Entries in use across every tree; the trees not drawn this frame skip theirs.
[Default(0)] uniform uint entryCount;
[Default(0)] uniform uint treeCount;
/// The CPU's instances and the drawn trees' after them.
[Default(0)] uniform uint instanceCount;

static const uint NoParent = 0xFFFFFFFF;
static const uint NodeClips = 1, NodeItemClips = 2, NodeHidden = 4;
static const uint AllowOpaque = 1, ResidentSlots = 2;
/// Depth steps per primitive: room for four million in a frame before they share a depth (PlacedPrimitives.DepthStep).
static const float DepthStep = 1.0 / (1 << 22);
