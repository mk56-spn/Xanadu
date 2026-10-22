using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Levels;
using Writer.Systems.Editor;

namespace Writer.Editor;

/// <summary>Editor overlay shared helpers.</summary>
public partial class EditorOverlay : CanvasLayer
{
    private string CurrentLevelPath() =>
        _editor.HasComponent<LevelFile>()
            ? _editor.GetComponent<LevelFile>().Path
            : "";

    private bool ValidateCurrent()
    {
        if (_editor.IsNull)
            return false;

        LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
        List<string> issues = LevelValidator.Validate(document);

        if (issues.Count == 0)
            return true;

        _statusLabel.Text = issues.Count == 1
            ? issues[0]
            : $"{issues[0]} (+{issues.Count - 1} more)";

        return false;
    }

    private static LineEdit AddDropdownField(
        Container parent,
        string labelText,
        string value)
    {
        var field = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        field.AddThemeConstantOverride("separation", 2);

        var label = new Label { Text = labelText };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        field.AddChild(label);

        var edit = new LineEdit
        {
            Text = value,
            CustomMinimumSize = new Vector2(0, 30),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        field.AddChild(edit);
        parent.AddChild(field);
        return edit;
    }

    private static string FormatMs(long milliseconds)
    {
        long value = Math.Max(0, milliseconds);
        return $"{value / 60000:00}:{value / 1000 % 60:00}.{value % 1000:000}";
    }

    private Entity FindEditor()
    {
        foreach (Entity entity in _entities.Query<PlayfieldView>().ToEntityList())
            return entity;

        return default;
    }
}
