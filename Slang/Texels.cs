using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>What an image's channels hold, for packing: images of one kind can share a texture's channels.</summary>
public enum TexelKind
{
    Float,
    Uint,
    Int,
}

/// <summary>
/// One texel format an image can have: what <c>Image&lt;T&gt;</c> it is for, the format Slang names, how many channels
/// it has, of which kind and how many bits each. The one table every reader of formats goes through.
/// </summary>
public sealed record Texel(string Type, string Format, int Channels, TexelKind Kind, int Bits, string? Precision = null)
{
    /// <summary>
    /// Every format: <c>float</c>, <c>float2</c> and <c>float4</c> at 16 bits a channel by default (<c>float</c> at 32, as a
    /// depth wants), <c>[Precise]</c> at 32, <c>[Compact]</c> smaller; uints and ints at 32.
    /// </summary>
    public static readonly IReadOnlyList<Texel> All =
    [
        new("float", "r32f", 1, TexelKind.Float, 32),
        new("float", "r32f", 1, TexelKind.Float, 32, "Precise"),
        new("float", "r16f", 1, TexelKind.Float, 16, "Compact"),
        new("float2", "rg16f", 2, TexelKind.Float, 16),
        new("float2", "rg32f", 2, TexelKind.Float, 32, "Precise"),
        new("float2", "rg8", 2, TexelKind.Float, 8, "Compact"),
        new("float4", "rgba16f", 4, TexelKind.Float, 16),
        new("float4", "rgba32f", 4, TexelKind.Float, 32, "Precise"),
        new("float4", "rgba8", 4, TexelKind.Float, 8, "Compact"),
        new("uint", "r32ui", 1, TexelKind.Uint, 32),
        new("uint2", "rg32ui", 2, TexelKind.Uint, 32),
        new("uint4", "rgba32ui", 4, TexelKind.Uint, 32),
        new("int", "r32i", 1, TexelKind.Int, 32),
        new("int2", "rg32i", 2, TexelKind.Int, 32),
        new("int4", "rgba32i", 4, TexelKind.Int, 32),
    ];

    /// <summary>The types an image may hold, in order.</summary>
    public static IEnumerable<string> Types => All.Select(texel => texel.Type).Distinct();

    /// <summary>The format for <c>Image&lt;type&gt;</c> with <paramref name="precision"/> (<c>Precise</c>, <c>Compact</c> or null); null when there is none.</summary>
    public static Texel? Of(string type, string? precision) => All.FirstOrDefault(texel => texel.Type == type && texel.Precision == precision);

    /// <summary>The texel a format names; null for one this table doesn't know.</summary>
    public static Texel? Named(string format) => All.FirstOrDefault(texel => texel.Format == format && texel.Precision is null)
                                                ?? All.FirstOrDefault(texel => texel.Format == format);

    /// <summary>The formats an image holding <paramref name="type"/> may have: what a draw's output of that type can be written into.</summary>
    public static string[] FormatsFor(string type) => [.. All.Where(texel => texel.Type == type).Select(texel => texel.Format).Distinct()];

    /// <summary>The type with <paramref name="channels"/> channels of <paramref name="kind"/>: float2, uint4 and so on.</summary>
    public static string TypeOf(TexelKind kind, int channels)
    {
        string scalar = kind switch { TexelKind.Uint => "uint", TexelKind.Int => "int", _ => "float" };
        return channels == 1 ? scalar : scalar + channels;
    }

    /// <summary>
    /// The format of a texture packing channels of <paramref name="kind"/>, <paramref name="channels"/> of them used (3 takes
    /// a four-channel texture), at the most bits any member wants.
    /// </summary>
    public static string PackedFormat(TexelKind kind, int channels, int bits)
    {
        int held = channels == 3 ? 4 : channels;
        string shape = held switch { 1 => "r", 2 => "rg", _ => "rgba" };
        return kind switch
        {
            TexelKind.Uint => shape + "32ui",
            TexelKind.Int => shape + "32i",
            _ => shape + (bits > 16 ? "32f" : "16f"),
        };
    }
}
