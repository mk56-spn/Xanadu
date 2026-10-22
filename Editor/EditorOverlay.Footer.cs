using Godot;
using Writer.Ecs;
using Writer.Editor.Commands;
using Writer.Systems.Editor;

namespace Writer.Editor;

/// <summary>Editor overlay transport footer.</summary>
public partial class EditorOverlay : CanvasLayer
{
    private void BuildFooter(PlayfieldView view)
    {
        Rect2 footer = EditorLayout.FooterRect(view);
        _footer = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = footer.Position,
            Size = footer.Size,
        };
        _root.AddChild(_footer);

        _timeLabel = new Label
        {
            Text = "00:00.000 / 00:00.000",
            Position = new Vector2(24, 4),
            Size = new Vector2(300, 16),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _timeLabel.AddThemeFontSizeOverride("font_size", 14);
        _timeLabel.AddThemeColorOverride("font_color", new Color("#cbd5e1"));
        _footer.AddChild(_timeLabel);

        _notesLabel = new Label
        {
            Text = "0 notes",
            Position = new Vector2(view.ScreenSize.X - 24 - 240, 4),
            Size = new Vector2(240, 16),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _notesLabel.AddThemeFontSizeOverride("font_size", 13);
        _notesLabel.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        _footer.AddChild(_notesLabel);
    }
}
