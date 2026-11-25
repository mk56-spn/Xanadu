using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using MoonWorks.Graphics;
using Xanadu.Host.Rendering.Compute;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = Xanadu.Core.Maths.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;
using Rect = MoonWorks.Graphics.Rect;

namespace Xanadu.Host.Rendering;

/// <summary>The mount whose resident tree an entity holds.</summary>
/// <remarks>Indexed as <c>object</c>: Friflo indexes a class or a struct value, and an interface is neither.</remarks>
internal struct ResidentOf(ICanvasMount owner) : IIndexedComponent<object>
{
    public ICanvasMount Owner = owner;

    public readonly object GetIndexedValue() => Owner;
}

/// <summary>A resident tree's GPU side: its buffers, and where it draws this frame.</summary>
internal struct ResidentGpu(HostResidentTree tree, GraphicsDevice device) : IComponent
{
    private const BufferUsageFlags Read = BufferUsageFlags.ComputeStorageRead;

    public HostResidentTree Tree = tree;
    public GpuArray<GpuResidentNode> Nodes = new(device, "resident nodes", Read | BufferUsageFlags.ComputeStorageWrite);
    public GpuArray<GpuResidentEntry> Entries = new(device, "resident entries", Read);
    public GpuArray<uint> RunSlots = new(device, "resident run slots", Read);
    public GpuArray<ResidentTrees.Piece> Pieces = new(device, "resident pieces", Read);
    public GpuArray<ResidentTrees.Patch> Patches = new(device, "resident patches", Read);
    public int PatchCount;

    public Transform2D Transform;
    public Color Modulate;
    public Rect Clip;
    public uint InstanceBase;

    /// <summary>The frame it was last drawn; only trees drawn this frame are sent, resolved and expanded.</summary>
    public long DrawnFrame = -1;

    public readonly void Dispose()
    {
        Nodes.Dispose();
        Entries.Dispose();
        RunSlots.Dispose();
        Pieces.Dispose();
        Patches.Dispose();
    }
}

/// <summary>
/// Resident trees (Docs/host.md §5.3): a mount's items kept on the GPU, placed by
/// resident_resolve.slang from their records, so the CPU neither walks nor places them. A tree
/// is an entity per mount; each frame the ones drawn send what changed, then resolve into
/// instances after the CPU's.
/// </summary>
internal sealed class ResidentTrees(GraphicsDevice device, CanvasTree canvas, EntityStore store) : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Patch
    {
        public uint Slot, Pad0, Pad1, Pad2;
        public GpuResidentNode Node;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Piece
    {
        public uint OutBase, DepthBase, OpaqueBase, Unused;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PatchUniforms
    {
        public uint Count, Pad0, Pad1, Pad2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ResolveUniforms
    {
        public Vector4 MountBasis, MountOrigin, MountTint, MountClip;
        public uint Count, InstanceBase, Pad0, Pad1;
    }

    private readonly ArchetypeQuery<ResidentGpu> _trees = store.Query<ResidentGpu>();
    private readonly List<Patch> _patches = [];
    private readonly List<Piece> _pieces = [];
    private ComputePipeline? _patch, _resolve;
    private long _frame;

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

        var tree = new HostResidentTree(canvas);
        tree.Gpu = store.CreateEntity(new ResidentOf(owner), new ResidentGpu(tree, device));
        return tree;
    }

    /// <summary>A mount taken down takes its tree with it.</summary>
    public void Drop(ICanvasMount owner)
    {
        Entity entity = Find(owner);
        if (entity.IsNull)
            return;

        ResidentGpu gpu = entity.GetComponent<ResidentGpu>();
        gpu.Tree.ForgetAll();
        gpu.Dispose();
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

    /// <summary>Sends each drawn tree's changed records, and its entries and run slots when the layout moved; notes where its instances go.</summary>
    public void Upload(CommandBuffer commands, int cpuInstances)
    {
        if (!AnyDrawn)
            return;

        CopyPass copy = commands.BeginCopyPass();
        _trees.Each(new SendTrees(_frame, copy, (uint)cpuInstances, _patches, _pieces));
        commands.EndCopyPass(copy);
    }

    private bool AnyDrawn => _trees.Each(new CountInstances(_frame)).Drawn > 0;

    /// <summary>Applies each drawn tree's patches, then resolves its entries into instances after the CPU's.</summary>
    public void Resolve(CommandBuffer commands, Buffer instances)
    {
        _patch ??= Compute("resident_patch.slang", "applyPatches", readOnly: 1);
        _resolve ??= Compute("resident_resolve.slang", "resolve", readOnly: 3);
        _trees.Each(new ResolveTrees(_frame, commands, instances, _patch, _resolve));
    }

    /// <summary>Each drawn tree's instances expanded in the open pass, reading its own run slots.</summary>
    public void Expand(CommandBuffer commands, ComputePass pass, Buffer pool, Buffer instances, Buffer slots, float depthStep) =>
        _trees.Each(new ExpandTrees(_frame, commands, pass, pool, instances, slots, depthStep));

    private ComputePipeline Compute(string file, string entry, uint readOnly) =>
        ComputePipelines.Create(device,
            SlangShaders.Compile(Path.Combine(AppContext.BaseDirectory, "Shaders", file), entry, "compute"),
            entry, new ComputePipelineCreateInfo
            {
                Format = ShaderFormat.SPIRV,
                NumReadonlyStorageBuffers = readOnly,
                NumReadWriteStorageBuffers = 1,
                NumUniformBuffers = 1,
                ThreadCountX = 64,
                ThreadCountY = 1,
                ThreadCountZ = 1,
            });

    public void Dispose()
    {
        _trees.Each(new DisposeTrees());
        _patch?.Dispose();
        _resolve?.Dispose();
    }

    private struct CountInstances(long frame) : IEach<ResidentGpu>
    {
        public int Total, Drawn;

        public void Execute(ref ResidentGpu gpu)
        {
            if (gpu.DrawnFrame != frame)
                return;
            Drawn++;
            Total += gpu.Tree.Entries.Count;
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

    private struct SendTrees(long frame, CopyPass copy, uint instanceBase, List<Patch> patches, List<Piece> pieces) : IEach<ResidentGpu>
    {
        public void Execute(ref ResidentGpu gpu)
        {
            if (gpu.DrawnFrame != frame)
                return;

            HostResidentTree tree = gpu.Tree;
            gpu.InstanceBase = instanceBase;
            instanceBase += (uint)tree.Entries.Count;

            // A bigger node buffer starts empty: every record goes up again.
            if (gpu.Nodes.Buffer is null || tree.SlotCount > gpu.Nodes.Capacity)
            {
                gpu.Nodes.Ensure(tree.SlotCount);
                for (int slot = 0; slot < tree.SlotCount; slot++)
                    tree.Set(slot, tree.NodeAt(slot));
            }

            tree.WriteRecords();
            patches.Clear();
            foreach (int slot in tree.DirtySlots)
                patches.Add(new Patch { Slot = (uint)slot, Node = tree.Records[slot] });
            tree.DirtySlots.Clear();
            gpu.PatchCount = patches.Count;
            gpu.Patches.Upload(copy, CollectionsMarshal.AsSpan(patches));

            if (tree.LayoutChanged)
            {
                tree.LayoutChanged = false;
                gpu.Entries.Ensure(tree.Entries.Count);
                gpu.RunSlots.Ensure(tree.RunSlots.Count);
                gpu.Entries.Upload(copy, CollectionsMarshal.AsSpan(tree.Entries));
                gpu.RunSlots.Upload(copy, CollectionsMarshal.AsSpan(tree.RunSlots));
            }

            pieces.Clear();
            foreach (ResidentPiece piece in tree.Pieces)
                pieces.Add(new Piece { OutBase = piece.OutBase, DepthBase = piece.DepthBase, OpaqueBase = piece.OpaqueBase });
            gpu.Pieces.Ensure(pieces.Count);
            gpu.Pieces.Upload(copy, CollectionsMarshal.AsSpan(pieces));
        }
    }

    private readonly struct ResolveTrees(long frame, CommandBuffer commands, Buffer instances, ComputePipeline patch, ComputePipeline resolve)
        : IEach<ResidentGpu>
    {
        public void Execute(ref ResidentGpu gpu)
        {
            if (gpu.DrawnFrame != frame)
                return;

            if (gpu.PatchCount > 0)
            {
                ComputePass patching = commands.BeginComputePass([], [new StorageBufferReadWriteBinding(gpu.Nodes.Buffer!, cycle: false)]);
                patching.BindComputePipeline(patch);
                patching.BindStorageBuffers(gpu.Patches.Buffer!);
                commands.PushComputeUniformData(new PatchUniforms { Count = (uint)gpu.PatchCount });
                patching.Dispatch(((uint)gpu.PatchCount + 63) / 64, 1, 1);
                commands.EndComputePass(patching);
            }

            int count = gpu.Tree.Entries.Count;
            if (count == 0)
                return;

            ComputePass resolving = commands.BeginComputePass([], [new StorageBufferReadWriteBinding(instances, cycle: false)]);
            resolving.BindComputePipeline(resolve);
            resolving.BindStorageBuffers(gpu.Nodes.Buffer!, gpu.Entries.Buffer!, gpu.Pieces.Buffer!);
            Transform2D t = gpu.Transform;
            Color m = gpu.Modulate;
            Rect c = gpu.Clip;
            commands.PushComputeUniformData(new ResolveUniforms
            {
                MountBasis = new Vector4(t.X.X, t.X.Y, t.Y.X, t.Y.Y),
                MountOrigin = new Vector4(t.Origin.X, t.Origin.Y, 0, 0),
                MountTint = new Vector4(m.R, m.G, m.B, m.A),
                MountClip = new Vector4(c.X, c.Y, c.X + c.W, c.Y + c.H),
                Count = (uint)count,
                InstanceBase = gpu.InstanceBase,
            });
            resolving.Dispatch(((uint)count + 63) / 64, 1, 1);
            commands.EndComputePass(resolving);
        }
    }

    private readonly struct ExpandTrees(long frame, CommandBuffer commands, ComputePass pass, Buffer pool, Buffer instances, Buffer slots,
        float depthStep) : IEach<ResidentGpu>
    {
        public void Execute(ref ResidentGpu gpu)
        {
            uint count = (uint)gpu.Tree.Entries.Count;
            if (gpu.DrawnFrame != frame || count == 0)
                return;

            pass.BindStorageBuffers(pool, instances, slots, gpu.RunSlots.Buffer!);
            commands.PushComputeUniformData(new ExpandUniforms { Instances = count, DepthStep = depthStep, First = gpu.InstanceBase });
            pass.Dispatch(count, 1, 1);
        }
    }

    private struct DisposeTrees : IEach<ResidentGpu>
    {
        public void Execute(ref ResidentGpu gpu) => gpu.Dispose();
    }
}
