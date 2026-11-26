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
/// the canvas and blended as the item is placed (a present is one of those, lowered to a draw: PresentDraws); a shade
/// draws the item's own triangles, placed by its deform.
/// </summary>
internal sealed class ExitDraws(GraphicsDevice device, FrameTargets targets, CanvasPipelines pipelines, Dictionary<ulong, Texture> textures,
    CanvasParticles particles, MaterialStore materials, GlobalBindings globals, GridCutter grids, GpuArray<CanvasVertex> vertices)
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ViewUniforms { public float Width, Height, Depth, Span; }

    /// <summary>The unit last pushed with the frame in this pass: a run with another one pushes the frame again.</summary>
    private float _pushedUnit = 1;

    /// <summary>particle_mesh.slang's <c>Place</c>: vertex uniform buffer 1 of a particle-mesh draw.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PlaceUniforms
    {
        public float OriginX, OriginY;
        public uint First, Pad1;
        public float XX, XY, YX, YY;
    }

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
    /// The stack's triangles at step <paramref name="s"/>. <paramref name="clocked"/> says the frame's clocks are already
    /// pushed for an effect in this pass, and is set once they are. False when nothing was drawn.
    /// </summary>
    public bool Shade(CommandBuffer commands, RenderPass pass, Step step, CanvasEffect effect, SampleCount samples,
        in FrameUniforms frame, ref bool clocked)
    {
        commands.PushVertexUniformData(new ViewUniforms { Width = targets.Width, Height = targets.Height, Depth = step.Depth, Span = step.Span });
        FrameUniforms drawn = frame.For(step.Values);
        if (!clocked || drawn.Unit != _pushedUnit)
        {
            // A deforming effect's vertex stage reads the frame too: its clocks and the item's unit.
            commands.PushVertexUniformData(drawn, SlangShaders.VertexFrameSlot);
            commands.PushFragmentUniformData(drawn);
            _pushedUnit = drawn.Unit;
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

        // After the globals: the graph's buffers it reads.
        Buffer[]? built = effect.Compiled.Build is null ? null : materials.Built(step.Material);
        if (effect.Compiled.StorageBuffers > 0)
            pass.BindFragmentStorageBuffers(globals.Bound(effect.Compiled, built));
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
                pass.BindVertexStorageBuffers(globals.Bound(vertex, built));
            if (vertex.ParamsSize > 0)
                ShaderParams.Push(commands, vertex, step.Material, step.Values, stage: ParamsStage.Vertex);
        }

        // Until its graph's buffers exist, not at all.
        if (effect.Compiled.Build is not null && built is null)
            return false;
        pass.DrawPrimitives(step.Count, 1, step.Start, 0);
        return true;
    }

    /// <summary>The shade's samplers: the item's texture, then by name the screen copy, the canvas depth or the material's textures.</summary>
    internal void BindSamplers(RenderPass pass, Step step, string[] names)
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
}
