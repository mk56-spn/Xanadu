using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Ui;
using Xanadu.Timeline;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The editor timeline's pointer work: dragging selected items to new times and
/// scrubbing the playhead, with the pan window that zooms a long song in.
/// </summary>
public sealed class EditorTimelineTool(EditorSceneQuery scene, EditorHistory history)
{
    private readonly UiDragController _drag = new();
    private readonly Dictionary<int, long> _startTimes = new();
    private Entity _primary;
    private bool _movedDrag;
    private EditorSnapshot _before;
    private long _windowStart;
    private long _windowEnd;

    public bool Dragging { get; private set; }

    public bool Scrubbing { get; private set; }

    public void BeginDrag(
        Vector2 screenPosition,
        Rect2 bar,
        ref Playhead playhead,
        Entity editor,
        Entity primary)
    {
        (_windowStart, _windowEnd) = EditorLayout.TimelineWindow(playhead, TimelineOf(editor));
        Dragging = true;
        _movedDrag = false;
        _drag.Begin(primary, screenPosition);
        _primary = primary;
        _before = history.Capture();
        _startTimes.Clear();

        if (editor.HasComponent<NoteSelection>())
        {
            foreach (Entity selected in editor.GetComponent<NoteSelection>().AllNotes)
            {
                if (EditorItems.IsItem(selected) &&
                    EditorItemTiming.TryGet(selected, out long timeMs))
                {
                    _startTimes[selected.Id] = timeMs;
                }
            }
        }

        SetWindow(editor, true);
    }

    public void Drag(Vector2 screenPosition, Rect2 bar, ref Playhead playhead, Entity editor)
    {
        if (!_drag.Update(screenPosition))
            return;

        long span = Math.Max(1, _windowEnd - _windowStart);
        float delta = _drag.Delta.X / Math.Max(1f, bar.Size.X) * span;
        long maxTime = playhead.DurationMs > 0 ? playhead.DurationMs : long.MaxValue;
        bool moved = false;

        foreach ((int id, long startTime) in _startTimes)
        {
            Entity entity = scene.Store.GetEntityById(id);
            if (entity.IsNull)
                continue;

            long target = EditorTiming.Snap(startTime + (long)Math.Round(delta), playhead);
            target = Math.Clamp(target, 0, maxTime);
            if (target == startTime)
                continue;

            EditorItemTiming.Set(entity, target);
            moved = true;
        }

        if (moved)
        {
            _movedDrag = true;
            if (!_primary.IsNull && EditorItemTiming.TryGet(_primary, out long primaryTime))
                playhead.SeekRequestMs = primaryTime;
        }

        SetWindow(editor, true);
    }

    public void BeginScrub(Vector2 screenPosition, Rect2 bar, ref Playhead playhead, Entity editor)
    {
        if (playhead.DurationMs <= 0)
            return;

        (_windowStart, _windowEnd) = EditorLayout.TimelineWindow(playhead, TimelineOf(editor));
        Scrubbing = true;
        SetWindow(editor, true);
        Scrub(screenPosition, bar, ref playhead, editor, 0f);
    }

    public void Scrub(
        Vector2 screenPosition,
        Rect2 bar,
        ref Playhead playhead,
        Entity editor,
        float deltaTime)
    {
        if (!Scrubbing || playhead.DurationMs <= 0)
            return;

        if (editor.HasComponent<TimelineView>())
        {
            ref TimelineView view = ref editor.GetComponent<TimelineView>();
            if (TimelineScrub.Pan(ref view, bar, screenPosition.X, deltaTime, playhead.DurationMs))
                (_windowStart, _windowEnd) = (view.ScrubStartMs, view.ScrubEndMs);
        }

        float t = Math.Clamp((screenPosition.X - bar.Position.X) / bar.Size.X, 0f, 1f);
        long raw = _windowStart + (long)((_windowEnd - _windowStart) * t);
        playhead.SeekRequestMs = Math.Clamp(raw, 0, Math.Max(0, playhead.DurationMs));
    }

    /// <summary>Ends a scrub or drag; false when neither was running.</summary>
    public bool End(Entity editor)
    {
        if (Dragging)
        {
            Dragging = false;
            _drag.End();
            if (!editor.IsNull)
                SetWindow(editor, false);
            if (_movedDrag && !editor.IsNull)
                history.Record(_before, history.Capture());
            _startTimes.Clear();
            _primary = default;
            return true;
        }

        if (Scrubbing)
        {
            Scrubbing = false;
            if (!editor.IsNull)
                SetWindow(editor, false);
            return true;
        }

        return false;
    }

    public Entity FindNoteAt(Vector2 screenPosition, Rect2 ruler, Playhead playhead, Entity editor)
    {
        if (playhead.DurationMs <= 0)
            return default;

        foreach ((Entity note, Rect2 bounds) in Slots(editor, ruler, playhead))
        {
            if (EditorTimelineNotes.HitBounds(bounds).HasPoint(screenPosition))
                return note;
        }

        return default;
    }

    public List<(Entity Note, Rect2 Bounds)> Slots(Entity editor, Rect2 ruler, Playhead playhead)
    {
        TimelineView view = TimelineOf(editor);
        (long start, long end) = EditorLayout.TimelineWindow(playhead, view);
        return EditorTimelineNotes.Slots(scene.Store, ruler, start, end, view.Zoom);
    }

    /// <summary>The editor's timeline view, or a whole-song one when it has none.</summary>
    private static TimelineView TimelineOf(Entity editor) =>
        editor.HasComponent<TimelineView>()
            ? editor.GetComponent<TimelineView>()
            : new TimelineView(1f);

    /// <summary>Holds the window still while a drag or scrub runs, through the shared timeline scrub.</summary>
    private void SetWindow(Entity editor, bool active)
    {
        if (!editor.HasComponent<TimelineView>())
            return;

        ref TimelineView timeline = ref editor.GetComponent<TimelineView>();
        if (active)
            TimelineScrub.Hold(ref timeline, new TimeWindow(_windowStart, _windowEnd));
        else
            TimelineScrub.Release(ref timeline);
    }
}
