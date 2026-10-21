using System;
using System.IO;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;

namespace Writer.Editor;

/// <summary>Native controls for song selection and editor metadata.</summary>
public partial class EditorOverlay : CanvasLayer
{
    private readonly EntityStore _entities;
    private readonly EditorHistory _history;

    private Control _root = null!;
    private FileDialog _songDialog = null!;
    private FileDialog _saveAsDialog = null!;
    private FileDialog _loadDialog = null!;
    private LineEdit _nameEdit = null!;
    private LineEdit _bpmEdit = null!;
    private LineEdit _offsetEdit = null!;
    private OptionButton _snapOption = null!;
    private Button _playButton = null!;
    private Label _statusLabel = null!;
    private Entity _editor;

    private bool _syncing;

    public EditorOverlay(EntityStore entities, EditorHistory history)
    {
        _entities = entities;
        _history = history;
        Layer = 100;
    }

    public override void _Ready()
    {
        _editor = FindEditor();

        _root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        BuildUi();
        BuildSongDialog();
        BuildSaveDialogs();

        CallDeferred(nameof(OpenSongDialog));
    }

    public override void _Process(double delta)
    {
        if (_editor.IsNull)
            return;

        ref EditorPlayhead playhead = ref _editor.GetComponent<EditorPlayhead>();
        ref LevelInfo level = ref _editor.GetComponent<LevelInfo>();

        _playButton.Text = playhead.Playing ? "Pause" : "Play";

        if (!_nameEdit.HasFocus() && _nameEdit.Text != level.Name)
        {
            _syncing = true;
            _nameEdit.Text = level.Name;
            _syncing = false;
        }

        string bpm = playhead.Bpm.ToString();
        if (!_bpmEdit.HasFocus() && _bpmEdit.Text != bpm)
        {
            _syncing = true;
            _bpmEdit.Text = bpm;
            _syncing = false;
        }

        string offset = playhead.OffsetMs.ToString();
        if (!_offsetEdit.HasFocus() && _offsetEdit.Text != offset)
        {
            _syncing = true;
            _offsetEdit.Text = offset;
            _syncing = false;
        }

        int snapIndex = playhead.SnapDivision switch
        {
            8 => 1,
            16 => 2,
            _ => 0,
        };

        if (_snapOption.Selected != snapIndex)
        {
            _syncing = true;
            _snapOption.Selected = snapIndex;
            _syncing = false;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        if (key.CtrlPressed && key.Keycode == Key.S)
        {
            SaveCurrent();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.CtrlPressed && key.Keycode == Key.Z)
        {
            Undo();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.CtrlPressed && key.Keycode == Key.Y)
        {
            Redo();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (GetViewport().GuiGetFocusOwner() is LineEdit)
            return;

        switch (key.Keycode)
        {
            case Key.Space:
                TogglePlayback();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Home:
                StopPlayback();
                GetViewport().SetInputAsHandled();
                break;
            case Key.L:
                OpenSongDialog();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Delete:
                DeleteSelectedNote();
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private void BuildUi()
    {
        if (_editor.IsNull)
            return;

        EditorView view = _editor.GetComponent<EditorView>();
        Playfield playfield = _editor.GetComponent<Playfield>();
        LevelInfo level = _editor.GetComponent<LevelInfo>();
        EditorPlayhead playhead = _editor.GetComponent<EditorPlayhead>();

        float fieldWidth = playfield.Width * view.Scale;
        float panelX = view.ScreenOrigin.X + fieldWidth + 30;
        float panelY = view.ScreenOrigin.Y;
        float panelWidth = view.ScreenSize.X - panelX - 30;
        float x = panelX + 16;
        float width = panelWidth - 32;
        float y = panelY + 220;

        var load = AddButton("Load Song (L)", x, y, width, 34);
        load.Pressed += OpenSongDialog;

        float half = (width - 8) / 2;
        float third = (width - 16) / 3;
        var save = AddButton("Save", x, y + 42, third, 32);
        save.Pressed += SaveCurrent;
        var saveAs = AddButton("Save As", x + third + 8, y + 42, third, 32);
        saveAs.Pressed += OpenSaveAsDialog;
        var loadLevel = AddButton("Load", x + (third + 8) * 2, y + 42, third, 32);
        loadLevel.Pressed += OpenLoadDialog;

        _playButton = AddButton("Play", x, y + 82, half, 34);
        _playButton.Pressed += TogglePlayback;

        var stop = AddButton("Stop", x + half + 8, y + 82, half, 34);
        stop.Pressed += StopPlayback;

        AddLabel("Level name", x, y + 128, width);
        _nameEdit = AddLineEdit(level.Name, x, y + 148, width);
        _nameEdit.TextChanged += OnNameChanged;

        AddLabel("BPM / offset (ms)", x, y + 188, width);
        _bpmEdit = AddLineEdit(playhead.Bpm.ToString(), x, y + 208, half);
        _bpmEdit.TextChanged += OnBpmChanged;

        _offsetEdit = AddLineEdit(
            playhead.OffsetMs.ToString(),
            x + half + 8,
            y + 208,
            half);
        _offsetEdit.TextChanged += OnOffsetChanged;

        AddLabel("Snap", x, y + 248, width);
        _snapOption = new OptionButton
        {
            Position = new Vector2(x, y + 268),
            Size = new Vector2(width, 30),
        };
        _snapOption.AddItem("1/4 beat");
        _snapOption.AddItem("1/8 beat");
        _snapOption.AddItem("1/16 beat");
        _snapOption.Selected = playhead.SnapDivision switch
        {
            8 => 1,
            16 => 2,
            _ => 0,
        };
        _snapOption.ItemSelected += OnSnapSelected;
        _root.AddChild(_snapOption);

        AddLabel("Space play/pause    Home stop    Delete note", x, y + 310, width);
        AddLabel("Ctrl+Z undo    Ctrl+Y redo", x, y + 332, width);

        _statusLabel = new Label
        {
            Text = "",
            Position = new Vector2(x, y + 358),
            Size = new Vector2(width, 20),
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 14);
        _root.AddChild(_statusLabel);
    }

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
        if (_editor.IsNull)
            return;

        try
        {
            LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
            string path = LevelStore.DefaultPath(document.Name);
            LevelStore.Save(document, path);
            _statusLabel.Text = $"Saved {Path.GetFileName(path)}";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"Save failed: {exception.Message}";
        }
    }

    private void OpenSaveAsDialog()
    {
        if (_editor.IsNull)
            return;

        LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
        _saveAsDialog.CurrentDir = LevelStore.Directory;
        _saveAsDialog.CurrentFile =
            Path.GetFileName(LevelStore.DefaultPath(document.Name));
        _saveAsDialog.PopupCentered(new Vector2I(900, 600));
    }

    private void OnSaveAsSelected(string path)
    {
        if (_editor.IsNull)
            return;

        try
        {
            LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
            LevelStore.Save(document, path);
            _statusLabel.Text = $"Saved {Path.GetFileName(path)}";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"Save failed: {exception.Message}";
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
            LevelDocumentMapper.Apply(_entities, _editor, document);
            _history.Clear();

            _syncing = true;
            _nameEdit.Text = document.Name;
            _bpmEdit.Text = document.Bpm.ToString();
            _offsetEdit.Text = document.OffsetMs.ToString();
            _snapOption.Selected = document.SnapDivision switch
            {
                8 => 1,
                16 => 2,
                _ => 0,
            };
            _syncing = false;

            _statusLabel.Text = $"Loaded {Path.GetFileName(path)}";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"Load failed: {exception.Message}";
        }
    }

    private void OnSongSelected(string path)
    {
        if (_editor.IsNull)
            return;

        ref LevelInfo level = ref _editor.GetComponent<LevelInfo>();
        ref EditorPlayhead playhead = ref _editor.GetComponent<EditorPlayhead>();

        level.AudioPath = path;
        if (string.IsNullOrWhiteSpace(level.Name) || level.Name == "Untitled")
        {
            level.Name = Path.GetFileNameWithoutExtension(path);
            _nameEdit.Text = level.Name;
        }

        playhead.TimeMs = 0;
        playhead.DurationMs = 0;
        playhead.Playing = false;
        playhead.SeekRequestMs = -1;
    }

    private void TogglePlayback()
    {
        if (_editor.IsNull)
            return;

        ref EditorPlayhead playhead = ref _editor.GetComponent<EditorPlayhead>();

        if (!playhead.Playing &&
            playhead.DurationMs > 0 &&
            playhead.TimeMs >= playhead.DurationMs)
        {
            playhead.SeekRequestMs = 0;
        }

        playhead.Playing = !playhead.Playing;
    }

    private void StopPlayback()
    {
        if (_editor.IsNull)
            return;

        ref EditorPlayhead playhead = ref _editor.GetComponent<EditorPlayhead>();
        playhead.Playing = false;
        playhead.SeekRequestMs = 0;
    }

    private void OnNameChanged(string value)
    {
        if (_syncing || _editor.IsNull)
            return;

        ref LevelInfo level = ref _editor.GetComponent<LevelInfo>();
        level.Name = value;
    }

    private void OnBpmChanged(string value)
    {
        if (_syncing || _editor.IsNull || !int.TryParse(value, out int bpm))
            return;

        ref EditorPlayhead playhead = ref _editor.GetComponent<EditorPlayhead>();
        playhead.Bpm = Math.Clamp(bpm, 1, 1000);
    }

    private void OnOffsetChanged(string value)
    {
        if (_syncing || _editor.IsNull || !long.TryParse(value, out long offset))
            return;

        ref EditorPlayhead playhead = ref _editor.GetComponent<EditorPlayhead>();
        playhead.OffsetMs = Math.Clamp(offset, -600_000, 600_000);
    }

    private void OnSnapSelected(long index)
    {
        if (_syncing || _editor.IsNull)
            return;

        ref EditorPlayhead playhead = ref _editor.GetComponent<EditorPlayhead>();
        playhead.SnapDivision = index switch
        {
            1 => 8,
            2 => 16,
            _ => 4,
        };
    }

    private void DeleteSelectedNote()
    {
        if (_editor.IsNull)
            return;

        ref NoteSelection selection = ref _editor.GetComponent<NoteSelection>();
        if (selection.Note.IsNull)
            return;

        var before = _history.Capture();
        selection.Note.DeleteEntity();
        var after = _history.Capture();
        _history.Record(before, after);
        selection.Note = default;
    }

    private void Undo()
    {
        if (_editor.IsNull || !_history.Undo())
            return;

        ClearSelection();
        _statusLabel.Text = "Undo";
    }

    private void Redo()
    {
        if (_editor.IsNull || !_history.Redo())
            return;

        ClearSelection();
        _statusLabel.Text = "Redo";
    }

    private void ClearSelection()
    {
        ref NoteSelection selection = ref _editor.GetComponent<NoteSelection>();
        selection.Note = default;
    }

    private Button AddButton(string text, float x, float y, float width, float height)
    {
        var button = new Button
        {
            Text = text,
            Position = new Vector2(x, y),
            Size = new Vector2(width, height),
        };
        _root.AddChild(button);
        return button;
    }

    private void AddLabel(string text, float x, float y, float width)
    {
        var label = new Label
        {
            Text = text,
            Position = new Vector2(x, y),
            Size = new Vector2(width, 20),
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        _root.AddChild(label);
    }

    private LineEdit AddLineEdit(string text, float x, float y, float width)
    {
        var edit = new LineEdit
        {
            Text = text,
            Position = new Vector2(x, y),
            Size = new Vector2(width, 30),
        };
        _root.AddChild(edit);
        return edit;
    }

    private Entity FindEditor()
    {
        foreach (Entity entity in _entities.Query<EditorView>().ToEntityList())
            return entity;

        return default;
    }
}
