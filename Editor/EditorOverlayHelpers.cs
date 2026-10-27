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

    /// <summary>Whether a group's overlay screen is up, which takes over the keyboard and the pointer.</summary>
    private bool IsGroupOverlayOpen()
    {
        foreach (Entity entity in _entities.Query<EditorGroupState>().ToEntityList())
        {
            if (entity.GetComponent<EditorGroupState>().Open >= 0)
                return true;
        }

        return false;
    }

    /// <summary>Whether an effect's move target is being dragged on the playfield.</summary>
    private bool IsGroupTargetEditing()
    {
        foreach (Entity entity in _entities.Query<EditorGroupState>().ToEntityList())
        {
            if (entity.GetComponent<EditorGroupState>().TargetEdit)
                return true;
        }

        return false;
    }

    private void CloseGroupTarget()
    {
        foreach (Entity entity in _entities.Query<EditorGroupState>().ToEntityList())
        {
            ref EditorGroupState state = ref entity.GetComponent<EditorGroupState>();
            state.TargetEdit = false;
            state.DragRow = -1;
        }
    }

    /// <summary>Leaves target dragging and reopens the group's timing overlay.</summary>
    private void ReopenTargetedGroup()
    {
        foreach (Entity entity in _entities.Query<EditorGroupState>().ToEntityList())
        {
            ref EditorGroupState state = ref entity.GetComponent<EditorGroupState>();
            state.TargetEdit = false;
            state.PaletteOpen = false;
            state.DragRow = -1;
            if (state.SelectedGroup >= 0)
                state.Open = state.SelectedGroup;
        }
    }

    private bool IsGroupPaletteOpen()
    {
        foreach (Entity entity in _entities.Query<EditorGroupState>().ToEntityList())
        {
            if (entity.GetComponent<EditorGroupState>().PaletteOpen)
                return true;
        }

        return false;
    }

    private void CloseGroupPalette()
    {
        foreach (Entity entity in _entities.Query<EditorGroupState>().ToEntityList())
        {
            ref EditorGroupState state = ref entity.GetComponent<EditorGroupState>();
            state.PaletteOpen = false;
        }
    }

    /// <summary>Closes the group overlay.</summary>
    private void CloseGroupOverlay()
    {
        foreach (Entity entity in _entities.Query<EditorGroupState>().ToEntityList())
        {
            ref EditorGroupState state = ref entity.GetComponent<EditorGroupState>();
            state.Open = -1;
            state.DragRow = -1;
        }
    }

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
