using System;

namespace Xanadu.Rendering;

/// <summary>What a blend term multiplies its colour by. The numbers match SDL_GPU's, so the host casts.</summary>
public enum BlendFactor
{
    Zero = 1,
    One,
    SrcColor,
    OneMinusSrcColor,
    DstColor,
    OneMinusDstColor,
    SrcAlpha,
    OneMinusSrcAlpha,
    DstAlpha,
    OneMinusDstAlpha,
    ConstantColor,
    OneMinusConstantColor,
    SrcAlphaSaturate,
}

/// <summary>How the two blend terms combine. The numbers match SDL_GPU's.</summary>
public enum BlendOp
{
    Add = 1,
    Subtract,
    ReverseSubtract,

    /// <summary>Per channel min(src, dst); the factors are ignored.</summary>
    Min,

    /// <summary>Per channel max(src, dst); the factors are ignored.</summary>
    Max,
}

/// <summary>Which channels a draw writes. The bits match SDL_GPU's.</summary>
[Flags]
public enum ColorWrite : byte
{
    None = 0,
    R = 1,
    G = 2,
    B = 4,
    A = 8,
    All = R | G | B | A,
}

/// <summary>
/// The whole fixed-function blend, for tools that want more than
/// <see cref="BlendMode"/>: each colour and alpha term's op and factors, the
/// channel write mask, and the constant the Constant factors read. With
/// <see cref="Enabled"/> off the source simply replaces what is there.
/// </summary>
public readonly record struct CustomBlend(
    bool Enabled,
    BlendOp ColorOp,
    BlendFactor SrcColor,
    BlendFactor DstColor,
    BlendOp AlphaOp,
    BlendFactor SrcAlpha,
    BlendFactor DstAlpha,
    ColorWrite Write,
    Color Constant)
{
    /// <summary>Ordinary alpha blending, the starting point.</summary>
    public static CustomBlend Mix => new(true,
        BlendOp.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha,
        BlendOp.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha,
        ColorWrite.All, new Color(1, 1, 1, 1));

    /// <summary>This blend without its constant: what a pipeline is built from (the constant is set per draw).</summary>
    public CustomBlend PipelineKey => this with { Constant = default };

    /// <summary>Whether a factor reads <see cref="Constant"/>, so the draw must set it.</summary>
    public bool UsesConstant =>
        Enabled && (IsConstant(SrcColor) || IsConstant(DstColor) || IsConstant(SrcAlpha) || IsConstant(DstAlpha));

    private static bool IsConstant(BlendFactor factor) =>
        factor is BlendFactor.ConstantColor or BlendFactor.OneMinusConstantColor;
}
