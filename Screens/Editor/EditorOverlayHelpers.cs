using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Logic.Editor;

namespace Xanadu.Screens.Editor;

/// <summary>Editor overlay shared helpers.</summary>
public sealed partial class EditorOverlay
{
    private string CurrentLevelPath() =>
        _editor.HasComponent<LevelFile>() ? _editor.GetComponent<LevelFile>().Path : "";

    private bool ValidateCurrent()
    {
        if (_editor.IsNull)
            return false;

        LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
        List<string> issues = LevelValidator.Validate(document);

        if (issues.Count == 0)
            return true;

        _saveProblem = issues.Count == 1 ? issues[0] : $"{issues[0]} (+{issues.Count - 1} more)";
        SetStatus(_saveProblem);

        return false;
    }

    /// <summary>Whether a dialog has the keyboard: a native file dialog or an ECS one.</summary>
    private bool AnyDialogVisible() =>
        NativeDialogVisible() || EditorDialogs.AnyOpen(_entities);

    /// <summary>
    ///     Whether a native file dialog is up. It is a window of its own, so the
    ///     pointer is held off the ECS UI while it is; an ECS dialog needs the pointer.
    /// </summary>
    private bool NativeDialogVisible() => FileDialogOpen?.Invoke() ?? false;

    /// <summary>Whether the host's file picker is up; the pointer is held off the ECS UI while it is.</summary>
    public Func<bool>? FileDialogOpen { get; set; }

    private void SetStatus(string message)
    {
        Entity editor = FindEditor();
        if (editor.IsNull || !editor.HasComponent<EditorHud>())
            return;

        ref EditorHud hud = ref editor.GetComponent<EditorHud>();
        hud.SetStatus(message);
    }

    private bool IsWarningOpen() => _entities.Query<WarningPopup>().Count > 0;

    private Entity FindFocusedTextField()
    {
        foreach (Entity entity in _entities.Query<TextField>().AllTags(Tags.Get<TextFieldFocused>()).ToEntityList())
            return entity;

        return default;
    }

    private Entity FindEditor()
    {
        foreach (Entity entity in _entities.Query<PlayfieldView>().ToEntityList())
            return entity;

        return default;
    }
}
