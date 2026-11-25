using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using MwColor = MoonWorks.Graphics.Color;

namespace Xanadu.Host.Rendering;

/// <summary>
/// Drawing a job into its target: the opaque pre-pass first, then every step in order with the
/// depth test on. A screen read ends the pass, copies the target and starts it again; a trace
/// runs between passes and lands under its stack's triangles.
/// </summary>
internal sealed class DrawPass(GraphicsDevice device, CanvasFrame frame, FrameTargets targets, PrimitivePipelines primitives,
    CanvasPipelines pipelines, PlacedPrimitives placed, PrimitivePool pool, Dictionary<ulong, Texture> textures,
    CanvasParticles particles, MaterialStore materials, GlobalBindings globals, StackPasses stacks, TracePass traces,
    GridCutter grids, GpuArray<CanvasVertex> vertices)
{
    private const int Slots = PrimitivePipelines.Slots;

    [StructLayout(LayoutKind.Sequential)]
    private struct ViewUniforms { public float Width, Height, Depth, Span; }

    /// <summary>particle_mesh.slang's <c>Place</c>: vertex uniform buffer 1 of a particle-mesh draw.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PlaceUniforms
    {
        public float OriginX, OriginY;
        public uint First, Pad1;
        public float XX, XY, YX, YY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DrawUniforms
    {
        public float Width, Height;
        public uint First, Pad;
    }

    private readonly GraphicsDevice _device = device;
    private readonly List<Step> _steps = frame.Steps;
    private readonly List<ulong> _stepTextures = frame.StepTextures;
    private readonly FrameTargets _targets = targets;
    private readonly PrimitivePipelines _primitives = primitives;
    private readonly CanvasPipelines _pipelines = pipelines;
    private readonly PlacedPrimitives _placed = placed;
    private readonly PrimitivePool _pool = pool;
    private readonly CanvasParticles _particles = particles;
    private readonly MaterialStore _materials = materials;
    private readonly GlobalBindings _globals = globals;
    private readonly StackPasses _stacks = stacks;
    private readonly TracePass _traces = traces;
    private readonly GridCutter _grids = grids;
    private readonly GpuArray<CanvasVertex> _verticesGpu = vertices;
    private readonly Dictionary<ulong, Texture> _textures = textures;

    /// <summary>Draw calls and screen copies since the counts were last cleared.</summary>
    public int BatchCount, CopyCount;

    private void DrawPrimitives(CommandBuffer commands, RenderPass pass, SampleCount samples, bool opaque, uint first, uint count, int firstTexture)
    {
        pass.BindGraphicsPipeline(_primitives.Get(opaque, samples));
        pass.BindVertexStorageBuffers(opaque ? _placed.Opaque : _placed.Placed);
        pass.SetScissor(_targets.FullClip);
        if (!opaque)
        {
            Span<TextureSamplerBinding> samplers = stackalloc TextureSamplerBinding[Slots + 1];
            for (int i = 0; i < Slots; i++)
            {
                ulong id = _stepTextures[firstTexture + i];
                samplers[i] = new TextureSamplerBinding(id != 0 ? _textures.GetValueOrDefault(id) ?? _targets.White : _targets.White, _targets.Sampler);
            }

            samplers[Slots] = new TextureSamplerBinding(_pool.Glyphs?.Layers ?? _targets.NoLayers, _targets.Sampler);
            pass.BindFragmentSamplers(samplers);
        }

        commands.PushVertexUniformData(new DrawUniforms { Width = _targets.Width, Height = _targets.Height, First = first });
        pass.DrawPrimitives(count * 6, 1, 0, 0);
        BatchCount++;
    }

    /// <summary>Whether any effect the job draws samples the depth, in its shade or its deform.</summary>
    private bool ReadsDepth(Job job)
    {
        for (int s = job.FirstStep; s < job.FirstStep + job.StepCount; s++)
        {
            Step step = _steps[s];
            if (step.Kind is StepKind.Primitives or StepKind.Particles)
                continue;
            CanvasEffect effect = _pipelines.Effect(step.Material);
            if (effect.Compiled.ReadsDepth || effect.Compiled.Vertex?.ReadsDepth == true || effect.Compiled.Trace?.Shader.ReadsDepth == true)
                return true;
        }

        return false;
    }

    /// <summary>
    /// The job's opaque pre-pass again, depth only, into <see cref="FrameTargets.DepthRead"/>: the depth the
    /// main pass tests against, in a texture effects can sample (MSAA depth can't be resolved).
    /// </summary>
    private void FillDepth(CommandBuffer commands, Job job)
    {
        using GpuScope scope = commands.Scope("depth for reads");
        RenderPass pass = _targets.BeginDepthReadPass(commands);
        if (job.OpaqueReserved > 0)
        {
            pass.BindGraphicsPipeline(_primitives.DepthOnly);
            pass.BindVertexStorageBuffers(_placed.Opaque);
            pass.SetScissor(_targets.FullClip);
            commands.PushVertexUniformData(new DrawUniforms { Width = _targets.Width, Height = _targets.Height, First = job.OpaqueStart });
            pass.DrawPrimitives(job.OpaqueReserved * 6, 1, 0, 0);
        }

        commands.EndRenderPass(pass);
    }

    public void Draw(CommandBuffer commands, Job job, float time, bool msaa)
    {
        SampleCount samples = msaa ? _targets.Samples : SampleCount.One;
        if (job.StepCount > 0 && ReadsDepth(job))
            FillDepth(commands, job);
        RenderPass pass = _targets.BeginPass(commands, job.Target, job.Clear, msaa, load: false);
        if (job.StepCount == 0)
        {
            commands.EndRenderPass(pass);
            return;
        }

        if (job.OpaqueReserved > 0)
            DrawPrimitives(commands, pass, samples, opaque: true, job.OpaqueStart, job.OpaqueReserved, 0);

        var frame = FrameUniforms.Now(_targets.Width, _targets.Height, time);
        bool copied = false, effectState = false;
        Span<TextureSamplerBinding> samplers = stackalloc TextureSamplerBinding[8];
        for (int s = job.FirstStep; s < job.FirstStep + job.StepCount; s++)
        {
            Step step = _steps[s];
            switch (step.Kind)
            {
                case StepKind.Primitives:
                    DrawPrimitives(commands, pass, samples, opaque: false, step.Start, step.Count, step.FirstTexture);
                    effectState = false;
                    continue;

                case StepKind.Particles:
                    pass.SetScissor(step.Clip);
                    _particles.Draw(commands, pass, step.Particles!, step.Transform, step.Color, _targets.Width, _targets.Height,
                        _textures.GetValueOrDefault(step.Texture) ?? _targets.White, _targets.Sampler, samples, step.Depth);
                    BatchCount++;
                    effectState = false;
                    continue;
            }

            CanvasEffect effect = _pipelines.Effect(step.Material);
            string[] names = effect.Compiled.Samplers;
            // The first screen read copies the target and every later read in the pass shares that copy.
            if (effect.Compiled.ReadsScreen && !copied)
            {
                copied = true;
                commands.EndRenderPass(pass);
                _targets.CopyScreen(commands, job.Target);
                CopyCount++;
                pass = _targets.BeginPass(commands, job.Target, job.Clear, msaa, load: true);
                effectState = false;
            }

            if (effect.Trace is not null && step.Kind == StepKind.Effect)
            {
                // The trace runs as compute between passes, then its colour and depth land under the stack's triangles.
                commands.EndRenderPass(pass);
                bool traced = _traces.Run(commands, effect, step, time);
                pass = _targets.BeginPass(commands, job.Target, job.Clear, msaa, load: true);
                if (traced)
                {
                    _traces.Draw(commands, pass, step, samples);
                    BatchCount++;
                }
                effectState = false;
                if (effect.Compiled.Trace!.Alone)
                    continue;
            }

            commands.PushVertexUniformData(new ViewUniforms { Width = _targets.Width, Height = _targets.Height, Depth = step.Depth, Span = step.Span });
            if (!effectState)
            {
                // A deforming effect's vertex stage reads the frame too; only its clocks matter there.
                commands.PushVertexUniformData(frame, SlangShaders.VertexFrameSlot);
                commands.PushFragmentUniformData(frame);
                effectState = true;
            }

            bool instanced = step.Kind == StepKind.MeshParticles;
            GraphicsPipeline pipeline = instanced
                ? _pipelines.ParticlePipeline(effect, step.Material, samples)
                : _pipelines.Pipeline(effect, step.Material, samples, depth: true);
            if (step.Material?.Custom is { UsesConstant: true } custom)
                pass.SetBlendConstants(new MwColor(custom.Constant.R, custom.Constant.G, custom.Constant.B, custom.Constant.A));
            pass.BindGraphicsPipeline(pipeline);
            pass.BindVertexBuffers(step.Grid ? _grids.Vertices.Buffer! : step.Mesh?.Gpu.Buffer ?? _verticesGpu.Buffer!);

            pass.SetScissor(step.Clip);
            int count = Math.Clamp(names.Length, 1, samplers.Length);
            samplers[0] = new TextureSamplerBinding(_textures.GetValueOrDefault(step.Texture) ?? _targets.White, _targets.Sampler);
            for (int i = 1; i < count; i++)
            {
                if (i == SlangShaders.FragmentDepth && names[i] == "depthTexture")
                {
                    samplers[i] = new TextureSamplerBinding(_targets.DepthRead, _device.PointSampler);
                    continue;
                }

                Texture? texture = i == 1 && names[i] == "screenTexture"
                    ? _targets.Screen
                    : names[i] is { } name && step.Material?.Textures.TryGetValue(name, out Handle id) == true
                        ? _textures.GetValueOrDefault(id.Id)
                        : null;
                samplers[i] = new TextureSamplerBinding(texture ?? _targets.White, _targets.Sampler);
            }

            pass.BindFragmentSamplers(samplers[..count]);
            // After the globals: kept state when the stack keeps some, then its pool's items.
            Buffer? items = effect.Compiled.Pool is null ? null : _materials.PoolItems(step.Material);
            Buffer? state = _materials.State(step.Material);
            Buffer[]? built = effect.Compiled.Build is null ? null : _materials.Built(step.Material);
            if (effect.Compiled.StorageBuffers > 0)
                pass.BindFragmentStorageBuffers(effect.Compiled.Update is null
                    ? _globals.Bound(effect.Compiled, items, null, built)
                    : _globals.Bound(effect.Compiled, state, items, built));
            if (effect.Compiled.ParamsSize > 0)
                ShaderParams.Push(commands, effect.Compiled, step.Material, step.Values);
            if (instanced)
            {
                // Each particle places a copy of the triangles; the effect's own deform sits out.
                pass.BindVertexStorageBuffers(_particles.Arena, _particles.Effects);
                Transform2D place = step.Transform;
                commands.PushVertexUniformData(new PlaceUniforms
                {
                    OriginX = place.Origin.X, OriginY = place.Origin.Y,
                    First = step.Particles!.First,
                    XX = place.X.X, XY = place.X.Y, YX = place.Y.X, YY = place.Y.Y,
                }, 1);
                pass.DrawPrimitives(step.Count, ParticleField.Capacity, step.Start, 0);
                BatchCount++;
                continue;
            }

            if (effect.Compiled.Vertex is { } vertex)
            {
                if (vertex.Samplers.Length > 0)
                    pass.BindVertexSamplers(new TextureSamplerBinding(_targets.DepthRead, _device.PointSampler));
                if (vertex.StorageBuffers > 0)
                    pass.BindVertexStorageBuffers(_globals.Bound(vertex, items, null, built));
                if (vertex.ParamsSize > 0)
                    ShaderParams.Push(commands, vertex, step.Material, step.Values, stage: ParamsStage.Vertex);
            }

            // A pooled stack draws once per item; until its pool (or its BUILD buffers) exists, not at all.
            uint copies = effect.Compiled.Pool is { } pooled ? items is null ? 0u : (uint)pooled.ItemCount : 1u;
            if (effect.Compiled.Build is not null && built is null)
                copies = 0;
            if (_stacks.DrawSlot(s, out uint slot))
                pass.DrawPrimitivesIndirect(_stacks.DrawArgs, slot * StackPasses.DrawArgsBytes, 1);
            else if (copies > 0)
                pass.DrawPrimitives(step.Count, copies, step.Start, 0);
            BatchCount++;
        }

        commands.EndRenderPass(pass);
    }
}
