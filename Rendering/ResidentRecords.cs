using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Xanadu.Rendering;

/// <summary>One slot's record on the GPU (the canvas stack's <c>TreeNode</c>): its node, its item's own place and look, and its pool range.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GpuResidentNode
{
    public Vector4 Basis;
    public Vector4 Origin;      // xy, opacity, unused
    public Vector4 Clip;        // its children's cut in its own space: min x, y, max x, y
    public Vector4 ItemBasis;
    public Vector4 ItemOrigin;
    public Vector4 ItemTint;    // the item's modulate times its self modulate
    public Vector4 ItemClip;    // the item's own cut in its space
    public uint Parent, Flags, PoolStart, Count;

    public const uint Clips = 1, ItemClips = 2, Hidden = 4;
    public const uint NoParent = 0xFFFFFFFF;
}

/// <summary>One drawn slot in draw order (the canvas stack's <c>TreeEntry</c>): which slot, where its primitives land in its piece, its run slots, its piece and its tree.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GpuResidentEntry
{
    public uint Slot, Out, SlotBase, Piece, Tree, Pad0, Pad1, Pad2;
}

/// <summary>
/// A run of a resident tree's draw order the frame treats as one: a
/// <b>segment</b> of plain items sharing a z, drawn from the GPU records, or a
/// <b>foreign</b> item the CPU places itself (one with a material, shaded runs,
/// children, mounts or particles).
/// </summary>
internal sealed class ResidentPiece
{
    public required HostResidentTree Tree;
    public bool Foreign;
    public int Z;

    /// <summary>A segment's range in <see cref="HostResidentTree.Sorted"/>; a foreign piece's slot.</summary>
    public int Start, Count, Slot;

    public int Index;
    public uint Primitives;

    /// <summary>A segment's draws: each a start and count within the piece and its first texture in the tree's run textures.</summary>
    public readonly List<(uint Start, uint Count, int FirstTexture)> Runs = [];

    /// <summary>This frame's place in the job's output, depth and pre-pass list.</summary>
    public uint OutBase, DepthBase, OpaqueBase;
}
