using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The Splitline rail layer's authoring buttons. Rail taps and holds are placed
/// at the playhead with the selected side key and hold length; channel end
/// markers are placed the same way.
/// </summary>
public sealed class EditorRailLayerSystem : QuerySystem<EditorButton>
{
    private static readonly long[] Holds = { 0, 250, 500, 1_000, 2_000 };

    private readonly EntityStore _entities;
    private readonly EditorHistory _history;
    private readonly ButtonPress _presses = new();

    public EditorRailLayerSystem(EntityStore entities, EditorHistory history)
    {
        _entities = entities;
        _history = history;

        // A press can land on a control that hides itself the same frame.
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<EditorPlacement>(_entities);

        if (editor.IsNull)
            return;

        foreach (Entity entity in Query.ToEntityList())
        {
            EditorButton button = entity.GetComponent<EditorButton>();
            if (button.Kind is not (EditorButtonKind.RailTap or EditorButtonKind.ChannelEnd or EditorButtonKind.RailHold))
                continue;
            if (!PressedNow(entity))
                continue;

            ref EditorPlacement state = ref editor.GetComponent<EditorPlacement>();
            switch (button.Kind)
            {
                case EditorButtonKind.RailTap:
                    PlaceRail((KeybindAction)button.Arg, editor, state.RailHoldMs);
                    break;
                case EditorButtonKind.ChannelEnd:
                    PlaceEnd((ChannelSide)button.Arg, editor);
                    break;
                case EditorButtonKind.RailHold:
                    state.RailHoldMs = NextHold(state.RailHoldMs);
                    break;
            }
        }

        long holdMs = editor.GetComponent<EditorPlacement>().RailHoldMs;
        EditorReadout.Show(
            _entities,
            EditorReadoutKind.RailHold,
            holdMs == 0 ? "TAP" : $"HOLD {holdMs}",
            CommandBuffer);
    }

    private bool PressedNow(Entity entity) => _presses.Started(entity);

    private void PlaceRail(KeybindAction action, Entity editor, long holdMs)
    {
        if (EditorItems.WouldOverfill(_entities, editor))
            return;

        Playhead playhead = editor.GetComponent<Playhead>();
        long time = EditorTiming.Snap(playhead.TimeMs, playhead);
        EditorSnapshot before = _history.Capture();
        EditorItems.Place(_entities, new Note(
            time,
            Vector2.Zero,
            target: action,
            kind: NoteKind.Rail,
            pairIndex: NoteRules.PairIndexFor(action),
            channelSide: NoteRules.SideOf(action),
            holdMs: holdMs));
        _history.Record(before, _history.Capture());
        EditorAutoAdvance.AfterPlacing(_entities, editor, time);
    }

    private void PlaceEnd(ChannelSide side, Entity editor)
    {
        if (EditorItems.WouldOverfill(_entities, editor))
            return;

        Playhead playhead = editor.GetComponent<Playhead>();
        long time = EditorTiming.Snap(playhead.TimeMs, playhead);
        EditorSnapshot before = _history.Capture();
        EditorItems.Place(_entities, new Note(
            time,
            Vector2.Zero,
            kind: NoteKind.ChannelEnd,
            channelSide: side));
        _history.Record(before, _history.Capture());
        EditorAutoAdvance.AfterPlacing(_entities, editor, time);
    }

    private static long NextHold(long current)
    {
        int index = System.Array.IndexOf(Holds, current);
        return Holds[(index + 1) % Holds.Length];
    }
}
