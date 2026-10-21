using Godot;
using Writer.Ecs;

namespace Writer.Systems.Editor;

/// <summary>Shared editor playfield and ruler geometry.</summary>
public static class EditorLayout
{
    public const float RulerGap = 10;
    public const float RulerHeight = 50;

    public static Rect2 PlayfieldRect(EditorView view, Playfield playfield) =>
        new(
            view.ScreenOrigin,
            new Vector2(playfield.Width * view.Scale, playfield.Height * view.Scale));

    public static Rect2 RulerRect(EditorView view, Playfield playfield)
    {
        Rect2 field = PlayfieldRect(view, playfield);
        return new Rect2(
            field.Position.X,
            field.End.Y + RulerGap,
            field.Size.X,
            RulerHeight);
    }
}
