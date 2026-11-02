using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Levels;
using SceneState = Xanadu.Ecs.SceneState;

namespace Xanadu.Systems.Scenes;

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    private LevelEntry? _previewEntry;
    private List<StartPoint> _previewStarts = new();
    private int _previewIndex;
    private long _previewReturnTimeMs;
    private PlayerPath? _previewPath;
    private EditorReturnState? _previewReturn;

    /// <summary>True while the play scene was launched from the editor.</summary>
    public bool IsEditorPreview => _previewEntry is not null;

    /// <summary>Number of start points the editor preview can cycle through.</summary>
    public int PreviewStartCount => _previewStarts.Count;

    /// <summary>
    /// Launches the real play scene for the editor's in-memory level, beginning
    /// at the start point at or before <paramref name="playheadMs"/>.
    /// </summary>
    /// <param name="fromPlayhead">Begins exactly at the playhead rather than at a start point.</param>
    public bool StartEditorPreview(LevelEntry entry, long playheadMs, bool fromPlayhead = false)
    {
        if (_stateEntity.IsNull || IsTransitioning())
            return false;

        _previewReturn = EditorReturnState.Capture(_entities);

        List<StartPoint> starts = (entry.Document.StartPoints ?? new())
            .Select(point => new StartPoint(
                point.Name,
                point.TimeMs,
                new Godot.Vector2(point.X, point.Y),
                new System.Numerics.Vector2(point.MomentumX, point.MomentumY),
                new Godot.Vector2(point.DirectionX, point.DirectionY),
                ItemGroups.FromList(point.Groups)))
            .OrderBy(point => point.TimeMs)
            .ToList();

        if (fromPlayhead)
        {
            StartPoint here = EditorReturnState.StartAtPlayhead(_entities, entry.Document, starts, playheadMs);
            starts.Add(here);
            starts = starts.OrderBy(point => point.TimeMs).ToList();
        }

        _previewEntry = entry;
        _previewStarts = starts;
        _previewReturnTimeMs = playheadMs;
        _previewIndex = 0;
        for (int i = 0; i < starts.Count; i++)
        {
            if (starts[i].TimeMs <= playheadMs)
                _previewIndex = i;
        }
        // The stable sort keeps the playhead's own start after any at the same time,
        // so the search above lands on it.

        QueuePreviewPlay();
        return true;
    }

    /// <summary>Restarts the preview, with its lead-in, at the next or previous start point.</summary>
    public bool CyclePreviewStart(int direction)
    {
        if (_stateEntity.IsNull ||
            _previewEntry is null ||
            _previewStarts.Count == 0 ||
            IsTransitioning() ||
            Current != SceneKind.Play)
        {
            return false;
        }

        int count = _previewStarts.Count;
        _previewIndex = ((_previewIndex + direction) % count + count) % count;
        HidePauseVisual();
        SetPlayback(false);
        QueuePreviewPlay();
        return true;
    }

    /// <summary>The start point the current preview runs from, if any.</summary>
    public StartPoint? CurrentPreviewStart =>
        _previewEntry is not null && _previewStarts.Count > 0
            ? _previewStarts[_previewIndex]
            : null;

    private void QueuePreviewPlay()
    {
        _pendingPlayEntry = _previewEntry;
        _pendingStart = CurrentPreviewStart;
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
        if (next == SceneKind.Play || next == SceneKind.Editor)
            return;

        _previewPath = null;
        _previewReturn = null;
        _previewEntry = null;
        _previewStarts = new();
    }

    /// <summary>Keeps the path of the preview that is ending, before its entities go.</summary>
    private void CapturePreviewPath()
    {
        if (_previewEntry is null)
            return;

        foreach (Entity entity in _entities.Query<PlayerPathRecorder>().ToEntityList())
        {
            List<PlayerPathSample> samples = entity.GetComponent<PlayerPathRecorder>().Samples;
            if (samples.Count == 0 ||
                !entity.TryGetComponent(out Player player) ||
                !entity.TryGetComponent(out PlayfieldView view))
            {
                continue;
            }

            _previewPath = new PlayerPath(samples, player.Size / view.Scale);
        }
    }

    private void StartPreviewPathRecording(SceneKind next)
    {
        if (next != SceneKind.Play || _previewEntry is null)
            return;

        foreach (Entity entity in _entities.Query<PlaySession>().ToEntityList())
            entity.AddComponent(new PlayerPathRecorder());
    }

    private void RestorePreviewPlayhead(SceneKind next)
    {
        if (next != SceneKind.Editor || _previewEntry is null)
            return;

        foreach (Entity entity in _entities
                     .Query<Playhead, PlayfieldMode>()
                     .ToEntityList())
        {
            if (entity.GetComponent<PlayfieldMode>().Kind != PlayfieldKind.Editor)
                continue;

            ref Playhead playhead = ref entity.GetComponent<Playhead>();
            playhead.TimeMs = _previewReturnTimeMs;
            playhead.SeekRequestMs = _previewReturnTimeMs;
            if (_previewPath is PlayerPath path)
                entity.AddComponent(path);
            _previewReturn?.Restore(_entities, entity);
        }

        _previewReturn = null;

        _previewPath = null;
        _previewEntry = null;
        _previewStarts = new();
    }
}
