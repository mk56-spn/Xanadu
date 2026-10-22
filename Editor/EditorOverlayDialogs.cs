using System;
using System.IO;
using Godot;
using Writer.Ecs;
using Writer.Levels;
using Writer.Editor.Commands;

namespace Writer.Editor;

/// <summary>Editor overlay native file dialogs and save/load actions.</summary>
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
            Filters = ["*.json ; Level Files"],
            CurrentDir = LevelStore.Directory,
            CurrentFile = "untitled.json",
        };
        _saveAsDialog.FileSelected += OnSaveAsSelected;
        AddChild(_saveAsDialog);

        _loadDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = false,
            Title = "Load level",
            Filters = ["*.json ; Level Files"],
            CurrentDir = LevelStore.Directory,
        };
        _loadDialog.FileSelected += OnLoadSelected;
        AddChild(_loadDialog);
    }

    private void BuildSongDialog()
    {
        _songDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = false,
            Title = "Load a song",
            Filters = ["*.mp3, *.wav, *.ogg, *.flac ; Audio Files"],
        };

        _songDialog.FileSelected += OnSongSelected;
        AddChild(_songDialog);
    }

    private void OpenSongDialog()
    {
        if (_songDialog.Visible)
            return;

        _songDialog.PopupCentered(new Vector2I(900, 600));
    }

    private void SaveCurrent()
    {
        if (_editor.IsNull || !ValidateCurrent())
            return;

        try
        {
            LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
            string path = CurrentLevelPath();
            if (string.IsNullOrEmpty(path))
                path = LevelStore.DefaultPath(document.Name);

            LevelStore.Save(document, path);
            _commands.Enqueue(EditorCommand.SetLevelFile(path));
            SetStatus($"Saved {Path.GetFileName(path)}");
        }
        catch (Exception exception)
        {
            SetStatus($"Save failed: {exception.Message}");
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
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                path += ".json";

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

    private void OnSongSelected(string path)
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.LoadSong(path));
    }
}
