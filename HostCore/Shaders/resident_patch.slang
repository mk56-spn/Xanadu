// Patches for a mount's tree kept on the GPU (Docs/host.md §5.3). Records stay
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

// ---- Patches ----------------------------------------------------------------

[[vk::binding(0, 0)]] StructuredBuffer<Patch> patches;
[[vk::binding(0, 1)]] RWStructuredBuffer<Node> patchedNodes;
[[vk::binding(0, 2)]] ConstantBuffer<PatchUniforms> patching;

[shader("compute")]
[numthreads(64, 1, 1)]
void applyPatches(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= patching.count)
        return;

    Patch p = patches[id.x];
    patchedNodes[p.slot.x] = p.node;
}

