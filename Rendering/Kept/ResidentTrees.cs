using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using Xanadu.Rendering.Gpu;
using Xanadu.Ui.Systems.Canvas;
using Color = Xanadu.Core.Maths.Color;
using Rect = MoonWorks.Graphics.Rect;

namespace Xanadu.Rendering.Kept;

/// <summary>The mount whose resident tree an entity holds.</summary>
/// <remarks>Indexed as <c>object</c>: Friflo indexes a class or a struct value, and an interface is neither.</remarks>
internal struct ResidentOf(ICanvasMount owner) : IIndexedComponent<object>
{
    public ICanvasMount Owner = owner;

    public readonly object GetIndexedValue() => Owner;
}

/// <summary>A run of one of the canvas stack's shared lists a tree holds: where it starts, and room for how many.</summary>
internal struct StoreRange
{
    public uint Base, Capacity;
}

/// <summary>A resident tree's GPU side: its ranges in the canvas stack's lists, and where it draws this frame.</summary>
internal struct ResidentGpu(HostResidentTree tree) : IComponent
{
    public HostResidentTree Tree = tree;
    public StoreRange Nodes, Entries, RunSlots;

    public Transform2D Transform;
    public Color Modulate;
    public Rect Clip;
    public uint InstanceBase, PieceBase;

    /// <summary>The frame it was last drawn; only trees drawn this frame are sent and resolved.</summary>
    public long DrawnFrame = -1;
}

/// <summary>
/// Resident trees (Docs/host.md §5.3): a mount's items kept on the GPU, placed by the canvas stack's <c>resolve</c>
/// pass from their records, so the CPU neither walks nor places them. A tree is an entity per mount; every tree is
/// a range of the stack's shared lists (nodes, entries, run slots), as recordings are of the pool, so one dispatch
/// resolves them all. Each frame the trees drawn send what changed and resolve into instances after the CPU's.
/// </summary>
internal sealed class ResidentTrees(CanvasTree canvas, EntityStore store)
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Patch
    {
        public uint Slot, Pad0, Pad1, Pad2;
        public GpuResidentNode Node;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Piece
    {
        public uint OutBase, DepthBase, OpaqueBase, Unused;
    }

    /// <summary>The stack's <c>TreeMount</c>: the mount's place, tint and clip, and the tree's ranges.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Mount
    {
        public Vector4 Basis, Origin, Tint, Clip;
        public uint NodeBase, EntryBase, EntryCount, InstanceBase;
        public uint PieceBase, RunSlotBase, Drawn, Unused;
    }

    /// <summary>Wasted room a store may hold, and its share of the store, before every tree takes fresh ranges.</summary>
    private const uint WasteFloor = 1 << 14;

    private readonly ArchetypeQuery<ResidentGpu> _trees = store.Query<ResidentGpu>();
    private readonly List<Patch> _patches = [];
    private readonly List<Piece> _pieces = [];
    private readonly List<Mount> _mounts = [];
    private readonly Stack<uint> _freeIndices = [];
    private uint _nextIndex;
    private Stores _stores;
    private long _frame;

    /// <summary>How far each shared list is taken, and how much of that is no tree's any more.</summary>
    private struct Stores
    {
        public uint Nodes, Entries, RunSlots, Waste;
    }

    /// <summary>A new frame: tells each tree which of its items changed since the last.</summary>
    public void BeginFrame()
    {
        _frame++;
        foreach (CanvasItem item in canvas.Touched)
        {
            item.IsTouched = false;
            item.ResidentOwner?.Touched(item.ResidentSlot);
        }

        canvas.Touched.Clear();
    }

    /// <summary>The mount's tree, made the first time it asks.</summary>
    public HostResidentTree For(ICanvasMount owner)
    {
        Entity entity = Find(owner);
        if (!entity.IsNull)
            return entity.GetComponent<ResidentGpu>().Tree;

        var tree = new HostResidentTree(canvas) { Index = _freeIndices.Count > 0 ? _freeIndices.Pop() : _nextIndex++ };
        tree.Gpu = store.CreateEntity(new ResidentOf(owner), new ResidentGpu(tree));
        return tree;
    }

    /// <summary>A mount taken down takes its tree with it; its ranges are waste until the stores start again.</summary>
    public void Drop(ICanvasMount owner)
    {
        Entity entity = Find(owner);
        if (entity.IsNull)
            return;

        ResidentGpu gpu = entity.GetComponent<ResidentGpu>();
        gpu.Tree.ForgetAll();
        _stores.Waste += gpu.Nodes.Capacity + gpu.Entries.Capacity + gpu.RunSlots.Capacity;
        _freeIndices.Push(gpu.Tree.Index);
        entity.DeleteEntity();
    }

    private Entity Find(ICanvasMount owner)
    {
        Entities found = store.ComponentIndex<ResidentOf, object>()[owner];
        return found.Count > 0 ? found[0] : default;
    }

    /// <summary>The tree is drawn this frame, under this place, tint and clip.</summary>
    public void Drawn(HostResidentTree tree, Transform2D transform, Color modulate, Rect clip)
    {
        ref ResidentGpu gpu = ref tree.Gpu.GetComponent<ResidentGpu>();
        gpu.Transform = transform;
        gpu.Modulate = modulate;
        gpu.Clip = clip;
        gpu.DrawnFrame = _frame;
    }

    /// <summary>The instances this frame's trees resolve into, after the CPU's.</summary>
    public int Instances => _trees.Each(new CountInstances(_frame)).Total;

    /// <summary>
    /// Before the jobs build: each drawn tree's changed recordings into the pool (all of them
    /// after the pool starts again), and its layout worked out again if a size or texture set moved.
    /// </summary>
    public void Prepare(PrimitivePool pool) => _trees.Each(new PrepareTrees(_frame, pool));

    /// <summary>
    /// Hands the canvas stack each drawn tree's changed records (as patches), its entries and run slots when its layout
    /// or range moved, and this frame's pieces and mounts; sets the counts its passes run over.
    /// </summary>
    public void Send(CanvasMaterial canvasStack, int cpuInstances)
    {
        // Mostly waste: every tree takes a fresh range and sends everything again.
        uint taken = _stores.Nodes + _stores.Entries + _stores.RunSlots;
        if (_stores.Waste > WasteFloor && _stores.Waste * 2 > taken)
        {
            _stores = default;
            _trees.Each(new ForgetRanges());
        }

        HostLists lists = canvasStack.Lists;
        _patches.Clear();
        _pieces.Clear();
        _mounts.Clear();
        CollectionsMarshal.SetCount(_mounts, (int)_nextIndex);
        CollectionsMarshal.AsSpan(_mounts).Clear();
        var send = new SendTrees(_frame, lists, (uint)cpuInstances, _patches, _pieces, _mounts, _stores);
        _stores = _trees.Each(send).Stores;

        lists.Size("nodes", (int)_stores.Nodes);
        lists.Size("entries", (int)_stores.Entries);
        lists.Size("runSlots", (int)_stores.RunSlots);
        Set(lists, "patches", CollectionsMarshal.AsSpan(_patches));
        Set(lists, "pieces", CollectionsMarshal.AsSpan(_pieces));
        Set(lists, "trees", CollectionsMarshal.AsSpan(_mounts));
        canvasStack.Set("patchCount", _patches.Count);
        canvasStack.Set("entryCount", (int)_stores.Entries);
        canvasStack.Set("treeCount", _mounts.Count);
    }

    /// <summary>A list's data set whole, and its length to match.</summary>
    private static void Set<T>(HostLists lists, string name, ReadOnlySpan<T> data) where T : unmanaged
    {
        lists.Size(name, data.Length);
        lists.Set(name, data);
    }

    /// <summary>A range with room for <paramref name="needed"/>, at the store's end when it has too little; true when it moved.</summary>
    private static bool Fit(ref StoreRange range, int needed, ref uint end, ref uint waste)
    {
        if (needed <= range.Capacity && range.Capacity > 0)
            return false;
        waste += range.Capacity;
        range.Capacity = BitOperations.RoundUpToPowerOf2((uint)Math.Max(needed, 16));
        range.Base = end;
        end += range.Capacity;
        return true;
    }

    private struct CountInstances(long frame) : IEach<ResidentGpu>
    {
        public int Total;

        public void Execute(ref ResidentGpu gpu)
        {
            if (gpu.DrawnFrame == frame)
                Total += gpu.Tree.Entries.Count;
        }
    }

    private struct ForgetRanges : IEach<ResidentGpu>
    {
        public void Execute(ref ResidentGpu gpu)
        {
            gpu.Nodes = default;
            gpu.Entries = default;
            gpu.RunSlots = default;
        }
    }

    private readonly struct PrepareTrees(long frame, PrimitivePool pool) : IEach<ResidentGpu>
    {
        public void Execute(ref ResidentGpu gpu)
        {
            if (gpu.DrawnFrame != frame)
                return;

            HostResidentTree tree = gpu.Tree;
            if (tree.PoolEpoch != pool.Epoch)
            {
                tree.PoolEpoch = pool.Epoch;
                tree.Recorded.Clear();
                for (int slot = 0; slot < tree.SlotCount; slot++)
                    tree.Recorded.Add(slot);
            }

            foreach (int slot in tree.Recorded)
            {
                if (tree.ItemAt(slot) is not { } item)
                    continue;

                pool.Send(item);
                tree.Placed(slot);
            }

            tree.Recorded.Clear();
            if (tree.LayoutDirty)
                tree.Layout();
        }
    }

    private struct SendTrees(long frame, HostLists lists, uint instanceBase, List<Patch> patches, List<Piece> pieces, List<Mount> mounts,
        Stores stores) : IEach<ResidentGpu>
    {
        public Stores Stores = stores;

        public void Execute(ref ResidentGpu gpu)
        {
            if (gpu.DrawnFrame != frame)
                return;

            HostResidentTree tree = gpu.Tree;
            gpu.InstanceBase = instanceBase;
            instanceBase += (uint)tree.Entries.Count;

            // A new node range starts empty: every record goes up again.
            if (Fit(ref gpu.Nodes, tree.SlotCount, ref Stores.Nodes, ref Stores.Waste))
                for (int slot = 0; slot < tree.SlotCount; slot++)
                    tree.Set(slot, tree.NodeAt(slot));

            tree.WriteRecords();
            foreach (int slot in tree.DirtySlots)
                patches.Add(new Patch { Slot = gpu.Nodes.Base + (uint)slot, Node = tree.Records[slot] });
            tree.DirtySlots.Clear();

            bool entriesMoved = Fit(ref gpu.Entries, tree.Entries.Count, ref Stores.Entries, ref Stores.Waste);
            bool slotsMoved = Fit(ref gpu.RunSlots, tree.RunSlots.Count, ref Stores.RunSlots, ref Stores.Waste);
            if (tree.LayoutChanged || entriesMoved || slotsMoved)
            {
                tree.LayoutChanged = false;
                lists.Write("entries", (int)gpu.Entries.Base, CollectionsMarshal.AsSpan(tree.Entries));
                lists.Write("runSlots", (int)gpu.RunSlots.Base, CollectionsMarshal.AsSpan(tree.RunSlots));
            }

            gpu.PieceBase = (uint)pieces.Count;
            foreach (ResidentPiece piece in tree.Pieces)
                pieces.Add(new Piece { OutBase = piece.OutBase, DepthBase = piece.DepthBase, OpaqueBase = piece.OpaqueBase });

            Transform2D t = gpu.Transform;
            Color m = gpu.Modulate;
            Rect c = gpu.Clip;
            mounts[(int)tree.Index] = new Mount
            {
                Basis = new Vector4(t.X.X, t.X.Y, t.Y.X, t.Y.Y),
                Origin = new Vector4(t.Origin.X, t.Origin.Y, 0, 0),
                Tint = new Vector4(m.R, m.G, m.B, m.A),
                Clip = new Vector4(c.X, c.Y, c.X + c.W, c.Y + c.H),
                NodeBase = gpu.Nodes.Base,
                EntryBase = gpu.Entries.Base,
                EntryCount = (uint)tree.Entries.Count,
                InstanceBase = gpu.InstanceBase,
                PieceBase = gpu.PieceBase,
                RunSlotBase = gpu.RunSlots.Base,
                Drawn = 1,
            };
        }
    }
}
