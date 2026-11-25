using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// A graph's <c>[Draw]</c> node, before the job's pass: a grid over the item's quad into the item's images (one for each
/// output of its shade) against the item's own draw depth. The images are the item's box, so the viewport is the
/// target moved by the box's corner, and the fragment gets the corner back to place its pixel on the target.
/// </summary>
internal sealed class ImageDraws(GraphicsDevice device, FrameTargets targets, GlobalBindings globals)
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct DrawBox { public float X, Y, Pad0, Pad1; }

    /// <summary>The images a draw's shade reads, gathered for binding; grown as needed, never shrunk.</summary>
    private Texture[] _read = new Texture[4];

    /// <summary>A draw's grid over the item's quad into the images the call names; the item's first draw starts its depth afresh.</summary>
    public void Draw(CommandBuffer commands, CanvasEffect effect, ItemSet set, RestFrame rest, StackBuild.Run call, Step step, float time)
    {
        CompiledDraw draw = effect.Compiled.Build!.Passes[call.Pass].Drawn!;
        int count = 0;
        Span<ColorTargetInfo> colours = stackalloc ColorTargetInfo[DrawOutput.Most];
        Span<TextureFormat> formats = stackalloc TextureFormat[DrawOutput.Most];
        for (; count < DrawOutput.Most && call.Target(count) != StackBuild.Run.None; count++)
        {
            Texture image = set.Images[(int)call.Target(count)];
            colours[count] = new ColorTargetInfo(image, LoadOp.Load);
            formats[count] = image.Format;
        }

        GraphicsPipeline pipeline = effect.DrawPipeline(device, call.Pass, formats[..count], targets.DepthFormat);
        using GpuScope scope = commands.Scope(effect.Compiled.Build!.Passes[call.Pass].Name);
        var z = new DepthStencilTargetInfo
        {
            Texture = set.Z!.Handle,
            ClearDepth = 1f,
            LoadOp = set.Drawn ? LoadOp.Load : LoadOp.Clear,
            StoreOp = StoreOp.Store,
            StencilLoadOp = LoadOp.DontCare,
            StencilStoreOp = StoreOp.DontCare,
            Cycle = !set.Drawn,
        };
        set.Drawn = true;
        RenderPass pass = commands.BeginRenderPass(z, colours[..count]);
        pass.BindGraphicsPipeline(pipeline);
        // The vertex stage places the grid on the target; the target moved by the box's corner lands the box on the images.
        pass.SetViewport(new Viewport(-set.Box.X, -set.Box.Y, targets.Width, targets.Height));
        FrameUniforms frame = FrameUniforms.Now(targets.Width, targets.Height, time);
        commands.PushVertexUniformData(new DrawQuad { Corner = rest.Corner, XAxis = rest.XAxis, YAxis = rest.YAxis });
        commands.PushVertexUniformData(frame, SlangShaders.VertexFrameSlot);
        if (draw.Vertex.ParamsSize > 0)
            ShaderParams.Push(commands, draw.Vertex, step.Material, step.Values, stage: ParamsStage.Vertex);
        commands.PushFragmentUniformData(frame);
        if (draw.Fragment.ParamsSize > 0)
            ShaderParams.Push(commands, draw.Fragment, step.Material, step.Values);
        commands.PushFragmentUniformData(new DrawBox { X = set.Box.X, Y = set.Box.Y }, SlangShaders.DrawBoxSlot);
        if (draw.Vertex.StorageBuffers > 0)
            pass.BindVertexStorageBuffers(globals.Bound(draw.Vertex.StorageBuffers));
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
                _read[k] = set.Images[draw.Reads[k]];
            pass.BindFragmentStorageTextures(_read.AsSpan(0, draw.Reads.Length));
        }

        if (draw.Fragment.StorageBuffers > 0)
            pass.BindFragmentStorageBuffers(globals.Bound(draw.Fragment.StorageBuffers));
        pass.DrawPrimitives(draw.Vertices, 1, 0, 0);
        commands.EndRenderPass(pass);
    }
}
