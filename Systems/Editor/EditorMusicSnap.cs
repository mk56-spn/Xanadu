using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>Marks the toggle in the group bar's music menu that turns music snap on and off.</summary>
public struct EditorMusicSnapToggle : ITag;

/// <summary>
/// Music snap: placed notes land on the beat grid. The toggle's <see cref="Toggle"/>
/// is the switch; <see cref="EditorViewBarSystem"/> copies it onto the
/// playhead (<see cref="Playhead.SnapOff"/>), which <see cref="EditorTiming.Snap"/>
/// reads. It starts on, and an editor with no toggle snaps.
/// </summary>
public static class EditorMusicSnap
{
    public const string OnLabel = "MUSIC SNAP ON";
    public const string OffLabel = "MUSIC SNAP OFF";

    public static Entity Attach(Entity button)
    {
        button.AddComponent(new Toggle(OnLabel, OffLabel, true));
        button.AddTag<EditorMusicSnapToggle>();
        button.AddComponent(new UiTooltip("Place notes on the beat grid. Off: notes keep the exact time they are put at"));
        return button;
    }

    public static bool IsOn(EntityStore store)
    {
        foreach (Entity toggle in store.Query<Toggle>().AllTags(Tags.Get<EditorMusicSnapToggle>()).WithDisabled().Entities)
            return toggle.GetComponent<Toggle>().Value;

        return true;
    }
}
