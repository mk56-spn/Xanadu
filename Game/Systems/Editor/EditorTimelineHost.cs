using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Systems.Timeline;
using Xanadu.Timeline;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The song as a shared timeline. Its markers are the placed items; its only
/// row is the ruler strip, where each note is a square stacked with the others
/// at its time. The shared <see cref="TimelineInputSystem"/> reads presses and
/// drags; this says what they do: a press picks a note and seeks to it, a drag
/// retimes the whole selection on the beat grid, a plain press on empty strip
/// scrubs and a Shift-drag boxes notes.
/// </summary>
public sealed class EditorTimelineHost(
    Entity editor,
    EditorCommandBus commands,
    Action<string> setStatus,
    EditorHistory? history = null) : ITimelineHost, ITimelineInput
{
    private readonly Dictionary<int, long> _startTimes = new();
    private EditorSnapshot _before;
    private bool _moved;

    private Playhead Playhead => editor.GetComponent<Playhead>();

    public double NowMs => Playhead.TimeMs;

    public double DurationMs => Playhead.DurationMs;

    public double StepMs => EditorTiming.StepMs(Playhead);

    public double StepOriginMs => EditorTiming.StepOriginMs(Playhead);

    public bool Blocked
    {
        get
        {
            EntityStore store = editor.Store;
            if (store.Query<WarningPopup>().Count > 0)
                return true;

            foreach (Entity entity in store.Query<EditorGroupState>().ToEntityList())
            {
                if (entity.GetComponent<EditorGroupState>().Open >= 0)
                    return true;
            }

            return false;
        }
    }

    public bool TryGetArea(out Rect2 panel, out Rect2 ruler)
    {
        panel = EditorLayout.RulerRect(editor.GetComponent<PlayfieldView>());
        ruler = default;
        return true;
    }

    public IEnumerable<double> MarkerTimesMs()
    {
        foreach (Entity item in editor.Store.Query().AllTags(Tags.Get<EditorItem>()).ToEntityList())
        {
            if (EditorItemTiming.TryGet(item, out long time))
                yield return time;
        }
    }

    public void SeekMs(double ms) => commands.Enqueue(EditorCommand.Seek((long)Math.Round(ms)));

    public void TogglePlay() => commands.Enqueue(EditorCommand.TogglePlayback());

    public void NoMarker(int direction) =>
        setStatus(direction < 0 ? "No earlier note" : "No later note");

    public TimelineScene? Scene()
    {
        var query = new EditorSceneQuery(editor.Store);
        Playhead playhead = Playhead;
        if (playhead.DurationMs <= 0 || query.IsModalOpen() || query.IsGroupTargetEditing())
            return null;

        Rect2 ruler = EditorLayout.RulerRect(editor.GetComponent<PlayfieldView>());
        TimelineView view = editor.GetComponent<TimelineView>();
        (long start, long end) = EditorLayout.TimelineWindow(playhead, view);
        NoteSelection selection = editor.GetComponent<NoteSelection>();
        var scene = new TimelineScene
        {
            Panel = ruler,
            Axis = new TimelineAxis(ruler, start, end),
            ScrubBand = ruler,
            DurationMs = playhead.DurationMs,
            Snap = ms => EditorTiming.Snap((long)Math.Round(ms), playhead),
            SnapScrub = false,
            ShiftBoxesOnScrub = true,
            Lanes = new TimelineLanes(ruler, new Rect2(ruler.Position, new Vector2(0, ruler.Size.Y)),
                ruler.Size.Y, 1, 0, 1, default, default, 0),
            Selection = ToObjects(selection.AllNotes),
        };

        foreach ((Entity note, Rect2 square) in EditorTimelineNotes.Slots(editor.Store, ruler, start, end, view.Zoom))
        {
            scene.Items.Add(TimelineItem.Marker(note, 0, note.GetComponent<Note>().TimeMs, area: EditorTimelineNotes.HitBounds(square)) with
            {
                Reach = float.MaxValue,
                Size = square.Size.X / 2,
                Selected = selection.Contains(note),
            });
        }

        return scene;
    }

    public void GestureStarted(TimelineGestureKind kind)
    {
    }

    public void GestureEnded(TimelineGestureKind kind)
    {
        _startTimes.Clear();
        _moved = false;
        _before = default;
    }

    public void ScrollRows(int first)
    {
    }

    public void Scrub(double ms)
    {
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.SeekRequestMs = Math.Clamp((long)Math.Round(ms), 0, Math.Max(0, playhead.DurationMs));
    }

    public void LabelPressed(int lane, Vector2 point)
    {
    }

    /// <summary>Empty strip only reaches here with Shift, as a box that adds to the selection.</summary>
    public void LanePressed(int lane, double ms, bool additive)
    {
    }

    /// <summary>
    /// A note pressed on the strip is picked as the shared timeline says and the
    /// playhead goes to it; the selection as it then stands is what a drag moves.
    /// </summary>
    public void ItemPressed(in TimelineItem item, TimelinePick pick, TimelineGrabPart part)
    {
        if (item.Ref is not Entity note)
            return;

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();
        if (pick == TimelinePick.Only)
            selection.Note = note;
        else if (pick == TimelinePick.Add)
            selection.Toggle(note);

        if (EditorItemTiming.TryGet(note, out long time))
            editor.GetComponent<Playhead>().SeekRequestMs = time;

        _before = history?.Capture() ?? default;
        _moved = false;
        _startTimes.Clear();
        foreach (Entity selected in selection.AllNotes)
        {
            if (EditorItems.IsItem(selected) && EditorItemTiming.TryGet(selected, out long start))
                _startTimes[selected.Id] = start;
        }
    }

    /// <summary>Moves every selected item by as much as the grabbed note moved, each landing on the grid.</summary>
    public void ItemMoved(in TimelineItem item, double fromMs, double toMs)
    {
        if (item.Ref is not Entity grabbed)
            return;

        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        long delta = (long)Math.Round(fromMs - item.FromMs);
        long maxTime = playhead.DurationMs > 0 ? playhead.DurationMs : long.MaxValue;
        foreach ((int id, long start) in _startTimes)
        {
            Entity entity = editor.Store.GetEntityById(id);
            if (entity.IsNull || !EditorItemTiming.TryGet(entity, out long now))
                continue;

            long target = Math.Clamp(EditorTiming.Snap(start + delta, playhead), 0, maxTime);
            if (target == now)
                continue;

            EditorItemTiming.Set(entity, target);
            _moved = true;
        }

        if (_moved && EditorItemTiming.TryGet(grabbed, out long grabbedTime))
            playhead.SeekRequestMs = grabbedTime;
    }

    /// <summary>A Shift-click on a selected note takes it out; a drag that moved anything is one undo step.</summary>
    public void ItemReleased(in TimelineItem item, bool remove)
    {
        if (remove && item.Ref is Entity note)
            editor.GetComponent<NoteSelection>().Toggle(note);

        if (_moved && history is not null)
            history.Record(_before, history.Capture());
    }

    public void BoxSelected(IReadOnlyList<object> selection)
    {
        var notes = new List<Entity>(selection.Count);
        foreach (object item in selection)
        {
            if (item is Entity entity)
                notes.Add(entity);
        }

        editor.GetComponent<NoteSelection>().SetMany(notes);
    }

    private static List<object> ToObjects(IReadOnlyList<Entity> entities)
    {
        var list = new List<object>(entities.Count);
        foreach (Entity entity in entities)
            list.Add(entity);
        return list;
    }
}
