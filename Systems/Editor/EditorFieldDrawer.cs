using Xanadu.Ecs;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>The editor's background, the field and its grid, and the labels on it.</summary>
public sealed class EditorFieldDrawer(EditorDrawSurface surface)
{
    private static readonly Color EditorBackground = Palette.Stage;

    public void Draw(EditorFrame frame)
    {
        PlayfieldView view = frame.View;
        Playfield playfield = frame.Playfield;
        surface.Canvas.DrawRect(
            surface.Target,
            new Rect2(Vector2.Zero, view.ScreenSize),
            EditorBackground);

        Rect2 area = EditorViewport.Area(view.ScreenSize);
        Rect2 field = PlayfieldRenderer.FieldRect(view, playfield);
        if (frame.Outside is { } around)
            surface.Canvas.DrawRect(surface.Target, area, around);
        bool grid = ViewportGrid.IsVisible(surface.Entities);
        if (grid)
            PlayfieldRenderer.DrawOuterGrid(surface.Canvas, surface.Target, playfield, field, area);
        PlayfieldRenderer.DrawFieldFrame(surface.Canvas, surface.Target, field, view.Scale, frame.Frame);
        PlayfieldRenderer.DrawField(surface.Canvas, surface.Target, field, 1, frame.Background, frame.Frame);
        if (grid)
            PlayfieldRenderer.DrawGrid(surface.Canvas, surface.Target, playfield, field, area);
        PlayfieldRenderer.DrawFieldLabels(surface.Canvas, surface.Target, playfield, field);
    }
}
