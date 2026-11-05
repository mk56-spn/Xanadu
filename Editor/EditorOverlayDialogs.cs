using System;
using System.IO;
using System.Linq;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Editor.Commands;

namespace Xanadu.Editor;

/// <summary>Editor overlay native file dialogs and save/load actions; the song picker is an ECS dialog.</summary>
public partial class EditorOverlay : CanvasLayer
{
    private void BuildSaveDialogs()
    {
        LevelStore.EnsureDirectory();

        _saveAsDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.SaveFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = false,
            Title = "Save level",
            Filters = ["*" + XmfBinary.Extension + " ; XMF Levels"],
            CurrentDir = LevelStore.Directory,
            CurrentFile = "untitled" + XmfBinary.Extension,
        };
        _saveAsDialog.FileSelected += OnSaveAsSelected;
        AddChild(_saveAsDialog);

        _loadDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = false,
            Title = "Load level",
            Filters = ["*" + XmfBinary.Extension + " ; XMF Levels"],
            CurrentDir = LevelStore.Directory,
        };
        _loadDialog.FileSelected += OnLoadSelected;
        AddChild(_loadDialog);
    }

    private void OpenSongDialog() => EditorDialogs.Open(_entities, EditorDialogKind.Song);

    public bool SaveCurrent() => SaveCurrent(out _);

    /// <summary>Saves on the way out through whichever overlay is open, if one is.</summary>
    public static Xanadu.Systems.Scenes.EditorSaveOutcome SaveForLeaving(EditorOverlay? overlay) =>
        overlay?.SaveForLeaving() ??
        Xanadu.Systems.Scenes.EditorSaveOutcome.Failed("the editor is closed");

    private Xanadu.Systems.Scenes.EditorSaveOutcome SaveForLeaving()
    {
        _saveProblem = "";
        return SaveCurrent()
            ? Xanadu.Systems.Scenes.EditorSaveOutcome.Success
            : Xanadu.Systems.Scenes.EditorSaveOutcome.Failed(_saveProblem);
    }

    /// <summary>
    /// Saves the level to its current path, or to a fresh unnamed one, and
    /// reports where it wrote so a caller can keep using that path.
    /// </summary>
    private bool SaveCurrent(out string path)
    {
        path = "";

        // A rebuilt scene replaces the entity this overlay was opened on.
        if (_editor.IsNull)
            _editor = FindEditor();

        if (_editor.IsNull)
        {
            _saveProblem = "no level is open";
            return false;
        }

        if (!ValidateCurrent())
            return false;

        try
        {
            LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
            string target = CurrentLevelPath();
            if (string.IsNullOrEmpty(target))
                target = LevelStore.NextUnnamedPath();

            LevelStore.Save(document, target);
            _commands.Enqueue(EditorCommand.SetLevelFile(target));
            SetStatus($"Saved {Path.GetFileName(target)}");
            path = target;
            return true;
        }
        catch (Exception exception)
        {
            _saveProblem = exception.Message;
            SetStatus($"Save failed: {exception.Message}");
            return false;
        }
    }

    private void OpenSaveAsDialog()
    {
        if (_editor.IsNull || !ValidateCurrent())
            return;

        LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
        string current = CurrentLevelPath();
        _saveAsDialog.CurrentDir = LevelStore.Directory;
        _saveAsDialog.CurrentFile = string.IsNullOrEmpty(current)
            ? Path.GetFileName(LevelStore.DefaultPath(document.Name))
            : Path.GetFileName(current);
        _saveAsDialog.PopupCentered(new Vector2I(900, 600));
    }

    private void OnSaveAsSelected(string path)
    {
        if (_editor.IsNull || !ValidateCurrent())
            return;

        try
        {
            LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
            if (!path.EndsWith(XmfBinary.Extension, StringComparison.OrdinalIgnoreCase))
                path += XmfBinary.Extension;

            LevelStore.Save(document, path);
            _commands.Enqueue(EditorCommand.SetLevelFile(path));
            SetStatus($"Saved {Path.GetFileName(path)}");
        }
        catch (Exception exception)
        {
            SetStatus($"Save failed: {exception.Message}");
        }
    }

    private void OpenLoadDialog()
    {
        LevelStore.EnsureDirectory();
        _loadDialog.CurrentDir = LevelStore.Directory;
        _loadDialog.PopupCentered(new Vector2I(900, 600));
    }

    private void OnLoadSelected(string path)
    {
        if (_editor.IsNull)
            return;

        try
        {
            LevelDocument document = LevelStore.Load(path);
            _commands.Enqueue(EditorCommand.ApplyLevel(document, path));

            SetStatus($"Loaded {Path.GetFileName(path)}");
        }
        catch (Exception exception)
        {
            SetStatus($"Load failed: {exception.Message}");
        }
    }

}
