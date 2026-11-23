using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Screens.Editor;

/// <summary>
///     What the ECS editor asks of its host: saving, previews, shortcuts and the
///     shader import picker. Every dialog is an ECS one (<see cref="EditorDialogs" />);
///     text fields are typed into by the host's text input. Engine-free: the host
///     calls <see cref="Tick" /> once a frame after the systems update.
/// </summary>
public sealed partial class EditorOverlay
{
    private readonly EntityStore _entities;
    private readonly Action? _onBack;
    private readonly Action? _onForward;
    private readonly Action<LevelDocument, string, long>? _onPreview;
    private readonly Action<LevelDocument, string, long>? _onPreviewHere;
    private readonly bool _openSongDialog;
    private readonly IBlockingPointerInputSource? _pointer;

    private bool _closed;

    private Entity _editor;
    private string _saveProblem = "";

    public EditorOverlay(
        EntityStore entities,
        IBlockingPointerInputSource? pointer,
        Action? onBack = null,
        Action? onForward = null,
        Action<LevelDocument, string, long>? onPreview = null,
        bool openSongDialog = true,
        Action<LevelDocument, string, long>? onPreviewHere = null)
    {
        _entities = entities;
        _pointer = pointer;
        _onBack = onBack;
        _onForward = onForward;
        _onPreview = onPreview;
        _onPreviewHere = onPreviewHere;
        _openSongDialog = openSongDialog;

        _editor = FindEditor();
    }

    /// <summary>Detaches the overlay before the ECS editor scene is destroyed.</summary>
    public void Close()
    {
        if (_closed)
            return;

        _closed = true;
        if (!_editor.IsNull && _editor.Tags.Has<KeyTarget>())
            _editor.RemoveTag<KeyTarget>();
        _editor = default;
        _pointer?.SetBlocked(false);
    }
}
