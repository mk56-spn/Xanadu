using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>
/// Marks storage buffers coherent in compiled SPIR-V. Slang writes <c>globallycoherent</c> as one
/// <c>OpDecorate %buffer Coherent</c> on the buffer's variable, but makes it part of the buffer's
/// type, so a coherent buffer can't be passed to a function taking a plain one (and no parameter
/// may say coherent). The stack's buffers are declared plain for Slang (a library's pass takes
/// them as parameters) and get the decoration here, after: the same SPIR-V Slang would write.
/// </summary>
public static class SpirvCoherence
{
    private const int OpDecorate = 71, DecorationCoherent = 23, DecorationBinding = 33, DecorationDescriptorSet = 34;

    /// <summary>The shader with the variables at these bindings of <paramref name="set"/> decorated Coherent; unchanged when none is there.</summary>
    public static CompiledShader Applied(CompiledShader shader, int set, IReadOnlyCollection<int> bindings)
    {
        if (bindings.Count == 0)
            return shader;
        byte[] patched = Applied(shader.Spirv, set, bindings);
        return ReferenceEquals(patched, shader.Spirv)
            ? shader
            : new CompiledShader(patched, shader.Samplers, shader.UniformBuffers, shader.Params, shader.ParamsSize)
            {
                StateBytes = shader.StateBytes,
                ItemBytes = shader.ItemBytes,
                BufferBytes = shader.BufferBytes,
            };
    }

    /// <summary>The code with the variables at these bindings of <paramref name="set"/> decorated Coherent, each once.</summary>
    public static byte[] Applied(byte[] code, int set, IReadOnlyCollection<int> bindings)
    {
        if (code.Length < 20 || bindings.Count == 0)
            return code;
        uint[] words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(code).ToArray();
        var sets = new Dictionary<uint, uint>();
        var placed = new Dictionary<uint, uint>();
        var coherent = new HashSet<uint>();
        int last = -1;
        for (int i = 5; i < words.Length;)
        {
            int count = (int)(words[i] >> 16), opcode = (int)(words[i] & 0xFFFF);
            if (count == 0)
                break;
            if (opcode == OpDecorate && count >= 3)
            {
                last = i + count;
                if (words[i + 2] == DecorationCoherent)
                    coherent.Add(words[i + 1]);
                else if (count >= 4 && words[i + 2] == DecorationDescriptorSet)
                    sets[words[i + 1]] = words[i + 3];
                else if (count >= 4 && words[i + 2] == DecorationBinding)
                    placed[words[i + 1]] = words[i + 3];
            }

            i += count;
        }

        uint[] targets = [.. placed
            .Where(pair => bindings.Contains((int)pair.Value) && sets.TryGetValue(pair.Key, out uint s) && s == set && !coherent.Contains(pair.Key))
            .Select(pair => pair.Key)
            .Order()];
        if (targets.Length == 0 || last < 0)
            return code;

        // Each new decoration goes after the last one there is: decorations sit together, before the types.
        var result = new List<uint>(words.Length + targets.Length * 3);
        result.AddRange(words.AsSpan(0, last));
        foreach (uint target in targets)
            result.AddRange([(3u << 16) | OpDecorate, target, DecorationCoherent]);
        result.AddRange(words.AsSpan(last));
        return System.Runtime.InteropServices.MemoryMarshal.AsBytes<uint>(result.ToArray()).ToArray();
    }
}
