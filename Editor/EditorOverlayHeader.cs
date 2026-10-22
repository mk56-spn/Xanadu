using Godot;
using Writer.Ecs;
using Writer.Systems.Editor;

namespace Writer.Editor;

/// <summary>Editor overlay header and level metadata summary.</summary>
public partial class EditorOverlay : CanvasLayer
{
    private void BuildHeader(PlayfieldView view, LevelInfo level)
    {
        _header = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = Vector2.Zero,
            Size = new Vector2(view.ScreenSize.X, EditorLayout.HeaderHeight),
        };
        _root.AddChild(_header);

        float infoWidth = Mathf.Max(160, view.ScreenSize.X - 670);

        _levelLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(level.Name) ? "Untitled" : level.Name,
            Position = new Vector2(144, 4),
            Size = new Vector2(infoWidth, 28),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _levelLabel.AddThemeFontSizeOverride("font_size", 22);
        _levelLabel.AddThemeColorOverride("font_color", new Color("#facc15"));
        _header.AddChild(_levelLabel);

        _songLabel = new Label
        {
            Text = "Song: No song loaded",
            Position = new Vector2(144, 32),
            Size = new Vector2(infoWidth, 18),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _songLabel.AddThemeFontSizeOverride("font_size", 14);
        _songLabel.AddThemeColorOverride("font_color", new Color("#cbd5e1"));
        _header.AddChild(_songLabel);

        _statusLabel = new Label
        {
            Text = "Space play   Home stop   Delete note   ←/→ nudge   Ctrl+Z/Y undo   T tap tempo   Target: select a note",
            Position = new Vector2(144, 50),
            Size = new Vector2(infoWidth, 18),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 12);
        _statusLabel.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        _header.AddChild(_statusLabel);

    }
}
