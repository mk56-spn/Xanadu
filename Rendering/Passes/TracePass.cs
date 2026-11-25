using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using Rect = MoonWorks.Graphics.Rect;
using Shader = MoonWorks.Graphics.Shader;
using Texture = MoonWorks.Graphics.Texture;
using MwColor = MoonWorks.Graphics.Color;

namespace Xanadu.Rendering.Passes;

/// <summary>A TRACE brick's quad (uniform 2, <c>XanaduTraceQuad</c> in SlangShaders.Traces): the quad on the target and the box traced.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TraceQuad
{
    public Vector2 Corner, XAxis, YAxis;
    public int OriginX, OriginY;
    public uint Width, Height;
    public float Dt, Pad;
}

/// <summary>What traced.slang reads, in both stages: the target, the step's depth slice, the box drawn.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TracedView
{
    public float ScreenWidth, ScreenHeight, Depth, Span;
    public float OriginX, OriginY, Width, Height;
}

/// <summary>
/// Stacks with a TRACE brick. At the step's place in the frame the draw pass ends, the trace
/// runs as compute over the quad's box on the target (a thread a pixel, in its tiles) into two
/// textures, colour and depth, and the pass starts again; traced.slang then draws them into the
/// frame over that box, colour blended as the stack's material blends, depth written within the
/// step's slice as a deform's corners write theirs (pixels into the screen, squashed). The
/// stack's own triangles draw after it, tested against that depth. The textures are the
/// target's size and shared by every trace: each runs and is drawn before the next.
/// </summary>
internal sealed class TracePass(GraphicsDevice device, CanvasFrame frame, FrameTargets targets, MaterialStore materials,
    GlobalBindings globals) : IDisposable
{
    private readonly GraphicsDevice _device = device;
    private readonly CanvasFrame _frame = frame;
    private readonly FrameTargets _targets = targets;
    private readonly MaterialStore _materials = materials;
    private readonly GlobalBindings _globals = globals;

    private Texture? _tracedColour, _tracedDepth;
    private Shader? _tracedVertex, _tracedFragment;
    private readonly Dictionary<(BlendMode, CustomBlend?, SampleCount), GraphicsPipeline> _tracedPipelines = [];
    private readonly HashSet<CanvasEffect> _untraceable = new(ReferenceEqualityComparer.Instance);

    /// <summary>The box the last trace filled, which <see cref="Draw"/> draws.</summary>
    private (int X, int Y, int Width, int Height) _tracedBox;

    /// <summary>Runs the step's trace into the two textures; false when there is nothing to draw (off screen, or no quad to trace).</summary>
    public bool Run(CommandBuffer commands, CanvasEffect effect, Step step, float time)
    {
        if (step.Mesh is not { } mesh || step.Start >= mesh.Vertices.Count)
        {
            // A [Grid]'s cells are cut on the GPU: the CPU has no corner to hand the trace.
            if (_untraceable.Add(effect))
                Console.Error.WriteLine($"Shader stack {step.Material?.Shader}: a TRACE brick needs its quad uncut; drop the deform's [Grid].");
            return false;
        }

        RestFrame rest = mesh.Vertices[(int)step.Start].Rest;
        if (Box(rest, step.Clip) is not var (x, y, width, height))
            return false;

        CompiledTrace trace = effect.Compiled.Trace!;
        EnsureTraceTextures();
        using GpuScope scope = commands.Scope("trace");
        commands.PushComputeUniformData(FrameUniforms.Now(_targets.Width, _targets.Height, time));
        if (trace.Shader.ParamsSize > 0)
            ShaderParams.Push(commands, trace.Shader, step.Material, step.Values, stage: ParamsStage.Compute);
        commands.PushComputeUniformData(new TraceQuad
        {
            Corner = rest.Corner, XAxis = rest.XAxis, YAxis = rest.YAxis,
            OriginX = x, OriginY = y, Width = (uint)width, Height = (uint)height, Dt = _frame.Dt,
        }, 2);

        Buffer[]? built = _materials.Built(step.Material);
        Span<StorageBufferReadWriteBinding> buffers = stackalloc StorageBufferReadWriteBinding[trace.Buffers.Length];
        for (int k = 0; k < buffers.Length; k++)
        {
            if (built is null)
                return false;
            buffers[k] = new StorageBufferReadWriteBinding(built[trace.Buffers[k]], cycle: false);
        }

        ComputePass compute = commands.BeginComputePass(
            [new StorageTextureReadWriteBinding(_tracedColour!), new StorageTextureReadWriteBinding(_tracedDepth!)], buffers);
        compute.BindComputePipeline(effect.Trace!);
        compute.BindSamplers(new TextureSamplerBinding(_targets.DepthRead, _device.PointSampler));
        Buffer? state = _materials.State(step.Material);
        if (trace.Shader.StorageBuffers > 0)
            compute.BindStorageBuffers(_globals.Bound(trace.Shader.StorageBuffers, state));
        compute.Dispatch((uint)(width + trace.Width - 1) / (uint)trace.Width, (uint)(height + trace.Height - 1) / (uint)trace.Height, 1);
        commands.EndComputePass(compute);
        _tracedBox = (x, y, width, height);
        return true;
    }

    /// <summary>The pixels the quad covers on the target, within the clip; null when none.</summary>
    private (int X, int Y, int Width, int Height)? Box(RestFrame rest, Rect clip) => BoxOf(rest, clip, _targets.Width, _targets.Height);

    /// <summary>The pixels a quad covers on a target <paramref name="targetWidth"/> × <paramref name="targetHeight"/>, within the clip; null when none.</summary>
    public static (int X, int Y, int Width, int Height)? BoxOf(RestFrame rest, Rect clip, uint targetWidth, uint targetHeight)
    {
        Vector2 a = rest.Corner, b = a + rest.XAxis, c = a + rest.YAxis, d = b + rest.YAxis;
        Vector2 low = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        Vector2 high = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        int x0 = Math.Max((int)MathF.Floor(low.X), Math.Max(clip.X, 0));
        int y0 = Math.Max((int)MathF.Floor(low.Y), Math.Max(clip.Y, 0));
        int x1 = Math.Min((int)MathF.Ceiling(high.X), Math.Min(clip.X + clip.W, (int)targetWidth));
        int y1 = Math.Min((int)MathF.Ceiling(high.Y), Math.Min(clip.Y + clip.H, (int)targetHeight));
        return x1 > x0 && y1 > y0 ? (x0, y0, x1 - x0, y1 - y0) : null;
    }

    /// <summary>The last trace's textures drawn into the pass over its box, with its depth in the step's slice.</summary>
    public void Draw(CommandBuffer commands, RenderPass pass, Step step, SampleCount samples) =>
        DrawOver(commands, pass, step, samples, _tracedColour!, _tracedDepth!, _tracedBox);

    /// <summary>
    /// A graph's presented images drawn into the pass over the step's quad, as a trace's are: colour blended as the
    /// material blends, depth (none reads as the item's plane) in the step's slice. False when the quad is off screen.
    /// </summary>
    public bool DrawPresented(CommandBuffer commands, RenderPass pass, Step step, SampleCount samples, Texture colour, Texture? depth)
    {
        if (step.Mesh is not { } mesh || step.Start >= mesh.Vertices.Count)
            return false;
        if (Box(mesh.Vertices[(int)step.Start].Rest, step.Clip) is not { } box)
            return false;
        DrawOver(commands, pass, step, samples, colour, depth ?? _flatDepth!, box);
        return true;
    }

    /// <summary>
    /// Makes, once, a depth texture of zeros the target's size: the item's own plane, for an image presented without a
    /// depth. Call it outside any render pass, before the draws.
    /// </summary>
    public void Prepare(CommandBuffer commands)
    {
        if (_flatDepth is not null)
            return;
        _flatDepth = Texture.Create2D(_device, "flat depth", _targets.Width, _targets.Height, TextureFormat.R32Float,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);
        commands.EndRenderPass(commands.BeginRenderPass(new ColorTargetInfo(_flatDepth, new MwColor(0, 0, 0, 0))));
    }

    private Texture? _flatDepth;

    private void DrawOver(CommandBuffer commands, RenderPass pass, Step step, SampleCount samples, Texture colour, Texture depth,
        (int X, int Y, int Width, int Height) box)
    {
        (int x, int y, int width, int height) = box;
        var view = new TracedView
        {
            ScreenWidth = _targets.Width, ScreenHeight = _targets.Height, Depth = step.Depth, Span = step.Span,
            OriginX = x, OriginY = y, Width = width, Height = height,
        };
        pass.BindGraphicsPipeline(TracedPipeline(step.Material, samples));
        pass.SetScissor(step.Clip);
        commands.PushVertexUniformData(view);
        commands.PushFragmentUniformData(view);
        pass.BindFragmentSamplers(
            new TextureSamplerBinding(colour, _device.PointSampler),
            new TextureSamplerBinding(depth, _device.PointSampler));
        pass.DrawPrimitives(6, 1, 0, 0);
    }

    private void EnsureTraceTextures()
    {
        if (_tracedColour is not null)
            return;
        _tracedColour = Texture.Create2D(_device, "traced colour", _targets.Width, _targets.Height, TextureFormat.R16G16B16A16Float,
            TextureUsageFlags.ComputeStorageWrite | TextureUsageFlags.Sampler);
        _tracedDepth = Texture.Create2D(_device, "traced depth", _targets.Width, _targets.Height, TextureFormat.R32Float,
            TextureUsageFlags.ComputeStorageWrite | TextureUsageFlags.Sampler);
    }

    /// <summary>traced.slang's pipeline for a material's blend: depth tested and written as a deforming effect's is.</summary>
    private GraphicsPipeline TracedPipeline(CanvasMaterial? material, SampleCount samples)
    {
        BlendMode blend = material?.Blend ?? BlendMode.Mix;
        CustomBlend? custom = material?.Custom?.PipelineKey;
        if (_tracedPipelines.TryGetValue((blend, custom, samples), out GraphicsPipeline? pipeline))
            return pipeline;

        if (_tracedVertex is null)
        {
            string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "traced.slang");
            _tracedVertex = Shader.Create(_device, SlangShaders.Compile(module, "cover", "vertex"), "main",
                new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Vertex, NumUniformBuffers = 1 });
            _tracedFragment = Shader.Create(_device, SlangShaders.Compile(module, "show", "fragment"), "main",
                new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Fragment, NumSamplers = 2, NumUniformBuffers = 1 });
        }

        pipeline = GraphicsPipeline.Create(_device, new GraphicsPipelineCreateInfo
        {
            VertexShader = _tracedVertex,
            FragmentShader = _tracedFragment!,
            VertexInputState = VertexInputState.Empty,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = new MultisampleState { SampleCount = samples },
            DepthStencilState = new DepthStencilState { EnableDepthTest = true, EnableDepthWrite = true, CompareOp = CompareOp.LessOrEqual },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription
                    {
                        Format = _targets.Format,
                        BlendState = custom is { } state ? CanvasPipelines.Blend(state) : CanvasPipelines.Blend(blend),
                    },
                ],
                HasDepthStencilTarget = true,
                DepthStencilFormat = _targets.DepthFormat,
            },
        });
        _tracedPipelines[(blend, custom, samples)] = pipeline;
        return pipeline;
    }

    public void Dispose()
    {
        foreach (GraphicsPipeline pipeline in _tracedPipelines.Values)
            pipeline.Dispose();
        _tracedVertex?.Dispose();
        _tracedFragment?.Dispose();
        _tracedColour?.Dispose();
        _tracedDepth?.Dispose();
        _flatDepth?.Dispose();
    }
}
