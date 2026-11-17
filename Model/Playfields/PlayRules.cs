namespace Xanadu.Model.Playfields;

/// <summary>
/// Which game the editor and the play run are: the classic one (every tool,
/// notes, dashes, physics, the character) or the curve rework started on
/// 2026-11-17 (one fixed floor, curve notes, and a hovering blue square that
/// follows them). The classic pieces are unplugged, not deleted: setting this
/// back to true plugs them all in again.
/// </summary>
public static class PlayRules
{
    /// <summary>A field rather than a constant, so the unplugged branches still compile without warnings.</summary>
    public static readonly bool Classic = false;

    /// <summary>The fixed floor's depth, playfield units up from the field's bottom edge.</summary>
    public const float FloorDepth = 300f;

    /// <summary>Where the floor's top is, in playfield units.</summary>
    public static float FloorY(Ecs.Components.Playfield playfield) => playfield.Height - FloorDepth;

    /// <summary>The blue square's side, playfield units.</summary>
    public const float SquareSize = 72f;
}
