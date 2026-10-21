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

    private Control _root = null!;
    private FileDialog _songDialog = null!;
    private LineEdit _nameEdit = null!;
    private LineEdit _bpmEdit = null!;
    private LineEdit _offsetEdit = null!;
    private Button _playButton = null!;
    private Entity _editor;

    private bool _syncing;

    public EditorOverlay(EntityStore entities)
    {
        _entities = entities;
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
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

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
        _playButton = AddButton("Play", x, y + 42, half, 34);
        _playButton.Pressed += TogglePlayback;

        var stop = AddButton("Stop", x + half + 8, y + 42, half, 34);
        stop.Pressed += StopPlayback;

        AddLabel("Level name", x, y + 88, width);
        _nameEdit = AddLineEdit(level.Name, x, y + 108, width);
        _nameEdit.TextChanged += OnNameChanged;

        AddLabel("BPM / offset (ms)", x, y + 148, width);
        _bpmEdit = AddLineEdit(playhead.Bpm.ToString(), x, y + 168, half);
        _bpmEdit.TextChanged += OnBpmChanged;

        _offsetEdit = AddLineEdit(
            playhead.OffsetMs.ToString(),
            x + half + 8,
            y + 168,
            half);
        _offsetEdit.TextChanged += OnOffsetChanged;

        AddLabel("Space play/pause    Home stop", x, y + 214, width);
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
