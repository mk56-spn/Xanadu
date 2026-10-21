using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Editor.Commands;
using Writer.Systems.Editor;
using Writer.Systems.Input;

namespace Writer.Editor;

/// <summary>Native editor header, transport, timeline footer, and level metadata controls.</summary>
public partial class EditorOverlay : CanvasLayer
{
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;
    private readonly GodotPointerInputSource? _pointer;
    private readonly TapTempo _tapTempo = new();

    private Control _root = null!;
    private Control _header = null!;
    private Control _footer = null!;
    private HBoxContainer _transport = null!;
    private Control _metadataCatcher = null!;
    private Panel _metadataPanel = null!;
    private Button _metadataButton = null!;
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
    private Label _levelLabel = null!;
    private Label _songLabel = null!;
    private Label _timeLabel = null!;
    private Label _notesLabel = null!;
    private Label _previewLabel = null!;
    private Label _statusLabel = null!;
    private Entity _editor;

    private bool _syncing;
    private bool _previewActive;
    private bool _suppressPointerUntilRelease;

    public EditorOverlay(
        EntityStore entities,
        EditorCommandBus commands,
        GodotPointerInputSource? pointer)
    {
        _entities = entities;
        _commands = commands;
        _pointer = pointer;
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

        if (_suppressPointerUntilRelease &&
            !Godot.Input.IsMouseButtonPressed(MouseButton.Left))
        {
            _suppressPointerUntilRelease = false;
        }

        _pointer?.SetBlocked(
            AnyDialogVisible() ||
            _metadataPanel.Visible ||
            _suppressPointerUntilRelease);

        ref EditorPlayhead playhead = ref _editor.GetComponent<EditorPlayhead>();
        ref LevelInfo level = ref _editor.GetComponent<LevelInfo>();

        string levelName = string.IsNullOrWhiteSpace(level.Name)
            ? "Untitled"
            : level.Name;
        if (_levelLabel.Text != levelName)
            _levelLabel.Text = levelName;

        string song = string.IsNullOrEmpty(level.AudioPath)
            ? "No song loaded"
            : Path.GetFileName(level.AudioPath);
        string songText = $"Song: {song}";
        if (_songLabel.Text != songText)
            _songLabel.Text = songText;

        string timeText =
            $"{FormatMs(playhead.TimeMs)} / {FormatMs(playhead.DurationMs)}";
        if (_timeLabel.Text != timeText)
            _timeLabel.Text = timeText;

        _notesLabel.Text = $"{_entities.Query<Note>().Count} notes";

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

        if (previewActive &&
            !playhead.Playing &&
            playhead.DurationMs > 0 &&
            playhead.TimeMs >= playhead.DurationMs)
        {
            EndPreview();
            _statusLabel.Text = "Preview ended";
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

        if (AnyDialogVisible())
            return;

        if (key.Keycode == Key.Escape && _metadataPanel.Visible)
        {
            HideMetadata();
            GetViewport().SetInputAsHandled();
            return;
        }

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
        LevelInfo level = _editor.GetComponent<LevelInfo>();
        EditorPlayhead playhead = _editor.GetComponent<EditorPlayhead>();

        _metadataCatcher = new Control
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _metadataCatcher.GuiInput += OnMetadataCatcherInput;
        _root.AddChild(_metadataCatcher);
        _metadataCatcher.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        BuildHeader(view, level);
        BuildMetadataDropdown(view, level, playhead);
        BuildFooter(view);
        BuildTransport(view, playhead);

        _previewLabel = new Label
        {
            Text = "PREVIEW   Esc to return",
            Position = new Vector2(40, 8),
            Size = new Vector2(view.ScreenSize.X - 80, 30),
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _previewLabel.AddThemeFontSizeOverride("font_size", 22);
        _root.AddChild(_previewLabel);
    }

    private void BuildHeader(EditorView view, LevelInfo level)
    {
        _header = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = Vector2.Zero,
            Size = new Vector2(view.ScreenSize.X, EditorLayout.HeaderHeight),
        };
        _root.AddChild(_header);

        float infoWidth = Mathf.Max(160, view.ScreenSize.X - 420);

        _levelLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(level.Name) ? "Untitled" : level.Name,
            Position = new Vector2(24, 4),
            Size = new Vector2(infoWidth, 28),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _levelLabel.AddThemeFontSizeOverride("font_size", 22);
        _levelLabel.AddThemeColorOverride("font_color", new Color("#facc15"));
        _header.AddChild(_levelLabel);

        _songLabel = new Label
        {
            Text = "Song: No song loaded",
            Position = new Vector2(24, 32),
            Size = new Vector2(infoWidth, 18),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _songLabel.AddThemeFontSizeOverride("font_size", 14);
        _songLabel.AddThemeColorOverride("font_color", new Color("#cbd5e1"));
        _header.AddChild(_songLabel);

        _statusLabel = new Label
        {
            Text = "Space play   Home stop   Delete note   Ctrl+Z/Y undo   T tap tempo",
            Position = new Vector2(24, 50),
            Size = new Vector2(infoWidth, 18),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 12);
        _statusLabel.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        _header.AddChild(_statusLabel);

        float right = view.ScreenSize.X - 24;
        float y = (EditorLayout.HeaderHeight - 38) / 2;
        const float levelWidth = 104;
        const float saveWidth = 88;
        const float songWidth = 128;

        _metadataButton = AddHeaderButton(
            "Level ▾",
            right - levelWidth,
            y,
            levelWidth);
        _metadataButton.Pressed += ToggleMetadata;

        var save = AddHeaderButton(
            "Save",
            right - levelWidth - 8 - saveWidth,
            y,
            saveWidth);
        save.Pressed += SaveCurrent;

        var loadSong = AddHeaderButton(
            "Load Song (L)",
            right - levelWidth - 8 - saveWidth - 8 - songWidth,
            y,
            songWidth);
        loadSong.Pressed += OpenSongDialog;
    }

    private void BuildMetadataDropdown(
        EditorView view,
        LevelInfo level,
        EditorPlayhead playhead)
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

        var fileRow = new HBoxContainer();
        fileRow.AddThemeConstantOverride("separation", 8);
        box.AddChild(fileRow);

        var saveAs = AddFillButton("Save As");
        saveAs.Pressed += OpenSaveAsDialog;
        fileRow.AddChild(saveAs);

        var loadLevel = AddFillButton("Load Level");
        loadLevel.Pressed += OpenLoadDialog;
        fileRow.AddChild(loadLevel);
    }

    private void BuildFooter(EditorView view)
    {
        Rect2 footer = EditorLayout.FooterRect(view);
        _footer = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = footer.Position,
            Size = footer.Size,
        };
        _root.AddChild(_footer);

        _timeLabel = new Label
        {
            Text = "00:00.000 / 00:00.000",
            Position = new Vector2(24, 4),
            Size = new Vector2(300, 16),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _timeLabel.AddThemeFontSizeOverride("font_size", 14);
        _timeLabel.AddThemeColorOverride("font_color", new Color("#cbd5e1"));
        _footer.AddChild(_timeLabel);

        _notesLabel = new Label
        {
            Text = "0 notes",
            Position = new Vector2(view.ScreenSize.X - 24 - 240, 4),
            Size = new Vector2(240, 16),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _notesLabel.AddThemeFontSizeOverride("font_size", 13);
        _notesLabel.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        _footer.AddChild(_notesLabel);
    }

    private void BuildTransport(EditorView view, EditorPlayhead playhead)
    {
        Rect2 transport = EditorLayout.TransportRect(view);
        _transport = new HBoxContainer
        {
            Position = transport.Position,
            Size = transport.Size,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _transport.AddThemeConstantOverride("separation", 8);
        _root.AddChild(_transport);

        _playButton = AddTransportButton("Play");
        _playButton.Pressed += TogglePlayback;

        var stop = AddTransportButton("Stop");
        stop.Pressed += StopPlayback;

        _previewButton = AddTransportButton("Preview");
        _previewButton.Pressed += TogglePreview;

        _metronomeButton = AddTransportButton("Click Off");
        _metronomeButton.Pressed += ToggleMetronome;

        _transport.AddChild(new Control
        {
            CustomMinimumSize = new Vector2(12, 0),
        });

        var snapLabel = new Label
        {
            Text = "Snap",
            VerticalAlignment = VerticalAlignment.Center,
        };
        snapLabel.AddThemeFontSizeOverride("font_size", 14);
        _transport.AddChild(snapLabel);

        _snapOption = new OptionButton
        {
            CustomMinimumSize = new Vector2(132, 36),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
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
        _transport.AddChild(_snapOption);
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
            string path = CurrentLevelPath();
            if (string.IsNullOrEmpty(path))
                path = LevelStore.DefaultPath(document.Name);

            LevelStore.Save(document, path);
            _commands.Enqueue(EditorCommand.SetLevelFile(path));
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
            _commands.Enqueue(EditorCommand.ApplyLevel(document, path));

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

    private bool AnyDialogVisible() =>
        _songDialog.Visible ||
        _saveAsDialog.Visible ||
        _loadDialog.Visible;

    private void SetEditControlsVisible(bool visible)
    {
        _header.Visible = visible;
        _footer.Visible = visible;
        _transport.Visible = visible;

        if (!visible)
            HideMetadata();
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
        _metadataButton.Text = "Level ▴";
    }

    private void HideMetadata()
    {
        if (GetViewport().GuiGetFocusOwner() is LineEdit)
            GetViewport().GuiReleaseFocus();

        _metadataPanel.Visible = false;
        _metadataCatcher.Visible = false;
        _metadataButton.Text = "Level ▾";
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

    private Button AddHeaderButton(string text, float x, float y, float width)
    {
        var button = new Button
        {
            Text = text,
            Position = new Vector2(x, y),
            Size = new Vector2(width, 38),
            CustomMinimumSize = new Vector2(width, 38),
        };
        _header.AddChild(button);
        return button;
    }

    private Button AddTransportButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(108, 38),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        _transport.AddChild(button);
        return button;
    }

    private static Button AddFillButton(string text) =>
        new()
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 32),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

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
        foreach (Entity entity in _entities.Query<EditorView>().ToEntityList())
            return entity;

        return default;
    }
}
