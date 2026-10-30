using System;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Where the editor shows the playfield and how the view moves. A field up to
/// a thousand units opens whole, as it always did; a bigger one opens at that
/// same scale on its bottom middle, where the player starts, and is panned
/// (middle drag) and zoomed (Ctrl+wheel, about the pointer) from there, or
/// fitted whole (Home). Every editor tool maps the pointer through the
/// <see cref="PlayfieldView"/>, so moving the view moves them all with it.
/// </summary>
public static class EditorViewport
{
    /// <summary>The most of the field, in units, the editor opens on.</summary>
    public const float OpenSize = 1000f;

    /// <summary>The closest the view zooms in: two screen pixels per unit.</summary>
    public const float MaxScale = 2f;

    /// <summary>How far out the view zooms, as a share of the scale that fits the whole field.</summary>
    public const float MinFitShare = 0.5f;

    /// <summary>One Ctrl+wheel notch zooms by this factor.</summary>
    public const float WheelStep = 1.15f;

    /// <summary>The screen area between the header and the transport row the field is shown in.</summary>
    public static Rect2 Area(Vector2 screen)
    {
        float top = EditorLayout.HeaderHeight + EditorLayout.Margin;
        float bottom = screen.Y -
                       EditorLayout.FooterHeight -
                       EditorLayout.TransportHeight -
                       EditorLayout.Gap -
                       EditorShellMetrics.StatusLineHeight -
                       EditorLayout.Margin;
        return new Rect2(
            EditorLayout.Margin,
            top,
            Math.Max(1, screen.X - EditorLayout.Margin * 2),
            Math.Max(1, bottom - top));
    }

    /// <summary>The view a field opens in.</summary>
    public static PlayfieldView Open(Vector2 screen, Playfield playfield)
    {
        Rect2 area = Area(screen);
        float shown = Math.Max(1, Math.Min(OpenSize, Math.Max(playfield.Width, playfield.Height)));
        float scale = Math.Min(shown, Math.Min(area.Size.X, area.Size.Y)) / shown;
        Vector2 size = new Vector2(playfield.Width, playfield.Height) * scale;
        var origin = new Vector2(
            area.Position.X + (area.Size.X - size.X) / 2,
            size.Y <= area.Size.Y
                ? area.Position.Y + (area.Size.Y - size.Y) / 2
                : area.End.Y - size.Y);
        return new PlayfieldView(origin, scale, screen);
    }

    /// <summary>The view that shows the whole field, centred.</summary>
    public static PlayfieldView Fit(Vector2 screen, Playfield playfield)
    {
        Rect2 area = Area(screen);
        float scale = FitScale(area, playfield);
        Vector2 size = new Vector2(playfield.Width, playfield.Height) * scale;
        return new PlayfieldView(area.Position + (area.Size - size) / 2, scale, screen);
    }

    /// <summary>Zooms by <paramref name="factor"/>, keeping the field point under <paramref name="anchor"/> where it is.</summary>
    public static PlayfieldView Zoom(PlayfieldView view, Playfield playfield, float factor, Vector2 anchor)
    {
        float least = FitScale(Area(view.ScreenSize), playfield) * MinFitShare;
        float scale = Math.Clamp(view.Scale * factor, Math.Min(least, MaxScale), MaxScale);
        Vector2 under = view.ScreenToPlayfield(anchor);
        return new PlayfieldView(anchor - under * scale, scale, view.ScreenSize);
    }

    public static PlayfieldView Pan(PlayfieldView view, Vector2 delta) =>
        new(view.ScreenOrigin + delta, view.Scale, view.ScreenSize);

    private static float FitScale(Rect2 area, Playfield playfield) =>
        Math.Min(1f, Math.Min(
            area.Size.X / Math.Max(1, playfield.Width),
            area.Size.Y / Math.Max(1, playfield.Height)));
}
