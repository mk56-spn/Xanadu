using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>Marks the header button that turns auto-advance on and off.</summary>
public struct EditorAutoAdvanceToggle : ITag;

/// <summary>
/// Moves the playhead on by one snap after a note is placed, so a run of notes
/// can be laid down click after click. The header button's <see cref="Toggle"/>
/// is the state; it starts off, and an editor with no button never advances.
/// </summary>
public static class EditorAutoAdvance
{
    public const string Label = "ADV";
    public const float ToggleWidth = 64f;

    public static Entity Attach(Entity button)
    {
        button.AddComponent(new Toggle(Label, Label, false));
        button.AddTag<EditorAutoAdvanceToggle>();
        button.AddComponent(new UiTooltip("Move the playhead on one snap after placing a note"));
        return button;
    }

    public static bool IsOn(EntityStore store)
    {
        foreach (Entity toggle in store.Query<Toggle>().AllTags(Tags.Get<EditorAutoAdvanceToggle>()).Entities)
            return toggle.GetComponent<Toggle>().Value;

        return false;
    }

    /// <summary>After a note at <paramref name="placedMs"/>, seeks to the next snap when auto-advance is on.</summary>
    public static void AfterPlacing(EntityStore store, Entity editor, long placedMs)
    {
        if (!IsOn(store) || !editor.HasComponent<Playhead>())
            return;

        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        long next = Next(placedMs, playhead);
        playhead.TimeMs = next;
        playhead.SeekRequestMs = next;
    }

    /// <summary>The snap after <paramref name="placedMs"/>, held inside the song when its length is known.</summary>
    public static long Next(long placedMs, Playhead playhead)
    {
        long next = playhead.SnapOff
            ? placedMs + (long)Math.Round(EditorTiming.StepMs(playhead))
            : EditorTiming.Step(placedMs, playhead, 1);
        return playhead.DurationMs > 0 ? Math.Min(next, playhead.DurationMs) : next;
    }
}
