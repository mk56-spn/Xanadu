namespace Xanadu.Rendering;

/// <summary>
/// How a draw's pixels combine with what is already on its target
/// (Docs/render-architecture.md D17). Static: a level may set it, but it is not
/// keyable. <see cref="Mix"/> is ordinary alpha blending; the rest are the
/// blend modes Godot's canvas items offer.
/// </summary>
public enum BlendMode
{
    Mix,
    Add,
    Sub,
    Mul,

    /// <summary>For drawing a texture whose pixels are already premultiplied.</summary>
    Premultiplied,
}

/// <summary>The blend modes as a list tools can step through, with their readout names.</summary>
public static class BlendModes
{
    /// <summary>The modes in the order the inspector steps through them.</summary>
    public static readonly BlendMode[] All =
    [
        BlendMode.Mix,
        BlendMode.Add,
        BlendMode.Sub,
        BlendMode.Mul,
        BlendMode.Premultiplied,
    ];

    /// <summary>The mode <paramref name="step"/> along the list, wrapping round.</summary>
    public static BlendMode Step(BlendMode mode, int step)
    {
        int index = System.Array.IndexOf(All, mode);
        if (index < 0)
            index = 0;
        int count = All.Length;
        return All[((index + step) % count + count) % count];
    }

    /// <summary>The mode's name for a readout.</summary>
    public static string Name(BlendMode mode) => mode switch
    {
        BlendMode.Add => "ADD",
        BlendMode.Sub => "SUB",
        BlendMode.Mul => "MUL",
        BlendMode.Premultiplied => "PREMUL",
        _ => "MIX",
    };
}
