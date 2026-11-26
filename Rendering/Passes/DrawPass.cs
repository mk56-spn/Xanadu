using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Slang;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// Drawing a job into its target: the opaque pre-pass first, then every step in order with the depth test on. An
/// effect's step runs its graph's exits (<see cref="ExitDraws"/>) at its place; a graph with mid-frame nodes ends the
/// pass for them and starts it again, and a screen read ends it to copy the target.
/// </summary>
internal sealed class DrawPass(CanvasFrame frame, FrameTargets targets, PrimitivePipelines primitives,
    CanvasPipelines pipelines, CanvasStack canvas, PrimitivePool pool, Dictionary<ulong, Texture> textures,
    CanvasParticles particles, GraphRunner graphs, ExitDraws exits, CopyDraws copies)
{
    private const int Slots = PrimitivePipelines.Slots;

    [StructLayout(LayoutKind.Sequential)]
    private struct DrawUniforms
    {
        public float Width, Height;
        public uint First, Pad;
    }

    private readonly List<Step> _steps = frame.Steps;
    private readonly List<ulong> _stepTextures = frame.StepTextures;
    private readonly FrameTargets _targets = targets;
    private readonly PrimitivePipelines _primitives = primitives;
    private readonly CanvasPipelines _pipelines = pipelines;
    private readonly CanvasStack _canvas = canvas;
    private readonly PrimitivePool _pool = pool;
    private readonly CanvasParticles _particles = particles;
    private readonly GraphRunner _graphs = graphs;
    private readonly ExitDraws _exits = exits;
    private readonly Dictionary<ulong, Texture> _textures = textures;

    /// <summary>Draw calls and screen copies since the counts were last cleared.</summary>
    public int BatchCount, CopyCount;

    private void DrawPrimitives(CommandBuffer commands, RenderPass pass, SampleCount samples, bool opaque, uint first, uint count, int firstTexture)
    {
        pass.BindGraphicsPipeline(_primitives.Get(opaque, samples));
        pass.BindVertexStorageBuffers(opaque ? _canvas.Opaque : _canvas.Placed);
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
            if (effect.Compiled.ReadsDepth || effect.Compiled.Vertex?.ReadsDepth == true || effect.Compiled.Build?.Passes.Any(pass => pass.ReadsDepth) == true)
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
            pass.BindVertexStorageBuffers(_canvas.Opaque);
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
        bool copied = false, clocked = false;
        for (int s = job.FirstStep; s < job.FirstStep + job.StepCount; s++)
        {
            Step step = _steps[s];
            switch (step.Kind)
            {
                case StepKind.Primitives:
                    DrawPrimitives(commands, pass, samples, opaque: false, step.Start, step.Count, step.FirstTexture);
                    clocked = false;
                    continue;

                case StepKind.Particles:
                    pass.SetScissor(step.Clip);
                    _particles.Draw(commands, pass, step.Particles!, step.Transform, step.Color, _targets.Width, _targets.Height,
                        _textures.GetValueOrDefault(step.Texture) ?? _targets.White, _targets.Sampler, samples, step.Depth);
                    BatchCount++;
                    clocked = false;
                    continue;
            }

            CanvasEffect effect = _pipelines.Effect(step.Material);
            // The first screen read copies the target and every later read in the pass shares that copy.
            if (effect.Compiled.ReadsScreen && !copied)
            {
                copied = true;
                commands.EndRenderPass(pass);
                _targets.CopyScreen(commands, job.Target);
                CopyCount++;
                pass = _targets.BeginPass(commands, job.Target, job.Clear, msaa, load: true);
                clocked = false;
            }

            // Copies draw through the effect's copied form: no graph, every value per copy.
            if (step.Kind == StepKind.Copies)
            {
                if (copies.Draw(commands, pass, step, samples, frame))
                    BatchCount++;
                clocked = false;
                continue;
            }

                        // A particle mesh borrows the effect's shade only; its graph ran with the material's own items.
            if (step.Kind == StepKind.MeshParticles)
            {
                if (_exits.Shade(commands, pass, step, effect, samples, frame, ref clocked))
                    BatchCount++;
                continue;
            }

            GraphPlan plan = _graphs.Plan(step.Material, effect);
            if (plan.BreaksPass)
            {
                // Mid-frame nodes read the canvas so far: the pass ends, they run, and it starts again.
                commands.EndRenderPass(pass);
                if (plan.ReadsScreen)
                {
                    _targets.CopyScreen(commands, job.Target);
                    CopyCount++;
                    copied = true;
                }

                _graphs.MidFrame(commands, plan, effect, step, s);
                pass = _targets.BeginPass(commands, job.Target, job.Clear, msaa, load: true);
                clocked = false;
            }

            foreach (GraphNode node in plan.Nodes)
            {
                if (node.Kind == NodeKind.Shade && _exits.Shade(commands, pass, step, effect, samples, frame, ref clocked))
                    BatchCount++;
                else if (node is { Kind: NodeKind.Draw, Place: NodePlace.Exit } && step.Mesh is { } mesh && step.Start < mesh.Vertices.Count)
                {
                    if (_graphs.Draws.Exit(commands, pass, effect, _graphs.Images(s), mesh.Vertices[(int)step.Start].Rest, node.Call, step, samples,
                            frame.Time))
                        BatchCount++;
                    clocked = false;
                }
            }
        }

        commands.EndRenderPass(pass);
    }
}
