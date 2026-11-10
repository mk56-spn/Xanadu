using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>The editor's panels and the masks that keep a panned field out of them.</summary>
public sealed class EditorChromeDrawer(EditorDrawSurface surface)
{
    private static readonly Color EditorBackground = Palette.Stage;
    private static readonly Color PanelBackground = Palette.Surface;

    /// <summary>Covers everything above and below the field area with the editor's background.</summary>
    public void DrawMasks(PlayfieldView view, Rect2 area)
    {
        surface.Canvas.DrawRect(
            surface.Target,
            new Rect2(0, 0, view.ScreenSize.X, area.Position.Y),
            EditorBackground);
        surface.Canvas.DrawRect(
            surface.Target,
            new Rect2(0, area.End.Y, view.ScreenSize.X, view.ScreenSize.Y - area.End.Y),
            EditorBackground);
    }

    public void Draw(PlayfieldView view)
    {
        Rect2 header = EditorLayout.HeaderRect(view);
        surface.Canvas.DrawRect(surface.Target, header, PanelBackground);
        surface.Canvas.DrawRect(
            surface.Target,
            new Rect2(header.Position.X, header.End.Y - 1, header.Size.X, 1),
            PlayfieldRenderer.BorderColor);

        Rect2 footer = EditorLayout.FooterRect(view);
        surface.Canvas.DrawRect(surface.Target, footer, PanelBackground);
        surface.Canvas.DrawRect(
            surface.Target,
            new Rect2(footer.Position.X, footer.Position.Y, footer.Size.X, 1),
            PlayfieldRenderer.BorderColor);

        DrawZoomLens(view);
    }

    private void DrawZoomLens(PlayfieldView view)
    {
        Vector2 center = EditorLayout.ZoomLensCenter(view);
        Color color = PlayfieldRenderer.DimTextColor;
        const float radius = 5f;

        surface.Canvas.DrawLine(
            surface.Target,
            center + new Vector2(-radius, -radius),
            center + new Vector2(radius, -radius),
            color,
            1);
        surface.Canvas.DrawLine(
            surface.Target,
            center + new Vector2(radius, -radius),
            center + new Vector2(radius, radius),
            color,
            1);
        surface.Canvas.DrawLine(
            surface.Target,
            center + new Vector2(radius, radius),
            center + new Vector2(-radius, radius),
            color,
            1);
        surface.Canvas.DrawLine(
            surface.Target,
            center + new Vector2(-radius, radius),
            center + new Vector2(-radius, -radius),
            color,
            1);
        surface.Canvas.DrawLine(
            surface.Target,
            center + new Vector2(4, 4),
            center + new Vector2(9, 9),
            color,
            2);
    }
}
