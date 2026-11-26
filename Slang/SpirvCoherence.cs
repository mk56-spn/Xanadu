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

    private const int OpTypePointer = 32, OpFunction = 54, OpLoad = 61, OpStore = 62, StorageClassPhysicalStorageBuffer = 5349;
    private const uint MemoryAccessVolatile = 1;

    /// <summary>
    /// The code with every load and store through a buffer pointer (a list reached by address, <see cref="ListReach"/>)
    /// marked Volatile: what Coherent is for a bound buffer, under the GLSL450 memory model Slang writes. A pointer has no
    /// variable to decorate, so the accesses carry it.
    /// </summary>
    public static byte[] Pointers(byte[] code)
    {
        if (code.Length < 20)
            return code;
        uint[] words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(code).ToArray();
        var types = new HashSet<uint>();
        var pointers = new HashSet<uint>();
        var result = new List<uint>(words.Length + 64);
        result.AddRange(words.AsSpan(0, 5));
        bool body = false;
        for (int i = 5; i < words.Length;)
        {
            int count = (int)(words[i] >> 16), opcode = (int)(words[i] & 0xFFFF);
            if (count == 0)
                break;
            body |= opcode == OpFunction;
            if (opcode == OpTypePointer && count >= 4 && words[i + 2] == StorageClassPhysicalStorageBuffer)
                types.Add(words[i + 1]);
            // In a function body a type id only ever stands at word 1 as a result type: what it makes is a pointer.
            else if (body && count >= 3 && types.Contains(words[i + 1]))
                pointers.Add(words[i + 2]);

            int through = opcode == OpLoad ? 3 : opcode == OpStore ? 1 : 0;
            if (through > 0 && count > through && pointers.Contains(words[i + through]))
            {
                int mask = opcode == OpLoad ? 4 : 3;
                if (count > mask)
                {
                    result.AddRange(words.AsSpan(i, count));
                    result[^(count - mask)] |= MemoryAccessVolatile;
                }
                else
                {
                    result.Add(((uint)(count + 1) << 16) | (uint)opcode);
                    result.AddRange(words.AsSpan(i + 1, count - 1));
                    result.Add(MemoryAccessVolatile);
                }
            }
            else
                result.AddRange(words.AsSpan(i, count));
            i += count;
        }

        return System.Runtime.InteropServices.MemoryMarshal.AsBytes<uint>(result.ToArray()).ToArray();
    }
}
