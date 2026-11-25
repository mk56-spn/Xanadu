using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>Just enough SPIR-V reading to see which resources survived compilation.</summary>
public static class SpirvScan
{
    private const int OpDecorate = 71, DecorationBinding = 33, DecorationDescriptorSet = 34;

    /// <summary>Whether any variable is decorated with this descriptor set, at any binding.</summary>
    public static bool BindsSet(byte[] code, int set) => Bound(code).Any(place => place.Set == set);

    /// <summary>Whether any variable is decorated with this descriptor set and binding.</summary>
    public static bool Binds(byte[] code, int set, int binding) => Bound(code).Contains(((uint)set, (uint)binding));

    private const int OpFunction = 54, OpFunctionParameter = 55, OpFunctionCall = 57, OpLoad = 61, OpStore = 62, OpCopyMemory = 63,
        OpAccessChain = 65, OpInBoundsAccessChain = 66, OpPtrAccessChain = 67, OpCopyObject = 83, OpBitcast = 124, OpSelect = 169,
        OpAtomicLoad = 227, OpAtomicStore = 228, OpPhi = 245,
        OpImageTexelPointer = 60, OpImageRead = 98, OpImageWrite = 99;

    /// <summary>
    /// How the code really uses the buffer at this set and binding: whether anything loads from
    /// it, and whether anything stores to it (an atomic does both). Follows the pointers made from
    /// it (access chains, copies, selects, phis, function parameters) to where they're used. A
    /// binding that isn't there, or is only bound (its length asked), is neither. For a storage
    /// <paramref name="image"/>, loading it only hands the image on: reads and writes are the
    /// image ops on what was loaded (and atomics through a texel pointer).
    /// </summary>
    public static GraphAccess Access(byte[] code, int set, int binding, bool image = false)
    {
        if (code.Length < 20)
            return 0;
        ReadOnlySpan<uint> words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(code);
        var sets = new Dictionary<uint, uint>();
        var bindings = new Dictionary<uint, uint>();
        var parameters = new Dictionary<uint, List<uint>>();
        uint function = 0;
        for (int i = 5; i < words.Length;)
        {
            int count = (int)(words[i] >> 16), opcode = (int)(words[i] & 0xFFFF);
            if (count == 0)
                break;
            if (opcode == OpDecorate && count >= 4 && words[i + 2] == DecorationDescriptorSet)
                sets[words[i + 1]] = words[i + 3];
            else if (opcode == OpDecorate && count >= 4 && words[i + 2] == DecorationBinding)
                bindings[words[i + 1]] = words[i + 3];
            else if (opcode == OpFunction)
                parameters[function = words[i + 2]] = [];
            else if (opcode == OpFunctionParameter)
                parameters[function].Add(words[i + 2]);
            i += count;
        }

        // The pointers into the buffer: its variable, then everything made from one, until nothing new turns up.
        var into = new HashSet<uint>();
        foreach ((uint id, uint value) in bindings)
            if (value == binding && sets.TryGetValue(id, out uint s) && s == set)
                into.Add(id);
        if (into.Count == 0)
            return 0;

        GraphAccess access = 0;
        for (bool grew = true; grew;)
        {
            grew = false;
            access = 0;
            for (int i = 5; i < words.Length;)
            {
                int count = (int)(words[i] >> 16), opcode = (int)(words[i] & 0xFFFF);
                if (count == 0)
                    break;
                switch (opcode)
                {
                    case OpAccessChain or OpInBoundsAccessChain or OpPtrAccessChain or OpCopyObject or OpBitcast when into.Contains(words[i + 3]):
                        grew |= into.Add(words[i + 2]);
                        break;
                    case OpSelect when into.Contains(words[i + 4]) || into.Contains(words[i + 5]):
                        grew |= into.Add(words[i + 2]);
                        break;
                    case OpPhi:
                        for (int k = i + 3; k < i + count; k += 2)
                            if (into.Contains(words[k]))
                                grew |= into.Add(words[i + 2]);
                        break;
                    case OpFunctionCall when parameters.TryGetValue(words[i + 3], out List<uint>? called):
                        for (int k = 0; k < called.Count && i + 4 + k < i + count; k++)
                            if (into.Contains(words[i + 4 + k]))
                                grew |= into.Add(called[k]);
                        break;
                    case OpLoad when image && into.Contains(words[i + 3]):
                        grew |= into.Add(words[i + 2]);
                        break;
                    case OpImageTexelPointer when into.Contains(words[i + 3]):
                        grew |= into.Add(words[i + 2]);
                        break;
                    case OpImageRead when into.Contains(words[i + 3]):
                        access |= GraphAccess.Read;
                        break;
                    case OpImageWrite when into.Contains(words[i + 1]):
                        access |= GraphAccess.Write;
                        break;
                    case OpLoad or OpAtomicLoad when into.Contains(words[i + 3]):
                        access |= GraphAccess.Read;
                        break;
                    case OpStore or OpAtomicStore when into.Contains(words[i + 1]):
                        access |= GraphAccess.Write;
                        break;
                    case OpCopyMemory:
                        if (into.Contains(words[i + 1]))
                            access |= GraphAccess.Write;
                        if (into.Contains(words[i + 2]))
                            access |= GraphAccess.Read;
                        break;
                    // Every other atomic (exchange, compare-exchange, add, min, max, and, or, xor, the flags, the float ones) reads and writes.
                    case >= 229 and <= 242 or 318 or 5614 or 5615 or 6035 when count > 3 && into.Contains(words[i + 3]):
                        access |= GraphAccess.ReadWrite;
                        break;
                    // OpAtomicFlagClear names its pointer first.
                    case 319 when into.Contains(words[i + 1]):
                        access |= GraphAccess.Write;
                        break;
                }

                i += count;
            }
        }

        return access;
    }

    /// <summary>Every (set, binding) a variable is decorated with.</summary>
    private static List<(uint Set, uint Binding)> Bound(byte[] code)
    {
        var places = new List<(uint, uint)>();
        if (code.Length < 20)
            return places;

        var sets = new Dictionary<uint, uint>();
        var bindings = new Dictionary<uint, uint>();
        ReadOnlySpan<uint> words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(code);
        for (int i = 5; i < words.Length;)
        {
            int count = (int)(words[i] >> 16), opcode = (int)(words[i] & 0xFFFF);
            if (count == 0)
                break;
            if (opcode == OpDecorate && count >= 4)
            {
                if (words[i + 2] == DecorationDescriptorSet)
                    sets[words[i + 1]] = words[i + 3];
                else if (words[i + 2] == DecorationBinding)
                    bindings[words[i + 1]] = words[i + 3];
            }

            i += count;
        }

        foreach ((uint id, uint value) in bindings)
            if (sets.TryGetValue(id, out uint s))
                places.Add((s, value));
        return places;
    }
}
