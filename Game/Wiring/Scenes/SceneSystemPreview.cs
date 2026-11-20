using System.Collections.Generic;
using System.Numerics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Logic.Editor;
using SceneState = Xanadu.Model.Ecs.Components.SceneState;

namespace Xanadu.Wiring.Scenes;

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    private LevelEntry? _previewEntry;
    private long _previewStartMs;
    private long _previewReturnTimeMs;
    private EditorReturnState? _previewReturn;
    private List<Vector2>? _previewTrace;

    /// <summary>True while the play scene was launched from the editor.</summary>
    public bool IsEditorPreview => _previewEntry is not null;

    /// <summary>
    /// Launches the real play scene for the editor's in-memory level, from the
    /// song's start, or from <paramref name="playheadMs"/> when <paramref name="fromPlayhead"/>.
    /// </summary>
    public bool StartEditorPreview(LevelEntry entry, long playheadMs, bool fromPlayhead = false)
    {
        if (_stateEntity.IsNull || IsTransitioning())
            return false;

        _previewReturn = EditorReturnState.Capture(_entities);
        _previewEntry = entry;
        _previewStartMs = fromPlayhead ? playheadMs : 0;
        _previewReturnTimeMs = playheadMs;
        QueuePreviewPlay();
        return true;
    }

    /// <summary>Where the current preview runs from, if one is running.</summary>
    public long? PreviewStartMs => _previewEntry is not null ? _previewStartMs : null;

    private void QueuePreviewPlay()
    {
        _pendingPlayEntry = _previewEntry;
        _pendingStartMs = PreviewStartMs;
        _pendingResults = null;
        _restartRequested = true;
        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        state.Pending = SceneKind.Play;
    }

    private void ReturnToEditor(ref SceneState state)
    {
        _pendingEditorEntry = _previewEntry;
        state.Pending = SceneKind.Editor;
    }

    private void ClearPreviewIfLeaving(SceneKind next)
    {
        if (next == SceneKind.Editor && IsEditorPreview)
            _previewTrace = TakeTrace();
        if (next == SceneKind.Play || next == SceneKind.Editor)
            return;

        _previewReturn = null;
        _previewEntry = null;
        _previewTrace = null;
    }

    /// <summary>The preview body's trace, before its scene goes (one play entity at most).</summary>
    private List<Vector2>? TakeTrace()
    {
        foreach (Entity entity in _entities.Query<BodyTrace>().Entities)
        {
            if (entity.TryGetComponent(out PlayfieldMode mode) && mode.Kind == PlayfieldKind.Play)
                return entity.GetComponent<BodyTrace>().Points;
        }

        return _previewTrace;
    }

    private void RestorePreviewPlayhead(SceneKind next)
    {
        if (next != SceneKind.Editor || _previewEntry is null)
            return;

        // One editor entity.
        foreach (Entity entity in _entities.Query<Playhead, PlayfieldMode>().ToEntityList())
        {
            if (entity.GetComponent<PlayfieldMode>().Kind != PlayfieldKind.Editor)
                continue;

            ref Playhead playhead = ref entity.GetComponent<Playhead>();
            playhead.TimeMs = _previewReturnTimeMs;
            playhead.SeekRequestMs = _previewReturnTimeMs;
            _previewReturn?.Restore(_entities, entity);
            if (_previewTrace is not null)
                entity.AddComponent(new BodyTrace { Points = _previewTrace });
        }

        _previewReturn = null;
        _previewTrace = null;
        _previewEntry = null;
    }
}
