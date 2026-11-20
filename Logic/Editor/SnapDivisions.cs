namespace Xanadu.Logic.Editor;

/// <summary>
/// The beat snap divisors the editor offers, as in osu!: the straight ones
/// (a row of their own in the music menu), then the triplets.
/// </summary>
public static class SnapDivisions
{
    public static readonly (int Division, string Label)[] Offered =
    {
        (1, "1/1"),
        (2, "1/2"),
        (4, "1/4"),
        (8, "1/8"),
        (3, "1/3"),
        (6, "1/6"),
        (12, "1/12"),
    };

    /// <summary>How many of <see cref="Offered"/> are straight; the rest are triplets.</summary>
    public const int Straight = 4;
}
