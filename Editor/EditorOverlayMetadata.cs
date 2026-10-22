using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Editor.Commands;
using Writer.Systems.Editor;

namespace Writer.Editor;

/// <summary>Editor overlay metadata panel and field handlers.</summary>
public partial class EditorOverlay : CanvasLayer
{
    private void BuildMetadataDropdown(
        PlayfieldView view,
        LevelInfo level,
        Playhead playhead)
    {
        const float width = 380;
        const float height = 300;
        float x = Mathf.Max(24, view.ScreenSize.X - 24 - width);

        _metadataPanel = new Panel
        {
            Position = new Vector2(x, EditorLayout.HeaderHeight + 8),
            Size = new Vector2(width, height),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color("#111827"),
            BorderColor = new Color("#475569"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ShadowColor = new Color(0, 0, 0, 0.35f),
            ShadowSize = 8,
        };
        _metadataPanel.AddThemeStyleboxOverride("panel", style);
        _header.AddChild(_metadataPanel);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        _metadataPanel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        _nameEdit = AddDropdownField(box, "Level name", level.Name);
        _nameEdit.TextChanged += OnNameChanged;

        _artistEdit = AddDropdownField(box, "Artist", level.Artist);
        _artistEdit.TextChanged += OnArtistChanged;

        _difficultyEdit = AddDropdownField(box, "Difficulty", level.Difficulty);
        _difficultyEdit.TextChanged += OnDifficultyChanged;

        var timing = new HBoxContainer();
        timing.AddThemeConstantOverride("separation", 8);
        box.AddChild(timing);

        _bpmEdit = AddDropdownField(timing, "BPM", playhead.Bpm.ToString());
        _bpmEdit.TextChanged += OnBpmChanged;

        _offsetEdit = AddDropdownField(
            timing,
            "Offset (ms)",
            playhead.OffsetMs.ToString());
        _offsetEdit.TextChanged += OnOffsetChanged;

    }

    private void ToggleMetadata()
    {
        if (_metadataPanel.Visible)
            HideMetadata();
        else
            ShowMetadata();
    }

    private void ShowMetadata()
    {
        _metadataPanel.Visible = true;
        _metadataCatcher.Visible = true;
    }

    private void HideMetadata()
    {
        if (GetViewport().GuiGetFocusOwner() is LineEdit)
            GetViewport().GuiReleaseFocus();

        _metadataPanel.Visible = false;
        _metadataCatcher.Visible = false;
    }

    private void OnMetadataCatcherInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton
            {
                Pressed: true,
                ButtonIndex: MouseButton.Left,
            })
        {
            return;
        }

        HideMetadata();
        _suppressPointerUntilRelease = true;
        _metadataCatcher.AcceptEvent();
    }

    private void OnNameChanged(string value)
    {
        if (_syncing || _editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.SetName(value));
    }

    private void OnArtistChanged(string value)
    {
        if (_syncing || _editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.SetArtist(value));
    }

    private void OnDifficultyChanged(string value)
    {
        if (_syncing || _editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.SetDifficulty(value));
    }

    private void OnBpmChanged(string value)
    {
        if (_syncing || _editor.IsNull || !int.TryParse(value, out int bpm))
            return;

        _commands.Enqueue(EditorCommand.SetBpm(bpm));
    }

    private void OnOffsetChanged(string value)
    {
        if (_syncing || _editor.IsNull || !long.TryParse(value, out long offset))
            return;

        _commands.Enqueue(EditorCommand.SetOffset(offset));
    }
}
