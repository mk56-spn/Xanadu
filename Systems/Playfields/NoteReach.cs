using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// How close the player has to be to a note for a press to count as being on
/// it. Notes are checked against a sphere around the player rather than the
/// character's damage box, which is far too thin to stand on a note reliably.
/// Damage is unaffected: mines still use the box.
/// </summary>
public static class NoteReach
{
    /// <summary>Radius in playfield units; the playfield view scales it to pixels.</summary>
    public const float Radius = 100f;

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
        float radius = OnScreen(view.Scale);
        return playerScreen.DistanceSquaredTo(noteScreen) <= radius * radius;
    }
}
