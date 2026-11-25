using System.Collections.Generic;
using System.IO;
using System.Numerics;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using MwColor = MoonWorks.Graphics.Color;
using Rect = MoonWorks.Graphics.Rect;
using Shader = MoonWorks.Graphics.Shader;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// A graph's exits: its draws into <c>frame</c>, inside the job's render pass at the item's place, depth tested against
/// the canvas and blended as the item is placed. A present draws two of the graph's images over the item's box
/// (traced.slang: colour blended, depth within the step's slice; none reads as the item's plane); a shade draws the
/// stack's own triangles, placed by its deform, once or per pool item or as many as a <c>[Draws]</c> buffer says.
/// </summary>
internal sealed class ExitDraws(GraphicsDevice device, FrameTargets targets, CanvasPipelines pipelines, Dictionary<ulong, Texture> textures,
    CanvasParticles particles, MaterialStore materials, GlobalBindings globals, GridCutter grids, GpuArray<CanvasVertex> vertices,
    IndirectDraws indirect) : IDisposable
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ViewUniforms { public float Width, Height, Depth, Span; }

    /// <summary>particle_mesh.slang's <c>Place</c>: vertex uniform buffer 1 of a particle-mesh draw.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PlaceUniforms
    {
        public float OriginX, OriginY;
        public uint First, Pad1;
        public float XX, XY, YX, YY;
    }

    private Texture? _flatDepth;
    private Shader? _tracedVertex, _tracedFragment;
    private readonly Dictionary<(BlendMode, CustomBlend?, SampleCount), GraphicsPipeline> _tracedPipelines = [];

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

    /// <summary>
    /// Makes, once, a depth texture of zeros the target's size: the item's own plane, for an image presented without a
    /// depth. Call it outside any render pass, before the draws.
    /// </summary>
    public void Prepare(CommandBuffer commands)
    {
        if (_flatDepth is not null)
            return;
        _flatDepth = Texture.Create2D(device, "flat depth", targets.Width, targets.Height, TextureFormat.R32Float,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);
        commands.EndRenderPass(commands.BeginRenderPass(new ColorTargetInfo(_flatDepth, new MwColor(0, 0, 0, 0))));
    }

    /// <summary>A present: the images the call names, the item's own, drawn over its box. False when there is nothing to draw.</summary>
    public bool Present(CommandBuffer commands, RenderPass pass, Step step, ItemSet? set, SampleCount samples, StackBuild.Run call)
    {
        if (set is null)
            return false;
        (int x, int y, int width, int height) = set.Box;
        Texture colour = set.Images[(int)call.Shown];
        Texture depth = call.ShownDepth == StackBuild.Run.None ? _flatDepth! : set.Images[(int)call.ShownDepth];
        var view = new TracedView
        {
            ScreenWidth = targets.Width, ScreenHeight = targets.Height, Depth = step.Depth, Span = step.Span,
            OriginX = x, OriginY = y, Width = width, Height = height,
            ColourStretch = set.Stretch((int)call.Shown), DepthStretch = call.ShownDepth == StackBuild.Run.None ? Vector2.One : set.Stretch((int)call.ShownDepth),
        };
        pass.BindGraphicsPipeline(PresentPipeline(step.Material, samples));
        pass.SetScissor(step.Clip);
        commands.PushVertexUniformData(view);
        commands.PushFragmentUniformData(view);
        pass.BindFragmentSamplers(
            new TextureSamplerBinding(colour, device.LinearSampler),
            new TextureSamplerBinding(depth, device.PointSampler));
        pass.DrawPrimitives(6, 1, 0, 0);
        return true;
    }

    /// <summary>
    /// The stack's triangles at step <paramref name="s"/>. <paramref name="clocked"/> says the frame's clocks are already
    /// pushed for an effect in this pass, and is set once they are. False when nothing was drawn.
    /// </summary>
    public bool Shade(CommandBuffer commands, RenderPass pass, int s, Step step, CanvasEffect effect, SampleCount samples,
        in FrameUniforms frame, ref bool clocked)
    {
        commands.PushVertexUniformData(new ViewUniforms { Width = targets.Width, Height = targets.Height, Depth = step.Depth, Span = step.Span });
        if (!clocked)
        {
            // A deforming effect's vertex stage reads the frame too; only its clocks matter there.
            commands.PushVertexUniformData(frame, SlangShaders.VertexFrameSlot);
            commands.PushFragmentUniformData(frame);
            clocked = true;
        }

        bool instanced = step.Kind == StepKind.MeshParticles;
        GraphicsPipeline pipeline = instanced
            ? pipelines.ParticlePipeline(effect, step.Material, samples)
            : pipelines.Pipeline(effect, step.Material, samples, depth: true);
        if (step.Material?.Custom is { UsesConstant: true } custom)
            pass.SetBlendConstants(new MwColor(custom.Constant.R, custom.Constant.G, custom.Constant.B, custom.Constant.A));
        pass.BindGraphicsPipeline(pipeline);
        pass.BindVertexBuffers(step.Grid ? grids.Vertices.Buffer! : step.Mesh?.Gpu.Buffer ?? vertices.Buffer!);
        pass.SetScissor(step.Clip);
        BindSamplers(pass, step, effect.Compiled.Samplers);

        // After the globals: kept state when the stack keeps some, then its pool's items.
        Buffer? items = effect.Compiled.Pool is null ? null : materials.PoolItems(step.Material);
        Buffer? state = materials.State(step.Material);
        Buffer[]? built = effect.Compiled.Build is null ? null : materials.Built(step.Material);
        if (effect.Compiled.StorageBuffers > 0)
            pass.BindFragmentStorageBuffers(effect.Compiled.Update is null
                ? globals.Bound(effect.Compiled, items, null, built)
                : globals.Bound(effect.Compiled, state, items, built));
        if (effect.Compiled.ParamsSize > 0)
            ShaderParams.Push(commands, effect.Compiled, step.Material, step.Values);
        if (instanced)
        {
            // Each particle places a copy of the triangles; the effect's own deform sits out.
            pass.BindVertexStorageBuffers(particles.Arena, particles.Effects);
            Transform2D place = step.Transform;
            commands.PushVertexUniformData(new PlaceUniforms
            {
                OriginX = place.Origin.X, OriginY = place.Origin.Y,
                First = step.Particles!.First,
                XX = place.X.X, XY = place.X.Y, YX = place.Y.X, YY = place.Y.Y,
            }, 1);
            pass.DrawPrimitives(step.Count, ParticleField.Capacity, step.Start, 0);
            return true;
        }

        if (effect.Compiled.Vertex is { } vertex)
        {
            if (vertex.Samplers.Length > 0)
                pass.BindVertexSamplers(new TextureSamplerBinding(targets.DepthRead, device.PointSampler));
            if (vertex.StorageBuffers > 0)
                pass.BindVertexStorageBuffers(globals.Bound(vertex, items, null, built));
            if (vertex.ParamsSize > 0)
                ShaderParams.Push(commands, vertex, step.Material, step.Values, stage: ParamsStage.Vertex);
        }

        // A pooled stack draws once per item; until its pool (or its graph's buffers) exists, not at all.
        uint copies = effect.Compiled.Pool is { } pooled ? items is null ? 0u : (uint)pooled.ItemCount : 1u;
        if (effect.Compiled.Build is not null && built is null)
            copies = 0;
        if (indirect.Slot(s, out uint slot))
            pass.DrawPrimitivesIndirect(indirect.Args, slot * IndirectDraws.ArgsBytes, 1);
        else if (copies > 0)
            pass.DrawPrimitives(step.Count, copies, step.Start, 0);
        else
            return false;
        return true;
    }

    /// <summary>The shade's samplers: the item's texture, then by name the screen copy, the canvas depth or the material's textures.</summary>
    private void BindSamplers(RenderPass pass, Step step, string[] names)
    {
        Span<TextureSamplerBinding> samplers = stackalloc TextureSamplerBinding[8];
        int count = Math.Clamp(names.Length, 1, samplers.Length);
        samplers[0] = new TextureSamplerBinding(textures.GetValueOrDefault(step.Texture) ?? targets.White, targets.Sampler);
        for (int i = 1; i < count; i++)
        {
            if (i == SlangShaders.FragmentDepth && names[i] == "depthTexture")
            {
                samplers[i] = new TextureSamplerBinding(targets.DepthRead, device.PointSampler);
                continue;
            }

            Texture? texture = i == 1 && names[i] == "screenTexture"
                ? targets.Screen
                : names[i] is { } name && step.Material?.Textures.TryGetValue(name, out Handle id) == true
                    ? textures.GetValueOrDefault(id.Id)
                    : null;
            samplers[i] = new TextureSamplerBinding(texture ?? targets.White, targets.Sampler);
        }

        pass.BindFragmentSamplers(samplers[..count]);
    }

    /// <summary>traced.slang's pipeline for a material's blend: depth tested and written as a deforming effect's is.</summary>
    private GraphicsPipeline PresentPipeline(CanvasMaterial? material, SampleCount samples)
    {
        BlendMode blend = material?.Blend ?? BlendMode.Mix;
        CustomBlend? custom = material?.Custom?.PipelineKey;
        if (_tracedPipelines.TryGetValue((blend, custom, samples), out GraphicsPipeline? pipeline))
            return pipeline;

        if (_tracedVertex is null)
        {
            string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "traced.slang");
            _tracedVertex = Shader.Create(device, SlangShaders.Compile(module, "cover", "vertex"), "main",
                new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Vertex, NumUniformBuffers = 1 });
            _tracedFragment = Shader.Create(device, SlangShaders.Compile(module, "show", "fragment"), "main",
                new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Fragment, NumSamplers = 2, NumUniformBuffers = 1 });
        }

        pipeline = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
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
                        Format = targets.Format,
                        BlendState = custom is { } state ? CanvasPipelines.Blend(state) : CanvasPipelines.Blend(blend),
                    },
                ],
                HasDepthStencilTarget = true,
                DepthStencilFormat = targets.DepthFormat,
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
        _flatDepth?.Dispose();
    }
}
