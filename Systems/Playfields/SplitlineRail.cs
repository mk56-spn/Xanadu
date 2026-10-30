using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Screen layout for the two Splitline rails. The rails are UI-like lanes over
/// the playfield: six key columns, notes scrolling down toward a judgement line
/// near the bottom of the field.
/// </summary>
public static class SplitlineRail
{
    public const float Inset = 72f;
    public const float LaneGap = 28f;
    public const float JudgementLine = 0.80f;
    public const float PixelsPerMs = 0.32f;
    public const float NoteRadius = 15f;

    public static bool IsActive(SplitlineState state, ChannelSide side) =>
        side == ChannelSide.Left ? state.LeftActive : state.RightActive;

    public static Vector2 ScreenPosition(
        PlayfieldView view,
        Playfield playfield,
        ChannelSide side,
        PairIndex index,
        long noteTimeMs,
        long playheadMs)
    {
        Rect2 field = PlayfieldRenderer.FieldRect(view, playfield);
        float sideX = side == ChannelSide.Left
            ? field.Position.X + Inset
            : field.End.X - Inset;
        float lane = (int)index - 2;
        float lineY = field.Position.Y + field.Size.Y * JudgementLine;
        return new Vector2(
            sideX + lane * LaneGap * view.Scale,
            lineY - (noteTimeMs - playheadMs) * PixelsPerMs * view.Scale);
    }

    public static Vector2 ScreenPosition(
        PlayfieldView view,
        Playfield playfield,
        Note note,
        long playheadMs) => ScreenPosition(
        view,
        playfield,
        note.ChannelSide,
        note.PairIndex,
        note.TimeMs,
        playheadMs);

    /// <summary>The six rails' top and bottom extents, for drawing the lane.</summary>
    public static Rect2 LaneBounds(
        PlayfieldView view,
        Playfield playfield,
        ChannelSide side)
    {
        Rect2 field = PlayfieldRenderer.FieldRect(view, playfield);
        float sideX = side == ChannelSide.Left
            ? field.Position.X + Inset
            : field.End.X - Inset;
        float half = LaneGap * view.Scale * 1.5f;
        float lineY = field.Position.Y + field.Size.Y * JudgementLine;
        return new Rect2(
            new Vector2(sideX - half, field.Position.Y + 10f),
            new Vector2(half * 2f, lineY - field.Position.Y - 10f));
    }
}
