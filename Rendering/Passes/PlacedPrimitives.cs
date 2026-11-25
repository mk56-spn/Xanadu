using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Rendering.Compute;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Rendering.Passes;

/// <summary>primitive_expand.slang's uniforms: the instances to place, the depth a primitive steps, the first instance.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ExpandUniforms
{
    public uint Instances;
    public float DepthStep;
    public uint First, Pad1;
}

/// <summary>
/// The frame's placed primitives (primitive_expand.slang): one instance per recording drawn,
/// with its run slots, expanded on the GPU into the primitives in draw order and the pre-pass
/// list, one slot per placed primitive holding its opaque cut or a skip. Fixed slots rather
/// than an atomic count and an indirect draw: the indirect args raced the compute pass (the
/// pre-pass drew nothing on most frames), and a skip costs the vertex stage next to nothing.
/// </summary>
internal sealed class PlacedPrimitives : IDisposable
{
    /// <summary>Depth steps per primitive: room for four million in a frame before they share a depth.</summary>
    public const float DepthStep = 1f / (1 << 22);

    /// <summary>An instance's flag: its plain opaque rects may join the pre-pass.</summary>
    public const uint AllowOpaque = 1;

    private readonly GpuArray<GpuInstance> _instances;
    private readonly GpuArray<uint> _slots;
    private readonly GpuArray<GpuPrimitive> _placed, _opaque;
    private readonly ComputePipeline _expand;

    /// <summary>One per recording drawn this frame, and each one's run slots.</summary>
    public readonly List<GpuInstance> Instances = [];

    public readonly List<uint> Slots = [];

    /// <summary>Primitives placed this frame, and pre-pass slots kept for them.</summary>
    public int PlacedCount, OpaqueReserved;

    public PlacedPrimitives(GraphicsDevice device)
    {
        _instances = new(device, "primitive instances", BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite);
        _slots = new(device, "texture slots", BufferUsageFlags.ComputeStorageRead);
        _placed = new(device, "placed primitives", BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead);
        _opaque = new(device, "opaque primitives", BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead);
        _expand = ComputePipelines.Create(device,
            SlangShaders.Compile(Path.Combine(AppContext.BaseDirectory, "Shaders", "primitive_expand.slang"), "expandPrimitives", "compute"),
            "expandPrimitives", new ComputePipelineCreateInfo
            {
                Format = ShaderFormat.SPIRV,
                NumReadonlyStorageBuffers = 4,
                NumReadWriteStorageBuffers = 2,
                NumUniformBuffers = 1,
                ThreadCountX = 64,
                ThreadCountY = 1,
                ThreadCountZ = 1,
            });
    }

    /// <summary>The placed primitives, in draw order, as the draws read them.</summary>
    public Buffer Placed => _placed.Buffer!;

    /// <summary>The pre-pass list.</summary>
    public Buffer Opaque => _opaque.Buffer!;

    /// <summary>Forgets this frame's instances: the build starts again.</summary>
    public void Clear()
    {
        Instances.Clear();
        Slots.Clear();
        PlacedCount = 0;
        OpaqueReserved = 0;
    }

    /// <summary>Makes the buffers this frame needs and sends the CPU's instances and slots; resident trees' instances follow them.</summary>
    public void Upload(CopyPass copy, int residentInstances)
    {
        _placed.Ensure(PlacedCount);
        _opaque.Ensure(OpaqueReserved);
        _instances.Ensure(Instances.Count + residentInstances);
        _slots.Ensure(Slots.Count);
        _instances.Upload(copy, CollectionsMarshal.AsSpan(Instances));
        _slots.Upload(copy, CollectionsMarshal.AsSpan(Slots));
    }

    /// <summary>Places every instance's primitives for the frame: the CPU's, then each resident tree's after its resolve.</summary>
    public void Expand(CommandBuffer commands, PrimitivePool pool, ResidentTrees residents)
    {
        if (Instances.Count == 0 && residents.Instances == 0)
            return;

        residents.Resolve(commands, _instances.Buffer!);
        ComputePass pass = commands.BeginComputePass([],
        [
            new StorageBufferReadWriteBinding(Placed, cycle: true),
            new StorageBufferReadWriteBinding(Opaque, cycle: true),
        ]);
        pass.BindComputePipeline(_expand);
        if (Instances.Count > 0)
        {
            pass.BindStorageBuffers(pool.Buffer!, _instances.Buffer!, _slots.Buffer!, _slots.Buffer!);
            commands.PushComputeUniformData(new ExpandUniforms { Instances = (uint)Instances.Count, DepthStep = DepthStep });
            pass.Dispatch((uint)Instances.Count, 1, 1);
        }

        residents.Expand(commands, pass, pool.Buffer!, _instances.Buffer!, _slots.Buffer!, DepthStep);
        commands.EndComputePass(pass);
    }

    public void Dispose()
    {
        _instances.Dispose();
        _slots.Dispose();
        _placed.Dispose();
        _opaque.Dispose();
        _expand.Dispose();
    }
}
