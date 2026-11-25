namespace Xanadu.Slang;

/// <summary>What SPIR-V does with storage images, beside <see cref="SpirvScan"/>'s bindings.</summary>
public static class SpirvImages
{
    private const int OpImageWrite = 99;

    /// <summary>Whether the code writes any storage image.</summary>
    public static bool Writes(byte[] code)
    {
        if (code.Length < 20)
            return false;
        ReadOnlySpan<uint> words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(code);
        for (int i = 5; i < words.Length;)
        {
            int count = (int)(words[i] >> 16);
            if (count == 0)
                break;
            if ((words[i] & 0xFFFF) == OpImageWrite)
                return true;
            i += count;
        }

        return false;
    }
}
