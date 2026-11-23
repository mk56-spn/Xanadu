using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Playfields;

/// <summary>
/// How close the player has to be to a note for a press to count as being on
/// it: the circle drawn round the player's rect (through its corners) reaches a
/// note as soon as it touches its edge.
/// </summary>
public static class NoteReach
{
    /// <summary>Radius in playfield units, half the body's diagonal; the playfield view scales it to pixels.</summary>
    public static readonly float Radius = PlayRules.BodySize.Length() / 2f;

    /// <summary>The reach on screen for a playfield drawn at <paramref name="scale"/>.</summary>
    public static float OnScreen(float scale) => Radius * scale;

    /// <summary>
    /// Whether the player at <paramref name="playerScreen"/> is in reach of the
    /// note at <paramref name="noteScreen"/>. Both are screen pixels.
    /// </summary>
    public static bool Contains(
        Vector2 playerScreen,
        PlayfieldView view,
        Vector2 noteScreen)
    {
        float radius = OnScreen(view.Scale) + PlayfieldMetrics.NoteSize * view.Scale / 2f;

        return playerScreen.DistanceSquaredTo(noteScreen) <= radius * radius;
    }
}
