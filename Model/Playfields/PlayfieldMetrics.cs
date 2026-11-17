using System;
using Xanadu.Model.Ecs;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Playfields;

/// <summary>The playfield's sizes: how big things are and where they sit on screen, shared by the rules that judge them and the code that draws them.</summary>
public static class PlayfieldMetrics
{
    public const float NoteSize = 72;
    public const float MineSize = 18;

    /// <summary>A wall note's slab, for a note of this radius: narrow and tall.</summary>
    public static Vector2 WallNoteSize(float radius) => new(radius * 0.5f, radius * 2.6f);

    /// <summary>A start point's radius on screen: a note's.</summary>
    public static float StartPointRadius(PlayfieldView view) => NoteSize * view.Scale / 2f;

    public static Rect2 FieldRect(PlayfieldView view, Playfield playfield) =>
        new(
            view.ScreenOrigin,
            new Vector2(playfield.Width * view.Scale, playfield.Height * view.Scale));

    /// <summary>Radius of the approach ring at a 0..1 approach amount.</summary>
    public static float ApproachRadius(float noteRadius, float approach) =>
        noteRadius * (1f + Math.Clamp(approach, 0f, 1f) * 8f);

    public const float NotePopLifetime = 0.32f;

    /// <summary>How much bigger a hit pop grows by the end of its life.</summary>
    private const float PopGrowth = 0.6f;

    /// <summary>The note's radius after <paramref name="t"/> of its pop, 0..1: a fast, soft swell.</summary>
    public static float NotePopRadius(float radius, float t) =>
        radius * (1f + PopGrowth * PopSwell(t));

    /// <summary>How far through its swell a pop is at <paramref name="t"/> of its life, 0..1.</summary>
    public static float PopSwell(float t)
    {
        float clamped = Math.Clamp(t, 0f, 1f);
        return 1f - (1f - clamped) * (1f - clamped);
    }

    /// <summary>Remaining opacity of a pop at <paramref name="t"/> of its life, 0..1.</summary>
    public static float NotePopAlpha(float t) => 1f - Math.Clamp(t, 0f, 1f);

    /// <summary>One, two or three dash lengths.</summary>
    public static int DashStrengthLevel(DashStrength strength) =>
        strength switch
        {
            DashStrength.Double => 2,
            DashStrength.Triple => 3,
            _ => 1,
        };

    /// <summary>The small badge that marks a double or triple dash; empty for a single.</summary>
    public static string DashStrengthLabel(DashStrength strength) =>
        strength switch
        {
            DashStrength.Double => "×2",
            DashStrength.Triple => "×3",
            _ => "",
        };
}
