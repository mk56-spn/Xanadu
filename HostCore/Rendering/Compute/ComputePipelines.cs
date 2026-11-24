using MoonWorks.Graphics;

namespace Xanadu.Host.Rendering.Compute;

/// <summary>
/// Compute pipelines that fail loudly. SDL_GPU only checks its binding limits
/// in debug mode (<c>XANADU_GPU_DEBUG=1</c>): without it, a pipeline over a
/// limit is still made and its dispatches silently do nothing. And when
/// creation does fail, MoonWorks logs and hands back null. Both throw here,
/// naming the pipeline.
/// </summary>
internal static class ComputePipelines
{
    /// <summary>SDL_GPU's cap on read-write storage buffers, and on read-write storage textures, per compute pipeline.</summary>
    public const uint MaxReadWrite = 8;

    public static ComputePipeline Create(GraphicsDevice device, ReadOnlySpan<byte> spirv, string name, in ComputePipelineCreateInfo info)
    {
        if (info.NumReadWriteStorageBuffers > MaxReadWrite)
            throw new InvalidOperationException(
                $"Compute pipeline '{name}' binds {info.NumReadWriteStorageBuffers} read-write storage buffers; SDL_GPU allows {MaxReadWrite}. Share buffers (a ComputeArena) or bind read-only ones as read-only.");
        if (info.NumReadWriteStorageTextures > MaxReadWrite)
            throw new InvalidOperationException(
                $"Compute pipeline '{name}' binds {info.NumReadWriteStorageTextures} read-write storage textures; SDL_GPU allows {MaxReadWrite}.");
        return ComputePipeline.Create(device, spirv, "main", info)
            ?? throw new InvalidOperationException($"Compute pipeline '{name}' could not be created (see the SDL error logged above).");
    }
}
