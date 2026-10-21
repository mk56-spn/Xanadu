using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Editor.Commands;

namespace Writer.Editor;

/// <summary>Native controls for song selection and editor metadata.</summary>
public partial class EditorOverlay : CanvasLayer
{
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;
    private readonly TapTempo _tapTempo = new();

    private Control _root = null!;
    private FileDialog _songDialog = null!;
    private FileDialog _saveAsDialog = null!;
    private FileDialog _loadDialog = null!;
    private LineEdit _nameEdit = null!;
    private LineEdit _artistEdit = null!;
    private LineEdit _difficultyEdit = null!;
    private LineEdit _bpmEdit = null!;
    private LineEdit _offsetEdit = null!;
    private OptionButton _snapOption = null!;
    private Button _playButton = null!;
    private Button _previewButton = null!;
    private Button _metronomeButton = null!;
    private Label _previewLabel = null!;
    private Label _statusLabel = null!;
    private Entity _editor;

    private bool _syncing;
    private bool _previewActive;

    public EditorOverlay(EntityStore entities, EditorCommandBus commands)
    {
        _entities = entities;
        _commands = commands;
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

        if (_editor.HasComponent<EditorMetronome>())
        {
            _metronomeButton.Text = _editor.GetComponent<EditorMetronome>().Enabled
                ? "Click On"
                : "Click Off";
        }

        bool previewActive = _editor.HasComponent<EditorPreview>() &&
                             _editor.GetComponent<EditorPreview>().Active;

        if (previewActive != _previewActive)
        {
            _previewActive = previewActive;
            _previewLabel.Visible = previewActive;
            SetEditControlsVisible(!previewActive);
        }

        if (_previewLabel.Visible)
        {
            _previewLabel.Text =
                $"PREVIEW - {level.Name}   " +
                $"{playhead.TimeMs / 1000f:0.00}s / {playhead.DurationMs / 1000f:0.00}s   Esc to return";
        }

        if (!_nameEdit.HasFocus() && _nameEdit.Text != level.Name)
        {
            _syncing = true;
            _nameEdit.Text = level.Name;
            _syncing = false;
        }

        if (!_artistEdit.HasFocus() && _artistEdit.Text != level.Artist)
        {
            _syncing = true;
            _artistEdit.Text = level.Artist;
            _syncing = false;
        }

        if (!_difficultyEdit.HasFocus() && _difficultyEdit.Text != level.Difficulty)
        {
            _syncing = true;
            _difficultyEdit.Text = level.Difficulty;
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

        if (key.Keycode == Key.Escape && IsPreviewing())
        {
            EndPreview();
            GetViewport().SetInputAsHandled();
            return;
        }

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
            case Key.T:
                TapTempo();
                GetViewport().SetInputAsHandled();
                break;
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
        float y = panelY + 200;

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

        float controlQuarter = (width - 24) / 4;
        _playButton = AddButton("Play", x, y + 82, controlQuarter, 34);
        _playButton.Pressed += TogglePlayback;

        var stop = AddButton("Stop", x + controlQuarter + 8, y + 82, controlQuarter, 34);
        stop.Pressed += StopPlayback;

        _previewButton = AddButton(
            "Preview",
            x + (controlQuarter + 8) * 2,
            y + 82,
            controlQuarter,
            34);
        _previewButton.Pressed += TogglePreview;

        _metronomeButton = AddButton(
            "Click Off",
            x + (controlQuarter + 8) * 3,
            y + 82,
            controlQuarter,
            34);
        _metronomeButton.Pressed += ToggleMetronome;

        _previewLabel = new Label
        {
            Text = "PREVIEW   Esc to return",
            Position = new Vector2(40, 8),
            Size = new Vector2(700, 30),
            Visible = false,
        };
        _previewLabel.AddThemeFontSizeOverride("font_size", 22);
        _root.AddChild(_previewLabel);

        AddLabel("Artist", x, y + 116, half);
        _artistEdit = AddLineEdit(level.Artist, x, y + 134, half);
        _artistEdit.TextChanged += OnArtistChanged;

        AddLabel("Difficulty", x + half + 8, y + 116, half);
        _difficultyEdit = AddLineEdit(level.Difficulty, x + half + 8, y + 134, half);
        _difficultyEdit.TextChanged += OnDifficultyChanged;

        AddLabel("Level name", x, y + 170, width);
        _nameEdit = AddLineEdit(level.Name, x, y + 188, width);
        _nameEdit.TextChanged += OnNameChanged;

        AddLabel("BPM / offset (ms)", x, y + 224, width);
        _bpmEdit = AddLineEdit(playhead.Bpm.ToString(), x, y + 242, half);
        _bpmEdit.TextChanged += OnBpmChanged;

        _offsetEdit = AddLineEdit(
            playhead.OffsetMs.ToString(),
            x + half + 8,
            y + 242,
            half);
        _offsetEdit.TextChanged += OnOffsetChanged;

        AddLabel("Snap", x, y + 278, width);
        _snapOption = new OptionButton
        {
            Position = new Vector2(x, y + 296),
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

        AddLabel(
            "Space play   Home stop   Delete note   Ctrl+Z/Y undo   T tap tempo",
            x,
            y + 336,
            width);

        _statusLabel = new Label
        {
            Text = "",
            Position = new Vector2(40, 4),
            Size = new Vector2(view.ScreenSize.X - 80, 20),
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
        if (_editor.IsNull || !ValidateCurrent())
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
        if (_editor.IsNull || !ValidateCurrent())
            return;

        LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
        _saveAsDialog.CurrentDir = LevelStore.Directory;
        _saveAsDialog.CurrentFile =
            Path.GetFileName(LevelStore.DefaultPath(document.Name));
        _saveAsDialog.PopupCentered(new Vector2I(900, 600));
    }

    private void OnSaveAsSelected(string path)
    {
        if (_editor.IsNull || !ValidateCurrent())
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
            _commands.Enqueue(EditorCommand.ApplyLevel(document));

            _syncing = true;
            _nameEdit.Text = document.Name;
            _artistEdit.Text = document.Artist;
            _difficultyEdit.Text = document.Difficulty;
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

        _commands.Enqueue(EditorCommand.LoadSong(path));
    }

    private void ToggleMetronome()
    {
        if (_editor.IsNull || !_editor.HasComponent<EditorMetronome>())
            return;

        EditorMetronome metronome = _editor.GetComponent<EditorMetronome>();
        _commands.Enqueue(EditorCommand.SetMetronome(!metronome.Enabled));
    }

    private void TapTempo()
    {
        if (_editor.IsNull)
            return;

        int? bpm = _tapTempo.Tap((long)Time.GetTicksMsec());
        if (bpm is not int value)
        {
            _statusLabel.Text = "Tap again for BPM";
            return;
        }

        _commands.Enqueue(EditorCommand.SetBpm(value));
        _bpmEdit.Text = value.ToString();
        _statusLabel.Text = $"Tap BPM {value}";
    }

    private void TogglePreview()
    {
        if (_editor.IsNull)
            return;

        if (IsPreviewing())
        {
            EndPreview();
            return;
        }

        if (!ValidateCurrent())
            return;

        _commands.Enqueue(EditorCommand.BeginPreview());
    }

    private void EndPreview()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.EndPreview());
    }

    private bool IsPreviewing() =>
        !_editor.IsNull &&
        _editor.HasComponent<EditorPreview>() &&
        _editor.GetComponent<EditorPreview>().Active;

    private void SetEditControlsVisible(bool visible)
    {
        foreach (Node child in _root.GetChildren())
        {
            if (child is Control control && control != _previewLabel)
                control.Visible = visible;
        }
    }

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

    private void TogglePlayback()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.TogglePlayback());
    }

    private void StopPlayback()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.StopPlayback());
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

    private void OnSnapSelected(long index)
    {
        if (_syncing || _editor.IsNull)
            return;

        int snap = index switch
        {
            1 => 8,
            2 => 16,
            _ => 4,
        };
        _commands.Enqueue(EditorCommand.SetSnap(snap));
    }

    private void DeleteSelectedNote()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.DeleteSelection());
    }

    private void Undo()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.Undo());
        _statusLabel.Text = "Undo";
    }

    private void Redo()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.Redo());
        _statusLabel.Text = "Redo";
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
