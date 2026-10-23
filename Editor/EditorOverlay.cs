using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Levels;
using Writer.Levels;
using Writer.Editor.Commands;
using Writer.Systems.Editor;
using Writer.Systems.Input;

namespace Writer.Editor;

/// <summary>Native file dialogs and text-input bridge for the ECS editor UI.</summary>
public partial class EditorOverlay
{
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;
    private readonly IBlockingPointerInputSource? _pointer;
    private readonly Action? _onBack;
    private readonly Action? _onForward;
    private readonly Action? _onPlayFromStart;
    private readonly TapTempo _tapTempo = new();

    private ConfirmationDialog _songPicker = null!;
    private ItemList _songList = null!;
    private Label _songInfo = null!;
    private Label _songEmpty = null!;
    private readonly List<SongDocument> _songs = new();
    private FileDialog _saveAsDialog = null!;
    private FileDialog _loadDialog = null!;
    private LineEdit _textInput = null!;
    private Entity _textFieldEntity;
    private Entity _editor;

    private bool _syncingText;
    private bool _closed;
    private readonly bool _openSongDialog;

    public EditorOverlay(
        EntityStore entities,
        EditorCommandBus commands,
        IBlockingPointerInputSource? pointer,
        Action? onBack = null,
        Action? onForward = null,
        Action? onPlayFromStart = null,
        bool openSongDialog = true)
    {
        _entities = entities;
        _commands = commands;
        _pointer = pointer;
        _onBack = onBack;
        _onForward = onForward;
        _onPlayFromStart = onPlayFromStart;
        _openSongDialog = openSongDialog;
        Layer = 100;
    }

    public override void _Ready()
    {
        _editor = FindEditor();

        CreateTextFieldBridge();
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


}
