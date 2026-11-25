using System.Numerics;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// A graph's <c>[Draw]</c> nodes: a grid over the item's quad. Into the item's images (one for each output of its shade),
/// before the job's pass, against the item's own draw depth: the images are the item's box, so the viewport is the target
/// moved by the box's corner, and the fragment gets the corner back to place its pixel on the target. Or onto the frame
/// (an exit), inside the job's pass where the item is placed: blended as its material says, its depth in the item's slice
/// of the canvas depth. Either way its shade reads the item's images at <c>group.pixel</c>, both stages read the graph's
/// buffers, and the grid is drawn once per copy: as many as its call says, or as its count buffer holds (indirect).
/// </summary>
internal sealed class ImageDraws(GraphicsDevice device, FrameTargets targets, GlobalBindings globals, MaterialStore materials)
{
    /// <summary>The images a draw's shade reads, gathered for binding; grown as needed, never shrunk.</summary>
    private Texture[] _read = new Texture[4];

    /// <summary>
    /// A draw's grid over the item's quad into the images the call names. Its depth carries on from the draw before only
    /// when that one wrote the same images; otherwise it starts afresh.
    /// </summary>
    public void Draw(CommandBuffer commands, CanvasEffect effect, BuildBuffers kept, ItemSet set, RestFrame rest, StackBuild.Run call, Step step, float time)
    {
        CompiledDraw draw = effect.Compiled.Build!.Passes[call.Pass].Drawn!;
        int count = 0;
        uint into = 0;
        Span<ColorTargetInfo> colours = stackalloc ColorTargetInfo[DrawOutput.Most];
        Span<TextureFormat> formats = stackalloc TextureFormat[DrawOutput.Most];
        for (; count < DrawOutput.Most && call.Target(count) != StackBuild.Run.None; count++)
        {
            Texture image = set.Images[(int)call.Target(count)];
            colours[count] = new ColorTargetInfo(image, LoadOp.Load);
            formats[count] = image.Format;
            into = (into << 8) | call.Target(count);
        }

        GraphicsPipeline pipeline = effect.DrawPipeline(device, call.Pass, formats[..count], targets.DepthFormat);
        using GpuScope scope = commands.Scope(effect.Compiled.Build!.Passes[call.Pass].Name);
        var z = new DepthStencilTargetInfo
        {
            Texture = set.Z!.Handle,
            ClearDepth = 1f,
            LoadOp = set.DrawnInto == into ? LoadOp.Load : LoadOp.Clear,
            StoreOp = StoreOp.Store,
            StencilLoadOp = LoadOp.DontCare,
            StencilStoreOp = StoreOp.DontCare,
            Cycle = set.DrawnInto != into,
        };
        set.DrawnInto = into;
        RenderPass pass = commands.BeginRenderPass(z, colours[..count]);
        pass.BindGraphicsPipeline(pipeline);
        // The vertex stage places the grid on the target; the target moved by the box's corner lands the box on the images.
        pass.SetViewport(new Viewport(-set.Box.X, -set.Box.Y, targets.Width, targets.Height));
        var corner = new Vector2(set.Box.X, set.Box.Y);
        Bind(commands, pass, draw, kept, set, rest, step, time, corner, new Vector2(0, 1));
        Issue(pass, draw, kept, call);
        commands.EndRenderPass(pass);
    }

    /// <summary>A draw onto the frame, inside the job's pass. False when the item has no images to read (no box).</summary>
    public bool Exit(CommandBuffer commands, RenderPass pass, CanvasEffect effect, ItemSet? set, RestFrame rest, StackBuild.Run call,
        Step step, SampleCount samples, float time)
    {
        CompiledDraw draw = effect.Compiled.Build!.Passes[call.Pass].Drawn!;
        if ((set is null && (draw.Reads.Length > 0 || draw.CoversBox)) || materials.Find(step.Material!) is not { IsNull: false } entity ||
            !entity.TryGetComponent(out BuildBuffers kept))
            return false;
        pass.BindGraphicsPipeline(effect.ExitPipeline(device, call.Pass, step.Material, samples, targets.Format, targets.DepthFormat));
        pass.SetViewport(new Viewport(0, 0, targets.Width, targets.Height));
        pass.SetScissor(step.Clip);
        Bind(commands, pass, draw, kept, set, rest, step, time, Vector2.Zero, new Vector2(step.Depth - step.Span, step.Span));
        Issue(pass, draw, kept, call);
        return true;
    }

    /// <summary>The grid, once per copy: indirect when the GPU counted them, else as many as the call says.</summary>
    private static void Issue(RenderPass pass, CompiledDraw draw, BuildBuffers kept, StackBuild.Run call)
    {
        if (draw.CopiesBuffer < 0)
        {
            if (call.Copies > 0)
                pass.DrawPrimitives(draw.Vertices, call.Copies, 0, 0);
        }
        else if (kept.DrawArgs is { } args)
            pass.DrawPrimitivesIndirect(args, (uint)call.Pass * GraphResources.ArgsBytes, 1);
    }

    /// <summary>Both stages' uniforms, samplers, read images and globals: the grid moved by <paramref name="shift"/>, its z in <paramref name="slice"/>.</summary>
    private void Bind(CommandBuffer commands, RenderPass pass, CompiledDraw draw, BuildBuffers kept, ItemSet? set, RestFrame rest, Step step, float time,
        Vector2 shift, Vector2 slice)
    {
        FrameUniforms frame = FrameUniforms.Now(targets.Width, targets.Height, time);
        // A draw that covers the box is handed the box as its quad.
        DrawQuad quad = draw.CoversBox && set is { Box: var box }
            ? new DrawQuad { Corner = new Vector2(box.X, box.Y), XAxis = new Vector2(box.Width, 0), YAxis = new Vector2(0, box.Height), Slice = slice }
            : new DrawQuad { Corner = rest.Corner, XAxis = rest.XAxis, YAxis = rest.YAxis, Slice = slice };
        commands.PushVertexUniformData(quad);
        commands.PushVertexUniformData(frame, SlangShaders.VertexFrameSlot);
        if (draw.Vertex.ParamsSize > 0)
            ShaderParams.Push(commands, draw.Vertex, step.Material, step.Values, stage: ParamsStage.Vertex);
        commands.PushFragmentUniformData(frame);
        if (draw.Fragment.ParamsSize > 0)
            ShaderParams.Push(commands, draw.Fragment, step.Material, step.Values);
        var origin = set is null ? Vector2.Zero : new Vector2(set.Box.X, set.Box.Y);
        commands.PushFragmentUniformData(new DrawBox
        {
            Shift = shift, Origin = origin, Slice = slice, Corner = quad.Corner, XAxis = quad.XAxis, YAxis = quad.YAxis,
        }, SlangShaders.DrawBoxSlot);
        if (draw.Vertex.StorageBuffers > 0)
            pass.BindVertexStorageBuffers(globals.Bound(draw.Vertex, kept.Buffers));
        // Neither stage gets a texture of its own yet: any sampler it declares sees white.
        Span<TextureSamplerBinding> samplers = stackalloc TextureSamplerBinding[Math.Max(draw.Vertex.Samplers.Length, draw.Fragment.Samplers.Length)];
        for (int k = 0; k < samplers.Length; k++)
            samplers[k] = new TextureSamplerBinding(targets.White, targets.Sampler);
        if (draw.Vertex.Samplers.Length > 0)
            pass.BindVertexSamplers(samplers[..draw.Vertex.Samplers.Length]);
        if (draw.Fragment.Samplers.Length > 0)
            pass.BindFragmentSamplers(samplers[..draw.Fragment.Samplers.Length]);
        if (draw.Reads.Length > 0)
        {
            // The images its shade reads, read-only, after its samplers.
            if (_read.Length < draw.Reads.Length)
                _read = new Texture[draw.Reads.Length];
            for (int k = 0; k < draw.Reads.Length; k++)
                _read[k] = set!.Images[draw.Reads[k]];
            pass.BindFragmentStorageTextures(_read.AsSpan(0, draw.Reads.Length));
        }

        if (draw.Fragment.StorageBuffers > 0)
            pass.BindFragmentStorageBuffers(globals.Bound(draw.Fragment, kept.Buffers));
    }
}
