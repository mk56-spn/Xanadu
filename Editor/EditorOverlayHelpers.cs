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

        SetStatus(issues.Count == 1
            ? issues[0]
            : $"{issues[0]} (+{issues.Count - 1} more)");

        return false;
    }

    private bool AnyDialogVisible() =>
        (_songPicker?.Visible ?? false) ||
        (_saveAsDialog?.Visible ?? false) ||
        (_loadDialog?.Visible ?? false);

    private bool IsWarningOpen()
    {
        foreach (Entity entity in _entities.Query<WarningPopup>().ToEntityList())
        {
            if (entity.GetComponent<WarningPopup>().Visible)
                return true;
        }

        return false;
    }

    private Entity FindEditor()
    {
        foreach (Entity entity in _entities.Query<PlayfieldView>().ToEntityList())
            return entity;

        return default;
    }
}
