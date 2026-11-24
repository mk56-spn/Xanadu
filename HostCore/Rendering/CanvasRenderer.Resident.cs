using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Host.Rendering.Compute;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = Xanadu.Core.Maths.Color;
using Rect = MoonWorks.Graphics.Rect;

namespace Xanadu.Host.Rendering;

/// <summary>A resident tree's GPU side: its buffers, and where it draws this frame.</summary>
internal sealed class ResidentGpu(HostResidentTree tree)
{
    public readonly HostResidentTree Tree = tree;

    public Buffer? Nodes, Entries, RunSlots, Pieces, Patches;
    public uint NodeCapacity, EntryCapacity, RunSlotCapacity, PieceCapacity, PatchCapacity;
    public TransferBuffer? PatchTransfer, EntryTransfer, RunSlotTransfer, PieceTransfer;
    public uint PatchTransferCapacity, EntryTransferCapacity, RunSlotTransferCapacity, PieceTransferCapacity;
    public int PatchCount;

    public Transform2D Transform;
    public Color Modulate;
    public Rect Clip;
    public uint InstanceBase;

    public void Dispose()
    {
        Nodes?.Dispose();
        Entries?.Dispose();
        RunSlots?.Dispose();
        Pieces?.Dispose();
        Patches?.Dispose();
        PatchTransfer?.Dispose();
        EntryTransfer?.Dispose();
        RunSlotTransfer?.Dispose();
        PieceTransfer?.Dispose();
    }
}

/// <summary>
/// Resident trees (Docs/host.md §5.3): a mount's items kept on the GPU,
/// placed by resident_resolve.slang from their records, so the CPU neither
/// walks nor places them. Segments of plain items join the frame as one entry
/// each; foreign items are placed by the CPU as before.
/// </summary>
internal sealed partial class CanvasRenderer
{
    [StructLayout(LayoutKind.Sequential)]
    private struct GpuResidentPatch
    {
        public uint Slot, Pad0, Pad1, Pad2;
        public GpuResidentNode Node;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GpuResidentPiece
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

    private readonly Dictionary<ICanvasMount, ResidentGpu> _residents = new(ReferenceEqualityComparer.Instance);
    private readonly List<ResidentGpu> _residentFrame = [];
    private readonly List<GpuResidentPatch> _patchScratch = [];
    private readonly List<GpuResidentPiece> _pieceScratch = [];
    private ComputePipeline? _residentPatch, _residentResolve;

    private int ResidentInstances
    {
        get
        {
            int total = 0;
            foreach (ResidentGpu gpu in _residentFrame)
                total += gpu.Tree.Entries.Count;
            return total;
        }
    }

    /// <summary>A mount taken down takes its tree with it.</summary>
    private void DropResident(ICanvasMount owner)
    {
        if (!_residents.Remove(owner, out ResidentGpu? gpu))
            return;

        gpu.Tree.ForgetAll();
        _residentFrame.Remove(gpu);
        gpu.Dispose();
    }

    private HostResidentTree ResidentFor(ICanvasMount owner)
    {
        if (!_residents.TryGetValue(owner, out ResidentGpu? gpu))
        {
            var tree = new HostResidentTree(_tree);
            gpu = new ResidentGpu(tree);
            tree.Gpu = gpu;
            _residents.Add(owner, gpu);
        }

        return gpu.Tree;
    }

    /// <summary>Tells each resident tree which of its items changed since the last frame.</summary>
    private void DrainTouched()
    {
        _residentFrame.Clear();
        foreach (CanvasItem item in _tree.Touched)
        {
            item.IsTouched = false;
            item.ResidentOwner?.Touched(item.ResidentSlot);
        }

        _tree.Touched.Clear();
    }

    /// <summary>The tree's pieces into the job: a segment as one entry, a foreign item placed here as the walk placed it.</summary>
    private void DrawResident(Job job, IResidentTree drawn, Transform2D transform, Color modulate, int z, Rect clip)
    {
        if (drawn is not HostResidentTree tree)
            return;

        ResidentGpu gpu = tree.Gpu!;
        HostFramePhases.Phase was = Switch(HostFramePhases.Phase.CollectCanvas);
        if (tree.PlanDirty)
            tree.Plan();

        gpu.Transform = transform;
        gpu.Modulate = modulate;
        gpu.Clip = clip;
        _residentFrame.Add(gpu);
        foreach (ResidentPiece piece in tree.Pieces)
        {
            int pz = Math.Clamp(z + piece.Z, -4096, 4096);
            if (!piece.Foreign)
            {
                job.Entries.Add(new Entry(pz, _sequence++, null!, default, default, default, piece));
                continue;
            }

            if (tree.ItemAt(piece.Slot) is not { Visible: true } item ||
                !tree.Resolve(piece.Slot, out Transform2D world, out float alpha, out Rect2? cut))
                continue;

            Rect within = cut is { } area ? Intersect(clip, Bounds(transform, area)) : clip;
            Place(job, item, transform * world * item.Transform, modulate * new Color(1f, 1f, 1f, alpha) * item.Modulate, pz, within);
        }

        Switch(was);
    }

    /// <summary>
    /// Before the jobs build: each drawn tree's changed recordings into the pool
    /// (all of them after the pool starts again), and its layout worked out again
    /// if a size or texture set moved.
    /// </summary>
    private void PrepareResident()
    {
        foreach (ResidentGpu gpu in _residentFrame)
        {
            HostResidentTree tree = gpu.Tree;
            if (tree.PoolEpoch != _poolEpoch)
            {
                tree.PoolEpoch = _poolEpoch;
                tree.Recorded.Clear();
                for (int slot = 0; slot < tree.SlotCount; slot++)
                    tree.Recorded.Add(slot);
            }

            foreach (int slot in tree.Recorded)
            {
                if (tree.ItemAt(slot) is not { } item)
                    continue;

                Resident(item);
                tree.Placed(slot);
            }

            tree.Recorded.Clear();
            if (tree.LayoutDirty)
                tree.Layout();
        }
    }

    /// <summary>A segment's draws, at this point in the job: its runs as steps, its bases noted for the resolve pass.</summary>
    private uint AddPiece(Job job, ResidentPiece piece, int jobFirst, uint sequence)
    {
        piece.OutBase = (uint)_placedCount;
        piece.DepthBase = sequence;
        piece.OpaqueBase = job.OpaqueStart + (uint)(_placedCount - jobFirst);
        List<ulong> textures = piece.Tree.RunTextures;
        foreach ((uint start, uint count, int firstTexture) in piece.Runs)
        {
            int first = _stepTextures.Count;
            for (int i = 0; i < Slots; i++)
                _stepTextures.Add(textures[firstTexture + i]);
            _steps.Add(new Step(StepKind.Primitives, piece.OutBase + start, count, first));
        }

        _placedCount += (int)piece.Primitives;
        return piece.Primitives;
    }

    /// <summary>Sends each drawn tree's changed records, and its entries and run slots when the layout moved; notes where its instances go.</summary>
    private void UploadResident(CommandBuffer commands)
    {
        if (_residentFrame.Count == 0)
            return;

        CopyPass copy = commands.BeginCopyPass();
        uint instanceBase = (uint)_instances.Count;
        foreach (ResidentGpu gpu in _residentFrame)
        {
            HostResidentTree tree = gpu.Tree;
            gpu.InstanceBase = instanceBase;
            instanceBase += (uint)tree.Entries.Count;

            // A bigger node buffer starts empty: every record goes up again.
            if (gpu.Nodes is null || tree.SlotCount > gpu.NodeCapacity)
            {
                Grow<GpuResidentNode>(_device, ref gpu.Nodes, ref gpu.NodeCapacity, Math.Max(1, tree.SlotCount),
                    BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite, "resident nodes");
                for (int slot = 0; slot < tree.SlotCount; slot++)
                    tree.Set(slot, tree.NodeAt(slot));
            }

            tree.WriteRecords();
            _patchScratch.Clear();
            foreach (int slot in tree.DirtySlots)
                _patchScratch.Add(new GpuResidentPatch { Slot = (uint)slot, Node = tree.Records[slot] });
            tree.DirtySlots.Clear();
            gpu.PatchCount = _patchScratch.Count;
            if (gpu.PatchCount > 0)
            {
                Grow<GpuResidentPatch>(_device, ref gpu.Patches, ref gpu.PatchCapacity, gpu.PatchCount,
                    BufferUsageFlags.ComputeStorageRead, "resident patches");
                Send(copy, _patchScratch, ref gpu.PatchTransfer, ref gpu.PatchTransferCapacity, gpu.Patches!, "resident patch upload");
            }

            if (tree.LayoutChanged)
            {
                tree.LayoutChanged = false;
                Grow<GpuResidentEntry>(_device, ref gpu.Entries, ref gpu.EntryCapacity, Math.Max(1, tree.Entries.Count),
                    BufferUsageFlags.ComputeStorageRead, "resident entries");
                Grow<uint>(_device, ref gpu.RunSlots, ref gpu.RunSlotCapacity, Math.Max(1, tree.RunSlots.Count),
                    BufferUsageFlags.ComputeStorageRead, "resident run slots");
                Send(copy, tree.Entries, ref gpu.EntryTransfer, ref gpu.EntryTransferCapacity, gpu.Entries!, "resident entry upload");
                Send(copy, tree.RunSlots, ref gpu.RunSlotTransfer, ref gpu.RunSlotTransferCapacity, gpu.RunSlots!, "resident run slot upload");
            }

            _pieceScratch.Clear();
            foreach (ResidentPiece piece in tree.Pieces)
                _pieceScratch.Add(new GpuResidentPiece { OutBase = piece.OutBase, DepthBase = piece.DepthBase, OpaqueBase = piece.OpaqueBase });
            Grow<GpuResidentPiece>(_device, ref gpu.Pieces, ref gpu.PieceCapacity, Math.Max(1, _pieceScratch.Count),
                BufferUsageFlags.ComputeStorageRead, "resident pieces");
            Send(copy, _pieceScratch, ref gpu.PieceTransfer, ref gpu.PieceTransferCapacity, gpu.Pieces!, "resident piece upload");
        }

        commands.EndCopyPass(copy);
    }

    private void Send<T>(CopyPass copy, List<T> items, ref TransferBuffer? transfer, ref uint capacity, Buffer target, string name)
        where T : unmanaged
    {
        if (items.Count == 0)
            return;

        TransferBuffer buffer = GrowTransfer<T>(_device, ref transfer, ref capacity, items.Count, name);
        CollectionsMarshal.AsSpan(items).CopyTo(buffer.Map<T>(cycle: true));
        buffer.Unmap();
        copy.UploadToBuffer<T>(buffer, target, 0, 0, (uint)items.Count, cycle: true);
    }

    /// <summary>Applies each drawn tree's patches, then resolves its entries into instances after the CPU's.</summary>
    private void ResolveResident(CommandBuffer commands)
    {
        foreach (ResidentGpu gpu in _residentFrame)
        {
            HostResidentTree tree = gpu.Tree;
            if (gpu.PatchCount > 0)
            {
                _residentPatch ??= Compute("resident_patch.slang", "applyPatches", readOnly: 1);
                ComputePass patch = commands.BeginComputePass([], [new StorageBufferReadWriteBinding(gpu.Nodes!, cycle: false)]);
                patch.BindComputePipeline(_residentPatch);
                patch.BindStorageBuffers(gpu.Patches!);
                commands.PushComputeUniformData(new PatchUniforms { Count = (uint)gpu.PatchCount });
                patch.Dispatch(((uint)gpu.PatchCount + 63) / 64, 1, 1);
                commands.EndComputePass(patch);
            }

            int count = tree.Entries.Count;
            if (count == 0)
                continue;

            _residentResolve ??= Compute("resident_resolve.slang", "resolve", readOnly: 3);
            ComputePass resolve = commands.BeginComputePass([], [new StorageBufferReadWriteBinding(_instanceBuffer!, cycle: false)]);
            resolve.BindComputePipeline(_residentResolve);
            resolve.BindStorageBuffers(gpu.Nodes!, gpu.Entries!, gpu.Pieces!);
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
            resolve.Dispatch(((uint)count + 63) / 64, 1, 1);
            commands.EndComputePass(resolve);
        }
    }

    private ComputePipeline Compute(string file, string entry, uint readOnly) =>
        ComputePipelines.Create(_device,
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

    private void DisposeResident()
    {
        foreach (ResidentGpu gpu in _residents.Values)
            gpu.Dispose();
        _residentPatch?.Dispose();
        _residentResolve?.Dispose();
    }
}
