using System.Collections.Generic;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using Entity = Friflo.Engine.ECS.Entity;
using MwColor = MoonWorks.Graphics.Color;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// A stack's graph (the BUILD brick's <c>graph()</c>, or <c>build()</c>): every frame, before any pass draws, the CPU
/// walks the schedule and runs each call as a compute pass of its own, so a pass sees all the passes before it wrote
/// (SDL_GPU orders storage writes only between passes). Each material keeps its own buffers and images (the target's
/// size), zeroed when the effect compiles. A pass over elements or groups runs as it says; a pixel pass runs over the
/// item's box on the target, in its tiles. <c>present</c> is remembered for the draw, which shows those images where the
/// item is (<see cref="TracePass.DrawPresented"/>).
/// </summary>
internal sealed class GraphPasses(GraphicsDevice device, FrameTargets targets, MaterialStore materials, GlobalBindings globals) : IDisposable
{
    /// <summary>Bytes of zeros a new buffer is cleared from, a piece at a time.</summary>
    private const uint ZeroPiece = 1 << 20;

    private readonly GraphicsDevice _device = device;
    private readonly FrameTargets _targets = targets;
    private readonly MaterialStore _materials = materials;
    private readonly GlobalBindings _globals = globals;
    private readonly HashSet<CanvasEffect> _brokenSchedules = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<CanvasEffect> _uncut = new(ReferenceEqualityComparer.Instance);
    private readonly List<StackBuild.Run> _calls = [];
    private readonly Dictionary<CanvasMaterial, (Texture Colour, Texture? Depth)> _presented = new(ReferenceEqualityComparer.Instance);
    private TransferBuffer? _zeroPiece;

    /// <summary>Forgets last frame's presents: a graph shows what it presented this frame, or nothing.</summary>
    public void BeginFrame() => _presented.Clear();

    /// <summary>The images a material's graph presented this frame; false when it presented none.</summary>
    public bool Presented(CanvasMaterial? material, out Texture colour, out Texture? depth)
    {
        if (material is not null && _presented.TryGetValue(material, out (Texture Colour, Texture? Depth) shown))
        {
            (colour, depth) = shown;
            return true;
        }

        colour = null!;
        depth = null;
        return false;
    }

    /// <summary>A material's graph, run as its schedule calls the passes this frame; <paramref name="step"/> is where its item is.</summary>
    public void Run(CommandBuffer commands, CanvasMaterial material, CanvasEffect effect, Step step, float dt, float time)
    {
        if (effect.Compiled.Build is not { } build)
            return;

        Entity entity = _materials.For(material);
        if (!entity.TryGetComponent(out BuildBuffers kept) || kept.Effect != effect)
        {
            if (kept.Buffers is not null)
                MaterialStore.Release(kept);
            entity.AddComponent(kept = new BuildBuffers(effect, MakeBuffers(commands, build), MakeImages(commands, build.Shape)));
        }

        _calls.Clear();
        try
        {
            build.Shape.Walk(name => Uniform(effect.Compiled, material, name), _calls);
        }
        catch (BuildProblem problem)
        {
            // Its values made the schedule impossible (a loop that won't end): said once, nothing run.
            if (_brokenSchedules.Add(effect))
                Console.Error.WriteLine($"Shader stack {material.Shader}: graph() can't run: {problem.Message}");
            return;
        }

        Buffer? state = _materials.State(material);
        foreach (StackBuild.Run call in _calls)
        {
            if (call.Presents)
            {
                _presented[material] = (kept.Images[(int)call.A], call.B == StackBuild.Run.None ? null : kept.Images[(int)call.B]);
                continue;
            }

            CompiledPass pass = build.Passes[call.Pass];
            if (pass.Tile is not null)
                DispatchPixels(commands, effect, kept, pass, call, state, material, step, dt, time);
            else
                Dispatch(commands, effect, kept, pass, call, state, material, dt, time);
        }
    }

    private void Dispatch(CommandBuffer commands, CanvasEffect effect, BuildBuffers kept, CompiledPass pass, StackBuild.Run call,
        Buffer? state, CanvasMaterial material, float dt, float time)
    {
        if (call.Groups == 0)
            return;
        using GpuScope scope = commands.Scope(pass.Name);
        commands.PushComputeUniformData(new PassUniforms
        {
            Dt = dt, Time = time, Over = call.Over, Beat = ShaderClock.Beat, A = call.A, B = call.B, C = call.C, D = call.D,
        });
        if (pass.Shader.ParamsSize > 0 && pass.Shader.UniformBuffers > 1)
            ShaderParams.Push(commands, pass.Shader, material, null, stage: ParamsStage.Compute);
        ComputePass compute = Begin(commands, effect, kept, pass, call, state);
        compute.Dispatch(Math.Min(call.Groups, 65535u), 1, 1);
        commands.EndComputePass(compute);
    }

    /// <summary>A pixel pass over the item's box on the target, a thread a pixel, in its tiles.</summary>
    private void DispatchPixels(CommandBuffer commands, CanvasEffect effect, BuildBuffers kept, CompiledPass pass, StackBuild.Run call,
        Buffer? state, CanvasMaterial material, Step step, float dt, float time)
    {
        if (step.Mesh is not { } mesh || step.Start >= mesh.Vertices.Count)
        {
            // A [Grid]'s cells are cut on the GPU: the CPU has no corner to hand the pass.
            if (_uncut.Add(effect))
                Console.Error.WriteLine($"Shader stack {material.Shader}: a pixel pass needs its quad uncut; drop the deform's [Grid].");
            return;
        }

        RestFrame rest = mesh.Vertices[(int)step.Start].Rest;
        if (TracePass.BoxOf(rest, step.Clip, _targets.Width, _targets.Height) is not var (x, y, width, height))
            return;

        (int tileWidth, int tileHeight) = pass.Tile!.Value;
        using GpuScope scope = commands.Scope(pass.Name);
        commands.PushComputeUniformData(FrameUniforms.Now(_targets.Width, _targets.Height, time));
        if (pass.Shader.ParamsSize > 0)
            ShaderParams.Push(commands, pass.Shader, material, step.Values, stage: ParamsStage.Compute);
        commands.PushComputeUniformData(new TraceQuad
        {
            Corner = rest.Corner, XAxis = rest.XAxis, YAxis = rest.YAxis,
            OriginX = x, OriginY = y, Width = (uint)width, Height = (uint)height, Dt = dt,
        }, 2);
        ComputePass compute = Begin(commands, effect, kept, pass, call, state);
        compute.Dispatch((uint)((width + tileWidth - 1) / tileWidth), (uint)((height + tileHeight - 1) / tileHeight), 1);
        commands.EndComputePass(compute);
    }

    /// <summary>A pass begun with the images and buffers it touches read-write, and the globals and kept state read-only.</summary>
    private ComputePass Begin(CommandBuffer commands, CanvasEffect effect, BuildBuffers kept, CompiledPass pass, StackBuild.Run call, Buffer? state)
    {
        Span<StorageTextureReadWriteBinding> images = stackalloc StorageTextureReadWriteBinding[pass.Images.Length];
        for (int k = 0; k < images.Length; k++)
            images[k] = new StorageTextureReadWriteBinding(kept.Images[pass.Images[k]]);
        Span<StorageBufferReadWriteBinding> writes = stackalloc StorageBufferReadWriteBinding[pass.Buffers.Length];
        for (int k = 0; k < writes.Length; k++)
            writes[k] = new StorageBufferReadWriteBinding(kept.Buffers[pass.Buffers[k]], cycle: false);
        ComputePass compute = commands.BeginComputePass(images, writes);
        compute.BindComputePipeline(effect.Passes[call.Pass]);
        if (pass.Shader.StorageBuffers > 0)
            compute.BindStorageBuffers(_globals.Bound(pass.Shader.StorageBuffers, state));
        return compute;
    }

    /// <summary>A uniform's value as the schedule reads it: the material's, else its default; null for a name that isn't one.</summary>
    private static BuildValue? Uniform(CompiledShader compiled, CanvasMaterial material, string name) =>
        compiled.ScheduleValue(name, material.Values.TryGetValue(name, out System.Numerics.Vector4 set) ? set : null);

    /// <summary>The graph's buffers, each cleared to zeros.</summary>
    private Buffer[] MakeBuffers(CommandBuffer commands, CompiledBuild build)
    {
        if (_zeroPiece is null)
        {
            _zeroPiece = TransferBuffer.Create<byte>(_device, "build zeros", TransferBufferUsage.Upload, ZeroPiece);
            _zeroPiece.Map<byte>(cycle: false).Clear();
            _zeroPiece.Unmap();
        }

        var buffers = new Buffer[build.Bytes.Length];
        CopyPass copy = commands.BeginCopyPass();
        for (int k = 0; k < buffers.Length; k++)
        {
            uint bytes = (uint)((build.Bytes[k] + 3) / 4 * 4);
            buffers[k] = Buffer.Create<byte>(_device, $"stack {build.Shape.Buffers[k].Name}",
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead, bytes);
            for (uint at = 0; at < bytes; at += ZeroPiece)
                copy.UploadToBuffer<byte>(_zeroPiece, buffers[k], 0, at, Math.Min(ZeroPiece, bytes - at), cycle: false);
        }

        commands.EndCopyPass(copy);
        return buffers;
    }

    /// <summary>The graph's images, the target's size, each cleared to zeros.</summary>
    private Texture[] MakeImages(CommandBuffer commands, StackBuild shape)
    {
        var images = new Texture[shape.Images.Count];
        for (int k = 0; k < images.Length; k++)
        {
            BuildImage image = shape.Images[k];
            TextureUsageFlags usage = TextureUsageFlags.ComputeStorageRead | TextureUsageFlags.ComputeStorageWrite | TextureUsageFlags.ColorTarget;
            if (image.Format != "r32ui")
                usage |= TextureUsageFlags.Sampler;
            images[k] = Texture.Create2D(_device, $"graph {image.Name}", _targets.Width, _targets.Height, FormatOf(image.Format), usage);
            commands.EndRenderPass(commands.BeginRenderPass(new ColorTargetInfo(images[k], new MwColor(0, 0, 0, 0))));
        }

        return images;
    }

    /// <summary>An image's texel format, from the name Slang gives it.</summary>
    private static TextureFormat FormatOf(string format) => format switch
    {
        "rgba16f" => TextureFormat.R16G16B16A16Float,
        "rgba8" => TextureFormat.R8G8B8A8Unorm,
        "rgba32f" => TextureFormat.R32G32B32A32Float,
        "rg16f" => TextureFormat.R16G16Float,
        "r16f" => TextureFormat.R16Float,
        "r32f" => TextureFormat.R32Float,
        _ => TextureFormat.R32Uint,
    };

    public void Dispose() => _zeroPiece?.Dispose();
}
