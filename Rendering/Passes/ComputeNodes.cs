using System.Numerics;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using Entity = Friflo.Engine.ECS.Entity;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// The graph's compute nodes, each a compute pass of its own (SDL_GPU orders storage writes only between passes):
/// UPDATE's state, STEP's pool, a schedule's <c>[Pass]</c> and <c>[Pixels]</c> calls, and a trace.
/// </summary>
/// <remarks>
/// A pool born at a rate is told each frame how many births are due: the rate times the frame's time, the fraction
/// carried to the next, so the rate comes out exact. A material whose effect compiled again gets a fresh pool, made by
/// the stack's <c>start</c>. A pixel node or a trace runs over the item's box on the target, a thread a pixel, in tiles,
/// on that item's images (<see cref="ItemSet"/>).
/// </remarks>
internal sealed class ComputeNodes(GraphicsDevice device, FrameTargets targets, MaterialStore materials, GlobalBindings globals)
{
    /// <summary>A stack's UPDATE: its state, made zeroed on its first run, updated once.</summary>
    public void Update(CommandBuffer commands, CanvasMaterial material, CanvasEffect effect, float dt, float time)
    {
        // Code that asked for state, or a lab-only effect file (Docs/shaders.md §14).
        if (!(material.KeepsState || (material.Source is null && SlangShaders.IsLabOnly(material.Shader))))
            return;
        if (effect.Update is not { } update || effect.Compiled.Update is not { } compiled)
            return;

        Entity entity = materials.For(material);
        if (!entity.TryGetComponent(out StateBuffer held))
        {
            Buffer made = Buffer.Create<byte>(device, "shader state",
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead,
                SlangShaders.MaxStateBytes);
            CopyPass copy = commands.BeginCopyPass();
            copy.UploadToBuffer(globals.Zeros, made, cycle: false);
            commands.EndCopyPass(copy);
            entity.AddComponent(held = new StateBuffer(made));
        }

        commands.PushComputeUniformData(new UpdateUniforms { Dt = dt, Time = time, Beat = ShaderClock.Beat });
        if (compiled.ParamsSize > 0)
            ShaderParams.Push(commands, compiled, material, null, stage: ParamsStage.Compute);
        ComputePass pass = commands.BeginComputePass(new StorageBufferReadWriteBinding(held.Buffer, cycle: false));
        pass.BindComputePipeline(update);
        if (compiled.StorageBuffers > 0)
            pass.BindStorageBuffers(globals.Bound(compiled.StorageBuffers));
        pass.Dispatch(1, 1, 1);
        commands.EndComputePass(pass);
    }

    /// <summary>A stack's STEP: its pool, made on its first run after a compile, stepped by the frame's time.</summary>
    public void Step(CommandBuffer commands, CanvasMaterial material, CanvasEffect effect, float dt, float time)
    {
        if (effect.Pool is not { } operation || effect.Compiled.Pool is not { } compiled)
            return;

        Entity entity = materials.For(material);
        bool fresh = !entity.TryGetComponent(out ItemPool kept) || kept.Effect != effect;
        if (fresh)
        {
            kept.Items?.Dispose();
            // Born at a rate: two slots more, the births' counters.
            int slots = compiled.ItemCount + (compiled.PoolRate > 0 ? 2 : 0);
            entity.AddComponent(new ItemPool(effect, Buffer.Create<byte>(device, "shader pool",
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead,
                (uint)(compiled.ItemBytes * slots))));
        }

        ref ItemPool pool = ref entity.GetComponent<ItemPool>();
        var job = new PoolJob { Reset = fresh ? 1u : 0u, Born = pool.Born, Parity = pool.Parity };
        if (compiled.PoolRate > 0 && !fresh)
        {
            // More births than slots can't all find one; the rest are dropped, as a full pool drops them.
            pool.Owed += compiled.PoolRate * dt;
            uint births = (uint)Math.Min(Math.Floor(pool.Owed), compiled.ItemCount);
            pool.Owed = Math.Min(pool.Owed - births, 1);
            job.Births = births;
            pool.Born += births;
            pool.Parity ^= 1;
        }

        if (compiled.ParamsSize > 0)
            ShaderParams.Push(commands, compiled, material, null, stage: ParamsStage.Compute);
        // Its inputs after the jobs: the globals, then kept state when the stack keeps some.
        operation.Submit(job);
        operation.Run(commands, pool.Items, globals.Bound(compiled.StorageBuffers - 1, materials.State(material)),
            new UpdateUniforms { Dt = dt, Time = time, Beat = ShaderClock.Beat });
    }

    /// <summary>A schedule's <c>[Pass]</c> call over elements or groups, once for the material, on its buffers and its own images.</summary>
    public void Pass(CommandBuffer commands, CanvasEffect effect, BuildBuffers kept, Texture?[]? shared, StackBuild.Run call,
        CanvasMaterial material, float dt, float time)
    {
        CompiledPass pass = effect.Compiled.Build!.Passes[call.Pass];
        using GpuScope scope = commands.Scope(pass.Name);
        commands.PushComputeUniformData(new PassUniforms
        {
            Dt = dt, Time = time, Over = call.Over, Beat = ShaderClock.Beat, A = call.Values[0], B = call.Values[1], C = call.Values[2], D = call.Values[3],
        });
        if (pass.Shader.ParamsSize > 0 && pass.Shader.UniformBuffers > 1)
            ShaderParams.Push(commands, pass.Shader, material, null, stage: ParamsStage.Compute);
        ComputePass compute = Begin(commands, shared ?? [], pass.Images, kept.Buffers, pass.Buffers, effect.Passes[call.Pass]!, pass.Shader,
            material);
        compute.Dispatch(Math.Min(call.Groups, 65535u), 1, 1);
        commands.EndComputePass(compute);
    }

    /// <summary>
    /// A schedule's <c>[Pixels]</c> call over the item's box, a thread a pixel, in its tiles, on the item's images; or over
    /// the texels of the image it names (<c>over:</c>), each standing for a stretch of the box.
    /// </summary>
    public void Pixels(CommandBuffer commands, CanvasEffect effect, BuildBuffers kept, ItemSet set, RestFrame rest, StackBuild.Run call,
        Step step, float dt, float time)
    {
        CompiledPass pass = effect.Compiled.Build!.Passes[call.Pass];
        using GpuScope scope = commands.Scope(pass.Name);
        (uint width, uint height) = pass.OverImage < 0 ? ((uint)set.Box.Width, (uint)set.Box.Height) : set.Extents[pass.OverImage];
        PushBox(commands, pass.Shader, step, rest, set.Box, dt, time, pass.OverImage < 0 ? Vector2.One : set.Stretch(pass.OverImage));
        ComputePass compute = Begin(commands, set.Images, pass.Images, kept.Buffers, pass.Buffers, effect.Passes[call.Pass]!, pass.Shader,
            step.Material!);
        (int tileWidth, int tileHeight) = pass.Tile!.Value;
        compute.Dispatch((width + (uint)tileWidth - 1) / (uint)tileWidth, (height + (uint)tileHeight - 1) / (uint)tileHeight, 1);
        commands.EndComputePass(compute);
    }

    /// <summary>The TRACE brick over the item's box into its two traced images, the canvas depth bound for it to read.</summary>
    public void Trace(CommandBuffer commands, CanvasEffect effect, BuildBuffers kept, ItemSet set, RestFrame rest, Step step, float dt, float time)
    {
        CompiledTrace trace = effect.Compiled.Trace!;
        using GpuScope scope = commands.Scope("trace");
        PushBox(commands, trace.Shader, step, rest, set.Box, dt, time, Vector2.One);
        int traced = GraphPlan.TracedImage(effect.Compiled);
        ComputePass compute = Begin(commands, set.Images, [traced, traced + 1], kept.Buffers, trace.Buffers, effect.Trace!, trace.Shader,
            step.Material!);
        compute.BindSamplers(new TextureSamplerBinding(targets.DepthRead, device.PointSampler));
        (int width, int height) = (set.Box.Width, set.Box.Height);
        compute.Dispatch((uint)(width + trace.Width - 1) / (uint)trace.Width, (uint)(height + trace.Height - 1) / (uint)trace.Height, 1);
        commands.EndComputePass(compute);
    }

    /// <summary>A box node's uniforms: the frame, the values, then the quad and box at 2.</summary>
    private void PushBox(CommandBuffer commands, CompiledShader shader, Step step, RestFrame rest, (int X, int Y, int Width, int Height) box,
        float dt, float time, Vector2 stretch)
    {
        commands.PushComputeUniformData(FrameUniforms.Now(targets.Width, targets.Height, time));
        if (shader.ParamsSize > 0)
            ShaderParams.Push(commands, shader, step.Material, step.Values, stage: ParamsStage.Compute);
        commands.PushComputeUniformData(new TraceQuad
        {
            Corner = rest.Corner, XAxis = rest.XAxis, YAxis = rest.YAxis,
            OriginX = box.X, OriginY = box.Y, Width = (uint)box.Width, Height = (uint)box.Height, Dt = dt, Stretch = stretch,
        }, 2);
    }

    /// <summary>A pass begun with the images and buffers it touches read-write, and the globals and kept state read-only.</summary>
    private ComputePass Begin(CommandBuffer commands, Texture?[] images, int[] imagePlaces, Buffer[] buffers, int[] bufferPlaces,
        ComputePipeline pipeline, CompiledShader shader, CanvasMaterial material)
    {
        Span<StorageTextureReadWriteBinding> writtenImages = stackalloc StorageTextureReadWriteBinding[imagePlaces.Length];
        for (int k = 0; k < writtenImages.Length; k++)
            writtenImages[k] = new StorageTextureReadWriteBinding(images[imagePlaces[k]]!);
        Span<StorageBufferReadWriteBinding> writes = stackalloc StorageBufferReadWriteBinding[bufferPlaces.Length];
        for (int k = 0; k < writes.Length; k++)
            writes[k] = new StorageBufferReadWriteBinding(buffers[bufferPlaces[k]], cycle: false);
        ComputePass compute = commands.BeginComputePass(writtenImages, writes);
        compute.BindComputePipeline(pipeline);
        if (shader.StorageBuffers > 0)
            compute.BindStorageBuffers(globals.Bound(shader.StorageBuffers, materials.State(material)));
        return compute;
    }
}
