using System.Linq;
using MoonWorks.Graphics;
using Xanadu.Rendering.Compute;
using Xanadu.Slang;

namespace Xanadu.Rendering;

/// <summary>An effect's compute pipelines, made from its compile: the update, the pool's step, the graph's passes, the trace.</summary>
internal static class EffectCompute
{
    /// <summary>A kept-state effect's update: the globals read-only, the state read-write, one thread.</summary>
    public static ComputePipeline? Update(GraphicsDevice device, CompiledShader compiled) => compiled.Update is { } step
        ? ComputePipelines.Create(device, step.Spirv, "update brick", new ComputePipelineCreateInfo
        {
            Format = ShaderFormat.SPIRV,
            NumReadonlyStorageBuffers = (uint)step.StorageBuffers,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = step.UniformBuffers,
            ThreadCountX = 1, ThreadCountY = 1, ThreadCountZ = 1,
        })
        : null;

    /// <summary>A pool's step: the values are uniform 1, the globals (and kept state) its read-only inputs after the jobs.</summary>
    public static ComputeOperation<Passes.PoolJob>? Pool(GraphicsDevice device, CompiledShader compiled) => compiled.Pool is { } step
        ? new ComputeOperation<Passes.PoolJob>(device, step.Spirv, inputs: (uint)(step.StorageBuffers - 1),
            groupsPerJob: (uint)((step.ItemCount + SlangShaders.PoolThreads - 1) / SlangShaders.PoolThreads), uniforms: step.UniformBuffers)
        : null;

    /// <summary>
    /// A graph's passes: the globals (and kept state) read-only, the images then the buffers each touches read-write; a
    /// pixel pass's threads are its tile.
    /// </summary>
    public static ComputePipeline[] Passes(GraphicsDevice device, CompiledShader compiled) => compiled.Build is { } build
        ? [.. build.Passes.Select(pass => ComputePipelines.Create(device, pass.Shader.Spirv, $"build pass {pass.Name}", new ComputePipelineCreateInfo
        {
            Format = ShaderFormat.SPIRV,
            NumReadonlyStorageBuffers = (uint)pass.Shader.StorageBuffers,
            NumReadWriteStorageTextures = (uint)pass.Images.Length,
            NumReadWriteStorageBuffers = (uint)pass.Buffers.Length,
            NumUniformBuffers = Math.Max(1, pass.Shader.UniformBuffers),
            ThreadCountX = (uint)(pass.Tile?.Width ?? pass.Threads), ThreadCountY = (uint)(pass.Tile?.Height ?? 1), ThreadCountZ = 1,
        }))]
        : [];

    /// <summary>A TRACE brick: the depth to read, the globals (and kept state) read-only; the two textures it fills and the buffers it touches read-write.</summary>
    public static ComputePipeline? Trace(GraphicsDevice device, CompiledShader compiled) => compiled.Trace is { } traced
        ? ComputePipelines.Create(device, traced.Shader.Spirv, "trace brick", new ComputePipelineCreateInfo
        {
            Format = ShaderFormat.SPIRV,
            NumSamplers = 1,
            NumReadonlyStorageBuffers = (uint)traced.Shader.StorageBuffers,
            NumReadWriteStorageTextures = 2,
            NumReadWriteStorageBuffers = (uint)traced.Buffers.Length,
            NumUniformBuffers = SlangShaders.TraceUniforms,
            ThreadCountX = (uint)traced.Width, ThreadCountY = (uint)traced.Height, ThreadCountZ = 1,
        })
        : null;
}
