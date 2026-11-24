using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Host.Rendering;

/// <summary>One slot's record on the GPU (resident_tree.slang's <c>Node</c>): its node, its item's own place and look, and its pool range.</summary>
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

/// <summary>One drawn slot in draw order: which slot, where its primitives land in its piece, its run slots and its piece.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GpuResidentEntry
{
    public uint Slot, Out, SlotBase, Piece;
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

/// <summary>
/// A mount's tree kept on the GPU (Docs/host.md §5.3): what the mount
/// sent, the items behind its slots, and what follows from them, worked out
/// again only when something changed: the plan (segments and foreign pieces,
/// when the order or an item's kind moves) and the layout (each entry's place
/// and texture slots, when a recording's size or textures move).
/// </summary>
internal sealed class HostResidentTree(CanvasTree canvas) : IResidentTree
{
    public const int Slots = 8;

    private CanvasItem?[] _items = [];
    private ResidentNode[] _nodes = [];
    private bool[] _foreign = [];
    private bool[] _dirty = [];
    private readonly Stack<int> _free = [];
    private int _slotCount;

    /// <summary>Slots whose record changed since the GPU last heard.</summary>
    public readonly List<int> DirtySlots = [];

    public GpuResidentNode[] Records = [];

    private int[] _order = [], _z = [];
    private int _orderCount;

    public readonly List<ResidentPiece> Pieces = [];
    public readonly List<int> Sorted = [];

    /// <summary>One per <see cref="Sorted"/> slot, and the texture slots and run textures they point into.</summary>
    public readonly List<GpuResidentEntry> Entries = [];
    public readonly List<uint> RunSlots = [];
    public readonly List<ulong> RunTextures = [];

    /// <summary>Slots whose recording may have changed: their pool range is checked before the frame builds.</summary>
    public readonly List<int> Recorded = [];

    public bool PlanDirty = true, LayoutDirty = true;

    /// <summary>Set when the layout was worked out again this frame, so it goes up.</summary>
    public bool LayoutChanged;

    public int PoolEpoch = -1;

    public int SlotCount => _slotCount;

    /// <summary>Its buffers and frame place, kept by the renderer.</summary>
    public ResidentGpu? Gpu;

    public CanvasItem? ItemAt(int slot) => _items[slot];

    public ResidentNode NodeAt(int slot) => _nodes[slot];

    public int Add(Handle handle)
    {
        int slot = _free.Count > 0 ? _free.Pop() : _slotCount++;
        Grow(slot + 1);
        CanvasItem? item = canvas.Item(handle);
        if (item is { ResidentOwner: { } other })
            other.Forget(item.ResidentSlot);
        _items[slot] = item;
        if (item is not null)
        {
            item.ResidentOwner = this;
            item.ResidentSlot = slot;
        }

        _nodes[slot] = default;
        _textures.Remove(slot);
        _foreign[slot] = item is not null && IsForeign(item);
        Recorded.Add(slot);
        PlanDirty = true;
        return slot;
    }

    public void Remove(int slot)
    {
        Forget(slot);
        _free.Push(slot);
        PlanDirty = true;
    }

    /// <summary>Lets go of a slot's item (removed, or the item freed): it draws nothing from now on.</summary>
    public void Forget(int slot)
    {
        if (_items[slot] is { } item && item.ResidentOwner == this)
        {
            item.ResidentOwner = null;
            item.ResidentSlot = -1;
        }

        _items[slot] = null;
        PlanDirty = true;
    }

    /// <summary>Lets go of every item, when the mount goes.</summary>
    public void ForgetAll()
    {
        for (int slot = 0; slot < _slotCount; slot++)
            Forget(slot);
    }

    public void Set(int slot, in ResidentNode node)
    {
        _nodes[slot] = node;
        MarkRecord(slot);
    }

    public void SetOrder(ReadOnlySpan<int> slots, ReadOnlySpan<int> z)
    {
        if (_order.Length < slots.Length)
        {
            _order = new int[Math.Max(slots.Length, _order.Length * 2)];
            _z = new int[_order.Length];
        }

        slots.CopyTo(_order);
        z.CopyTo(_z);
        _orderCount = slots.Length;
        PlanDirty = true;
    }

    /// <summary>The item changed its own place, look, kind or recording: its record is written again and its kind checked.</summary>
    public void Touched(int slot)
    {
        if (_items[slot] is not { } item)
            return;

        bool foreign = IsForeign(item);
        if (foreign != _foreign[slot])
        {
            _foreign[slot] = foreign;
            PlanDirty = true;
        }

        Recorded.Add(slot);
        MarkRecord(slot);
    }

    /// <summary>After the renderer made the item's pool range current: the record takes it, and a new size or texture set moves the layout.</summary>
    public void Placed(int slot)
    {
        if (_items[slot] is not { } item)
            return;

        ref GpuResidentNode record = ref Records[slot];
        if (record.Count != (uint)item.Primitives.Count || !SameTextures(slot, item))
            LayoutDirty = true;
        MarkRecord(slot);
    }

    private readonly Dictionary<int, ulong[]> _textures = [];

    private bool SameTextures(int slot, CanvasItem item)
    {
        if (_textures.TryGetValue(slot, out ulong[]? known) && known.AsSpan().SequenceEqual(CollectionsMarshal.AsSpan(item.LocalTextures)))
            return true;

        _textures[slot] = [.. item.LocalTextures];
        return false;
    }

    private void MarkRecord(int slot)
    {
        if (_dirty[slot])
            return;

        _dirty[slot] = true;
        DirtySlots.Add(slot);
    }

    /// <summary>Writes the dirty slots' records from their nodes and items, and clears the marks.</summary>
    public void WriteRecords()
    {
        foreach (int slot in DirtySlots)
        {
            _dirty[slot] = false;
            ResidentNode node = _nodes[slot];
            CanvasItem? item = _items[slot];
            ref GpuResidentNode r = ref Records[slot];
            r.Basis = new Vector4(node.Local.X.X, node.Local.X.Y, node.Local.Y.X, node.Local.Y.Y);
            r.Origin = new Vector4(node.Local.Origin.X, node.Local.Origin.Y, node.Opacity, 0);
            r.Clip = new Vector4(node.Clip.Position.X, node.Clip.Position.Y, node.Clip.End.X, node.Clip.End.Y);
            r.Parent = node.Parent < 0 ? GpuResidentNode.NoParent : (uint)node.Parent;
            r.Flags = node.Clips ? GpuResidentNode.Clips : 0;
            if (item is null)
            {
                r.Flags |= GpuResidentNode.Hidden;
                r.Count = 0;
                continue;
            }

            Transform2D t = item.Transform;
            Color tint = item.Modulate * item.SelfModulate;
            r.ItemBasis = new Vector4(t.X.X, t.X.Y, t.Y.X, t.Y.Y);
            r.ItemOrigin = new Vector4(t.Origin.X, t.Origin.Y, 0, 0);
            r.ItemTint = new Vector4(tint.R, tint.G, tint.B, tint.A);
            r.ItemClip = new Vector4(item.CustomRect.Position.X, item.CustomRect.Position.Y, item.CustomRect.End.X, item.CustomRect.End.Y);
            if (item.Clip && item.UseCustomRect)
                r.Flags |= GpuResidentNode.ItemClips;
            if (!item.Visible)
                r.Flags |= GpuResidentNode.Hidden;
            r.PoolStart = (uint)Math.Max(0, item.PoolStart);
            r.Count = (uint)item.Primitives.Count;
        }
    }

    /// <summary>An item the GPU records can't draw: the CPU places it, its children and mounts as before.</summary>
    private static bool IsForeign(CanvasItem item) =>
        item.Material is not null || item.Runs.Count > 0 || item.Children.Count > 0 || item.Mounts is { Count: > 0 } ||
        item.Particles is not null || item.ParticleMesh;

    // ---- Plan and layout ----------------------------------------------------

    private readonly List<(int Z, int Index, int Slot)> _interval = [];

    /// <summary>
    /// Cuts the order into pieces: between foreign items, the plain ones grouped
    /// by z (in walk order within each), so sorting the frame's entries by z
    /// draws exactly as the walk did.
    /// </summary>
    public void Plan()
    {
        PlanDirty = false;
        LayoutDirty = true;
        Pieces.Clear();
        Sorted.Clear();
        _interval.Clear();
        for (int i = 0; i < _orderCount; i++)
        {
            int slot = _order[i];
            if (_items[slot] is null)
                continue;

            if (!_foreign[slot])
            {
                _interval.Add((_z[i], i, slot));
                continue;
            }

            Flush();
            Pieces.Add(new ResidentPiece { Tree = this, Foreign = true, Z = _z[i], Slot = slot, Index = Pieces.Count });
        }

        Flush();
    }

    private void Flush()
    {
        if (_interval.Count == 0)
            return;

        _interval.Sort(static (a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.Index.CompareTo(b.Index));
        int from = 0;
        while (from < _interval.Count)
        {
            int z = _interval[from].Z, to = from;
            var piece = new ResidentPiece { Tree = this, Z = z, Start = Sorted.Count, Index = Pieces.Count };
            while (to < _interval.Count && _interval[to].Z == z)
                Sorted.Add(_interval[to++].Slot);
            piece.Count = to - from;
            Pieces.Add(piece);
            from = to;
        }

        _interval.Clear();
    }

    /// <summary>
    /// Each segment's entries: where every item's primitives land in the piece,
    /// and its textures in runs of up to <see cref="Slots"/>, as the CPU path cuts them.
    /// </summary>
    public void Layout()
    {
        LayoutDirty = false;
        LayoutChanged = true;
        Entries.Clear();
        RunSlots.Clear();
        RunTextures.Clear();
        foreach (ResidentPiece piece in Pieces)
        {
            piece.Runs.Clear();
            piece.Primitives = 0;
            if (piece.Foreign)
                continue;

            uint at = 0, runStart = 0;
            int runFirst = -1, runUsed = 0;
            for (int i = piece.Start; i < piece.Start + piece.Count; i++)
            {
                int slot = Sorted[i];
                CanvasItem item = _items[slot]!;
                List<ulong> textures = item.LocalTextures;
                if (runFirst < 0 || !Fits(runFirst, runUsed, textures))
                {
                    if (runFirst >= 0 && at > runStart)
                        piece.Runs.Add((runStart, at - runStart, runFirst));
                    runStart = at;
                    runFirst = RunTextures.Count;
                    runUsed = 0;
                    for (int s = 0; s < Slots; s++)
                        RunTextures.Add(0);
                }

                int slotBase = RunSlots.Count;
                foreach (ulong texture in textures)
                    RunSlots.Add((uint)Math.Max(0, SlotIn(runFirst, ref runUsed, texture)));
                Entries.Add(new GpuResidentEntry { Slot = (uint)slot, Out = at, SlotBase = (uint)slotBase, Piece = (uint)piece.Index });
                at += (uint)item.Primitives.Count;
            }

            if (runFirst >= 0 && at > runStart)
                piece.Runs.Add((runStart, at - runStart, runFirst));
            piece.Primitives = at;
        }
    }

    private bool Fits(int first, int used, List<ulong> textures)
    {
        int extra = 0;
        foreach (ulong texture in textures)
        {
            if (RunTextures.IndexOf(texture, first, used) < 0)
                extra++;
        }

        return used + extra <= Slots;
    }

    private int SlotIn(int first, ref int used, ulong texture)
    {
        int at = RunTextures.IndexOf(texture, first, used);
        if (at >= 0)
            return at - first;
        if (used == Slots)
            return -1;

        RunTextures[first + used] = texture;
        return used++;
    }

    /// <summary>A foreign slot's place, fade and cut in the mount's space, worked out on the CPU from its ancestors' nodes; false when nothing of it shows.</summary>
    public bool Resolve(int slot, out Transform2D world, out float alpha, out Rect2? clip)
    {
        world = _nodes[slot].Local;
        alpha = _nodes[slot].Opacity;
        clip = null;
        Span<int> chain = stackalloc int[64];
        int depth = 0;
        for (int p = _nodes[slot].Parent; p >= 0 && depth < chain.Length; p = _nodes[p].Parent)
            chain[depth++] = p;

        Transform2D above = Transform2D.Identity;
        float fade = 1f;
        for (int k = depth - 1; k >= 0; k--)
        {
            ResidentNode node = _nodes[chain[k]];
            above *= node.Local;
            fade *= node.Opacity;
            if (!node.Clips)
                continue;

            Rect2 own = above * node.Clip;
            clip = clip is { } outer ? outer.Intersection(own) : own;
        }

        world = above * world;
        alpha *= fade;
        return alpha > 0f;
    }

    private void Grow(int size)
    {
        if (_items.Length >= size)
            return;

        int next = Math.Max(size, Math.Max(64, _items.Length * 2));
        Array.Resize(ref _items, next);
        Array.Resize(ref _nodes, next);
        Array.Resize(ref _foreign, next);
        Array.Resize(ref _dirty, next);
        Array.Resize(ref Records, next);
    }
}
