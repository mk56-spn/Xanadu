using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

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

    /// <summary>The switch's name in its store (<see cref="UiToggles.Switch"/>).</summary>
    public const string Id = "editor-music-snap";

    public static Entity Attach(Entity button)
    {
        button.AddComponent(new Toggle(OnLabel, OffLabel, true));
        button.Switch(Id);
        button.AddComponent(new UiTooltip("Place notes on the beat grid. Off: notes keep the exact time they are put at"));
        return button;
    }

    public static bool IsOn(EntityStore store) => UiToggles.On(store, Id, none: true);
}
