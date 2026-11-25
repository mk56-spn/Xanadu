using System.Linq;
using MoonWorks.Graphics;
using Xanadu.Rendering.Compute;
using Xanadu.Slang;
using Shader = MoonWorks.Graphics.Shader;

namespace Xanadu.Rendering;

/// <summary>An effect's graph pipelines, made from its compile: its compute passes and its draws' stages.</summary>
internal static class EffectCompute
{
    /// <summary>
    /// A graph's passes: the globals (and kept state) read-only, the images then the buffers each touches read-write; a
    /// pixel pass's threads are its tile.
    /// </summary>
    public static ComputePipeline?[] Passes(GraphicsDevice device, CompiledShader compiled) => compiled.Build is { } build
        ? [.. build.Passes.Select(pass => pass.Drawn is not null ? null : ComputePipelines.Create(device, pass.Shader.Spirv, $"build pass {pass.Name}", new ComputePipelineCreateInfo
        {
            Format = ShaderFormat.SPIRV,
            NumSamplers = pass.Tile is null ? 0u : SlangShaders.PixelSamplers,
            NumReadonlyStorageBuffers = (uint)pass.Shader.StorageBuffers,
            NumReadWriteStorageTextures = (uint)pass.Images.Length,
            NumReadWriteStorageBuffers = (uint)pass.Buffers.Length,
            NumUniformBuffers = Math.Max(1, pass.Shader.UniformBuffers),
            ThreadCountX = (uint)(pass.Tile?.Width ?? pass.Threads), ThreadCountY = (uint)(pass.Tile?.Height ?? 1), ThreadCountZ = 1,
        }))]
        : [];

    /// <summary>A graph's draws' vertex and fragment stages, by their places among its passes; null for a compute pass.</summary>
    public static (Shader Vertex, Shader Fragment)?[] Draws(GraphicsDevice device, CompiledShader compiled) => compiled.Build is { } build
        ? [.. build.Passes.Select(pass => pass.Drawn is { } drawn ? ((Shader, Shader)?)(Stage(device, drawn.Vertex, ShaderStage.Vertex), Stage(device, drawn.Fragment, ShaderStage.Fragment, drawn.Reads.Length)) : null)]
        : [];

    private static Shader Stage(GraphicsDevice device, CompiledShader stage, ShaderStage kind, int images = 0) => Shader.Create(device, stage.Spirv, "main", new ShaderCreateInfo
    {
        Format = ShaderFormat.SPIRV,
        Stage = kind,
        NumSamplers = (uint)stage.Samplers.Length,
        NumStorageTextures = (uint)images,
        NumStorageBuffers = (uint)stage.StorageBuffers,
        NumUniformBuffers = stage.UniformBuffers,
    });
}
