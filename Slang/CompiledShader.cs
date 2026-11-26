using System.Collections.Generic;

namespace Xanadu.Slang;

/// <summary>One value an effect reads from its <c>Params</c> block: where it sits, how big, and its default.</summary>
/// <param name="IsBool">A <c>uniform bool</c>: an on/off switch, held as 0 or 1 (and written as an int, like <paramref name="IsInt"/>).</param>
/// <param name="Rows">A matrix's rows (1 for a scalar or vector); each row takes 16 bytes of the block, as Slang lays them out.</param>
/// <param name="Columns">A vector's channels or a matrix's columns (1 for a scalar).</param>
/// <param name="IsUnsigned">A <c>uint</c> (or uint vector): never below zero.</param>
public sealed record ShaderParam(string Name, int Offset, int Size, bool IsInt, Vector4 Default, bool IsBool = false,
    int Rows = 1, int Columns = 1, bool IsUnsigned = false)
{
    /// <summary>Tagged <c>[Fit]</c>: an item fitting its shader fills x and y with its shape's half size.</summary>
    public bool Fit { get; init; }

    /// <summary>Tagged <c>[Options("a|b|c")]</c>: the names of a whole-number value's choices, 0 first; null when untagged.</summary>
    public string[]? Options { get; init; }

    /// <summary>A <c>floatRxC</c>: rows of <see cref="Columns"/>, 16 bytes apart.</summary>
    public bool IsMatrix => Rows > 1;
}

/// <summary>
/// A compiled entry point and what reflection says it binds: how many
/// samplers and uniform buffers (for <c>ShaderCreateInfo</c>), the sampler
/// names by binding, and the layout of uniform buffer 1 (an effect's values).
/// </summary>
public sealed class CompiledShader(byte[] spirv, string[] samplers, uint uniformBuffers, ShaderParam[] parameters, int paramsSize, int storageBuffers = 0)
{
    public byte[] Spirv { get; } = spirv;

    /// <summary>Sampler names by binding index in the stage's sampler set; a gap is null.</summary>
    public string[] Samplers { get; } = samplers;

    public uint UniformBuffers { get; } = uniformBuffers;

    public ShaderParam[] Params { get; } = parameters;

    /// <summary>Read-only storage buffers: every library global (after the samplers in a fragment stage), then the stack's buffers it reads.</summary>
    public int StorageBuffers { get; } = storageBuffers;

    /// <summary>Bytes in one element of every structured buffer it declares, by name: a BUILD brick's buffers are sized from it.</summary>
    public IReadOnlyDictionary<string, int> BufferBytes { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// The stack's BUILD buffers this stage reads, by their place in <see cref="StackBuild.Buffers"/>: bound in
    /// that order after its own storage buffers (counted in <see cref="StorageBuffers"/>), unless <see cref="Reached"/>.
    /// </summary>
    public int[] StackBuffers { get; init; } = [];

    /// <summary>
    /// Its lists are pointers (<see cref="ListReach"/>): none of <see cref="StackBuffers"/> is bound, and the host pushes
    /// every list's address at uniform <see cref="ListReach.Slot"/>.
    /// </summary>
    public bool Reached { get; init; }

    /// <summary>A stack's graph, compiled: its resources, its passes and draws, and their schedule; null without one. Set on the fragment.</summary>
    public CompiledBuild? Build { get; init; }

    /// <summary>
    /// An effect's own vertex stage, from its <c>deform</c>; null for the host's standard one.
    /// Its values block is the fragment's, as vertex uniform buffer 1; the frame is buffer 2,
    /// and the globals, the storage buffers of set 0.
    /// </summary>
    public CompiledShader? Vertex { get; init; }

    /// <summary>
    /// A deforming effect's <c>[Grid(columns, rows)]</c>: the cells each rect it draws is cut into;
    /// null for four corners. Set on <see cref="Vertex"/>.
    /// </summary>
    public (int Columns, int Rows)? Grid { get; init; }

    /// <summary>Whether a deform is <c>[Translucent]</c>: its triangles test the depth and never write it. Set on <see cref="Vertex"/>.</summary>
    public bool Translucent { get; init; }

    /// <summary>Bytes in the values block, padded to 16; 0 when the effect has none.</summary>
    public int ParamsSize { get; } = paramsSize;

    /// <summary>
    /// Whether the compiled code really samples the screen copy (fragment
    /// sampler 1). Reflection lists every declared global, used or not; the
    /// SPIR-V keeps only what the entry point reaches.
    /// </summary>
    public bool ReadsScreen { get; } = SpirvScan.Binds(spirv, set: 2, binding: 1);

    /// <summary>
    /// A uniform as a BUILD schedule reads it: <paramref name="set"/> when the material sets it,
    /// else its default, whole for an int; null for a name that isn't one of its values.
    /// </summary>
    public BuildValue? ScheduleValue(string name, Vector4? set)
    {
        foreach (ShaderParam param in Params)
        {
            if (param.Name != name)
                continue;
            Vector4 value = set ?? param.Default;
            return param.IsInt ? BuildValue.Of((long)MathF.Round(value.X)) : new BuildValue(value.X, false);
        }

        return null;
    }

    /// <summary>
    /// Whether the compiled code really samples the depth: fragment sampler 2, or, for a
    /// deforming effect's vertex stage, vertex sampler 0 (set when it is compiled).
    /// </summary>
    public bool ReadsDepth { get; init; }

    /// <summary>
    /// The body as it was compiled (lowered, colours converted), for its copied form (<see cref="SlangCopies"/>);
    /// null for a graph, an overlay, or a stage that isn't an effect's fragment.
    /// </summary>
    public string? CopiedSource { get; init; }
}
