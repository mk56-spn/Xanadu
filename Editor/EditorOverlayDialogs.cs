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
        _songPicker = new ConfirmationDialog
        {
            Title = "Choose a song",
            OkButtonText = "SELECT",
        };
        var box = new VBoxContainer();
        _songPicker.AddChild(box);
        _songEmpty = new Label
        {
            Text = "NO SONGS",
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        box.AddChild(_songEmpty);
        _songList = new ItemList
        {
            CustomMinimumSize = new Vector2(620, 360),
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _songList.ItemSelected += OnSongHighlighted;
        box.AddChild(_songList);
        _songInfo = new Label { Text = "" };
        box.AddChild(_songInfo);
        _songImport = new Button { Text = "IMPORT SONG..." };
        _songImport.Pressed += OpenSongImportDialog;
        box.AddChild(_songImport);
        _songPicker.Confirmed += OnSongChoiceConfirmed;
        AddChild(_songPicker);

        _songDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = false,
            Title = "Import a song",
            Filters = ["*.mp3, *.wav, *.ogg, *.flac ; Audio Files"],
        };
        _songDialog.FileSelected += OnSongSelected;
        AddChild(_songDialog);
    }

    private void OpenSongDialog()
    {
        if (_songPicker.Visible)
            return;

        RefreshSongList();
        _songPicker.PopupCentered(new Vector2I(700, 540));
    }

    private void OpenSongImportDialog() => _songDialog.PopupCentered(new Vector2I(900, 600));

    private void RefreshSongList()
    {
        _songs.Clear();
        _songs.AddRange(new SongStore().LoadAll());
        _songList.Clear();
        foreach (SongDocument song in _songs)
        {
            string artist = string.IsNullOrWhiteSpace(song.Artist) ? "Unknown artist" : song.Artist;
            _songList.AddItem($"{song.Title}   ·   {artist}");
        }

        bool empty = _songs.Count == 0;
        _songEmpty.Visible = empty;
        _songList.Visible = !empty;
        _songInfo.Text = empty
            ? "No songs in the library. Import one first."
            : "Select a song, then choose SELECT.";
        _songPicker.GetOkButton().Disabled = empty;
        if (!empty)
            _songList.Select(0);
    }

    private void OnSongHighlighted(long index)
    {
        if (index < 0 || index >= _songs.Count)
            return;
        SongDocument song = _songs[(int)index];
        _songInfo.Text = $"{song.Bpm} BPM   ·   offset {song.OffsetMs} ms   ·   {song.Slug}";
    }

    private void OnSongChoiceConfirmed()
    {
        int index = _songList.GetSelectedItems().Length > 0
            ? _songList.GetSelectedItems()[0]
            : -1;
        if (index < 0 || index >= _songs.Count || _editor.IsNull)
            return;
        _commands.Enqueue(EditorCommand.LoadSong(_songs[index].AudioPath));
    }

    public bool SaveCurrent()
    {
        if (_editor.IsNull || !ValidateCurrent())
            return false;

        try
        {
            LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
            string path = CurrentLevelPath();
            if (string.IsNullOrEmpty(path))
                path = LevelStore.DefaultPath(document.Name);

            LevelStore.Save(document, path);
            _commands.Enqueue(EditorCommand.SetLevelFile(path));
            SetStatus($"Saved {Path.GetFileName(path)}");
            return true;
        }
        catch (Exception exception)
        {
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
        try
        {
            new SongStore().Import(path);
            RefreshSongList();
            _songPicker.PopupCentered(new Vector2I(700, 540));
        }
        catch (Exception exception)
        {
            SetStatus($"Song import failed: {exception.Message}");
        }
    }
}
