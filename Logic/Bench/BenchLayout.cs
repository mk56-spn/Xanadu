namespace Xanadu.Logic.Bench;

/// <summary>Where things stand on the bench stage, shared by the scene that builds it and the system that runs it.</summary>
public static class BenchLayout
{
    /// <summary>The player box, in stage pixels: 2.5 tall to 1 wide.</summary>
    public static readonly Vector2 PlayerSize = new(40, 100);

    /// <summary>How far down the stage the surface's top sits (0 = top, 1 = bottom).</summary>
    public const float SurfaceAt = 0.75f;

    /// <summary>How high above the surface the player box hovers, centre to surface.</summary>
    public const float HoverHeight = 220;

    /// <summary>Where the player box stands across the stage (0 = left, 1 = right); the world scrolls past it.</summary>
    public const float PlayerAt = 0.3f;

    public const float NoteRadius = 22;
}
