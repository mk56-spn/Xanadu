using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Editor.Commands;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Input;

namespace Xanadu.Editor;

/// <summary>
/// What the ECS editor asks of its host: saving, previews, shortcuts and the
/// shader import picker. Every dialog is an ECS one (<see cref="EditorDialogs"/>);
/// text fields are typed into by the host's text input. Engine-free: the host
/// calls <see cref="Tick"/> once a frame after the systems update.
/// </summary>
public sealed partial class EditorOverlay
{
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;
    private readonly IBlockingPointerInputSource? _pointer;
    private readonly Action? _onBack;
    private string _saveProblem = "";
    private readonly Action? _onForward;
    private readonly Action<LevelDocument, string, long>? _onPreview;
    private readonly Action<LevelDocument, string, long>? _onPreviewHere;

    private Entity _editor;

    private bool _closed;
    private readonly bool _openSongDialog;

    public EditorOverlay(
        EntityStore entities,
        EditorCommandBus commands,
        IBlockingPointerInputSource? pointer,
        Action? onBack = null,
        Action? onForward = null,
        Action<LevelDocument, string, long>? onPreview = null,
        bool openSongDialog = true,
        Action<LevelDocument, string, long>? onPreviewHere = null)
    {
        _entities = entities;
        _commands = commands;
        _pointer = pointer;
        _onBack = onBack;
        _onForward = onForward;
        _onPreview = onPreview;
        _onPreviewHere = onPreviewHere;
        _openSongDialog = openSongDialog;

        _editor = FindEditor();
        if (!_editor.IsNull)
        {
            _editor.AddComponent(new EditorShortcutHost(this));
            _editor.AddComponent(new Xanadu.Systems.Timeline.TimelineHost(
                new EditorTimelineHost(_editor, _commands, SetStatus)));
        }
    }

    /// <summary>Detaches the overlay before the ECS editor scene is destroyed.</summary>
    public void Close()
    {
        if (_closed)
            return;

        _closed = true;
        _editor = default;
        _pointer?.SetBlocked(false);
    }
}
