using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Levels;
using Writer.Editor.Commands;
using Writer.Systems.Editor;
using Writer.Systems.Input;

namespace Writer.Editor;

/// <summary>Native editor header, transport, timeline footer, and level metadata controls.</summary>
public partial class EditorOverlay : CanvasLayer
{
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;
    private readonly IBlockingPointerInputSource? _pointer;
    private readonly Action? _onBack;
    private readonly Action? _onForward;
    private readonly TapTempo _tapTempo = new();

    private Control _root = null!;
    private Control _header = null!;
    private Control _footer = null!;
    private Control _metadataCatcher = null!;
    private Panel _metadataPanel = null!;
    private FileDialog _songDialog = null!;
    private FileDialog _saveAsDialog = null!;
    private FileDialog _loadDialog = null!;
    private LineEdit _nameEdit = null!;
    private LineEdit _artistEdit = null!;
    private LineEdit _difficultyEdit = null!;
    private LineEdit _bpmEdit = null!;
    private LineEdit _offsetEdit = null!;
    private Label _levelLabel = null!;
    private Label _songLabel = null!;
    private Label _timeLabel = null!;
    private Label _notesLabel = null!;
    private Label _previewLabel = null!;
    private Label _statusLabel = null!;
    private Entity _editor;

    private bool _syncing;
    private bool _previewActive;
    private bool _confirmationActive;
    private bool _suppressPointerUntilRelease;
    private bool _closed;
    private readonly bool _openSongDialog;

    public EditorOverlay(
        EntityStore entities,
        EditorCommandBus commands,
        IBlockingPointerInputSource? pointer,
        Action? onBack = null,
        Action? onForward = null,
        bool openSongDialog = true)
    {
        _entities = entities;
        _commands = commands;
        _pointer = pointer;
        _onBack = onBack;
        _onForward = onForward;
        _openSongDialog = openSongDialog;
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

        if (_openSongDialog &&
            _editor.HasComponent<LevelInfo>() &&
            string.IsNullOrWhiteSpace(_editor.GetComponent<LevelInfo>().AudioPath))
        {
            CallDeferred(nameof(OpenSongDialog));
        }
    }

    /// <summary>Detaches the overlay before the ECS editor scene is destroyed.</summary>
    public void Close()
    {
        if (_closed)
            return;

        _closed = true;
        _editor = default;
        QueueFree();
    }

    private void BuildUi()
    {
        if (_editor.IsNull)
            return;

        PlayfieldView view = _editor.GetComponent<PlayfieldView>();
        LevelInfo level = _editor.GetComponent<LevelInfo>();
        Playhead playhead = _editor.GetComponent<Playhead>();

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

}
