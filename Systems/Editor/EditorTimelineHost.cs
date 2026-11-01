using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Systems.Timeline;

namespace Xanadu.Systems.Editor;

/// <summary>The song as a shared timeline; its markers are the placed items, and its strip has no zoom ruler.</summary>
public sealed class EditorTimelineHost(
    Entity editor,
    EditorCommandBus commands,
    Action<string> setStatus) : ITimelineHost
{
    private Playhead Playhead => editor.GetComponent<Playhead>();

    public double NowMs => Playhead.TimeMs;

    public double DurationMs => Playhead.DurationMs;

    public double StepMs => EditorTiming.StepMs(Playhead);

    public double StepOriginMs => Playhead.OffsetMs;

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
}
