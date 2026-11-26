using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// The graph's compute nodes, each a compute pass of its own (SDL_GPU orders storage writes only between passes):
/// a schedule's <c>[Pass]</c> and <c>[Pixels]</c> calls.
/// </summary>
/// <remarks>
/// A pixel node runs over the item's box on the target (or an image's texels), a thread a pixel, in tiles, on that
/// item's images (<see cref="ItemSet"/>).
/// </remarks>
internal sealed class ComputeNodes(GraphicsDevice device, FrameTargets targets, GlobalBindings globals)
{
    /// <summary>A schedule's <c>[Pass]</c> call over elements or groups, once for the material, on its buffers and its own images.</summary>
    public void Pass(CommandBuffer commands, CanvasEffect effect, BuildBuffers kept, Texture?[]? shared, StackBuild.Run call,
        CanvasMaterial material, float dt, float time)
    {
        CompiledPass pass = effect.Compiled.Build!.Passes[call.Pass];
        using GpuScope scope = commands.Scope(pass.Name);
        commands.PushComputeUniformData(new PassUniforms
        {
            Dt = dt, Time = time, Over = call.Over, Beat = ShaderClock.Beat, Arguments = call.Values,
        });
        if (pass.Shader.ParamsSize > 0 && pass.Shader.UniformBuffers > 1)
            ShaderParams.Push(commands, pass.Shader, material, null, stage: ParamsStage.Compute);
        ComputePass compute = Begin(commands, shared ?? [], pass.Images, kept.Buffers, pass.Buffers, effect.Passes[call.Pass]!, pass.Shader);
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
        ComputePass compute = Begin(commands, set.Images, pass.Images, kept.Buffers, pass.Buffers, effect.Passes[call.Pass]!, pass.Shader);
        // The canvas so far: its depth and the screen copy, fresh when the pass reads them (it then runs mid-frame); then
        // the images it reads through sampled(), filtered by the GPU.
        Span<TextureSamplerBinding> samplers = stackalloc TextureSamplerBinding[SlangShaders.PixelSamplers + pass.Sampled.Length];
        samplers[0] = new TextureSamplerBinding(targets.DepthRead, device.PointSampler);
        samplers[1] = new TextureSamplerBinding(targets.Screen, targets.Sampler);
        for (int k = 0; k < pass.Sampled.Length; k++)
            samplers[SlangShaders.PixelSamplers + k] = new TextureSamplerBinding(set.Images[pass.Sampled[k]], targets.Sampler);
        compute.BindSamplers(samplers);
        (int tileWidth, int tileHeight) = pass.Tile!.Value;
        compute.Dispatch((width + (uint)tileWidth - 1) / (uint)tileWidth, (height + (uint)tileHeight - 1) / (uint)tileHeight, 1);
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

    /// <summary>A pass begun with the images and buffers it touches read-write, and the globals read-only.</summary>
    private ComputePass Begin(CommandBuffer commands, Texture?[] images, int[] imagePlaces, Buffer[] buffers, int[] bufferPlaces,
        ComputePipeline pipeline, CompiledShader shader)
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
            compute.BindStorageBuffers(globals.Bound(shader.StorageBuffers));
        return compute;
    }
}
