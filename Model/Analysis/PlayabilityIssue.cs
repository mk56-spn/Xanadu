namespace Xanadu.Model.Analysis;

public enum PlayabilityProblem
{
    /// <summary>The note is too far sideways from where the player can be.</summary>
    TooFarSideways,

    /// <summary>The note is higher than gravity lets the player stay.</summary>
    TooHigh
}

public readonly record struct PlayabilityIssue(
    long TimeMs,
    Vector2 Position,
    PlayabilityProblem Problem,
    float Shortfall);
