using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Buffer = MoonWorks.Graphics.Buffer;
using MwColor = MoonWorks.Graphics.Color;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// Drawing copy steps (<see cref="StepKind.Copies"/>): the frame's copies and their values go up once, and each step
/// is one instanced draw of its mesh through the effect's copied form (<see cref="CopiesPipelines"/>), each instance
/// one copy placed, tinted and valued by its own record.
/// </summary>
internal sealed class CopyDraws(GraphicsDevice device, CanvasFrame frame, FrameTargets targets, CanvasPipelines pipelines, CopiesPipelines copies,
    GlobalBindings globals, ExitDraws exits) : IDisposable
{
    /// <summary>SlangCopies' vertex view (uniform 0): the target, the draw's depth and slice, its first copy.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct CopyView
    {
        public float Width, Height, Depth, Span;
        public uint First, Pad0, Pad1, Pad2;
    }

    private readonly GpuArray<GpuCopy> _copies = new(device, "copies", BufferUsageFlags.GraphicsStorageRead, 1 << 8);
    private readonly GpuArray<byte> _values = new(device, "copy values", BufferUsageFlags.GraphicsStorageRead, 1 << 12);
    private Buffer[] _bound = [];

    /// <summary>Sends the frame's copies and their values.</summary>
    public void Upload(CopyPass copy)
    {
        _copies.Ensure(1);
        _values.Ensure(16);
        _copies.Upload(copy, CollectionsMarshal.AsSpan(frame.Copies));
        _values.Upload(copy, CollectionsMarshal.AsSpan(frame.CopyValues));
    }

    /// <summary>One copy step's draw; false when its effect has no copied form after all.</summary>
    public bool Draw(CommandBuffer commands, RenderPass pass, Step step, SampleCount samples, in FrameUniforms frameUniforms)
    {
        CanvasEffect effect = pipelines.Effect(step.Material);
        if (step.Mesh is not { } mesh || mesh.Gpu.Buffer is null || copies.Of(effect, step.Material?.Shader ?? "") is not { } copied)
            return false;

        pass.BindGraphicsPipeline(copies.Pipeline(copied, step.Material, samples));
        if (step.Material?.Custom is { UsesConstant: true } custom)
            pass.SetBlendConstants(new MwColor(custom.Constant.R, custom.Constant.G, custom.Constant.B, custom.Constant.A));
        pass.BindVertexBuffers(mesh.Gpu.Buffer);
        pass.SetScissor(step.Clip);
        exits.BindSamplers(pass, step, copied.Compiled.Fragment.Samplers);

        // Each stage: the globals, then the copies and their values.
        int own = SlangShaders.Globals.Count;
        if (_bound.Length != own + 2)
            _bound = new Buffer[own + 2];
        globals.Bound(own).CopyTo(_bound);
        _bound[own] = _copies.Buffer!;
        _bound[own + 1] = _values.Buffer!;
        pass.BindFragmentStorageBuffers(_bound);
        pass.BindVertexStorageBuffers(_bound);
        if (copied.Compiled.Vertex.Samplers.Length > 0)
            pass.BindVertexSamplers(new TextureSamplerBinding(targets.DepthRead, device.PointSampler));

        commands.PushVertexUniformData(new CopyView
        {
            Width = targets.Width, Height = targets.Height, Depth = step.Depth, Span = step.Span, First = step.Start,
        });
        commands.PushVertexUniformData(frameUniforms, SlangShaders.VertexFrameSlot);
        commands.PushFragmentUniformData(frameUniforms);
        pass.DrawPrimitives((uint)mesh.Vertices.Count, step.Count, 0, 0);
        return true;
    }

    public void Dispose()
    {
        _copies.Dispose();
        _values.Dispose();
    }
}
