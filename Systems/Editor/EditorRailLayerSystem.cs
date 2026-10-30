using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The Splitline rail layer's authoring buttons. Rail taps and holds are placed
/// at the playhead with the selected side key and hold length; channel end
/// markers are placed the same way.
/// </summary>
public sealed class EditorRailLayerSystem : QuerySystem<EditorRailButton>
{
    private static readonly long[] Holds = { 0, 250, 500, 1_000, 2_000 };

    private readonly EntityStore _entities;
    private readonly EditorHistory _history;
    private readonly HashSet<int> _held = new();

    public EditorRailLayerSystem(EntityStore entities, EditorHistory history)
    {
        _entities = entities;
        _history = history;
    }

    protected override void OnUpdate()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities
                     .Query<EditorRailHoldState, EditorRailHoldReadout>()
                     .ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        EditorRailHoldState state = editor.GetComponent<EditorRailHoldState>();

        foreach (Entity entity in Query.ToEntityList())
        {
            if (!PressedNow(entity))
                continue;

            PlaceRail(
                entity.GetComponent<EditorRailButton>().Action,
                editor,
                state.HoldMs);
        }

        foreach (Entity entity in _entities
                     .Query<EditorChannelEndButton>()
                     .ToEntityList())
        {
            if (!PressedNow(entity))
                continue;

            PlaceEnd(
                entity.GetComponent<EditorChannelEndButton>().Side,
                editor);
        }

        foreach (Entity entity in _entities
                     .Query<EditorRailHoldButton>()
                     .ToEntityList())
        {
            if (!PressedNow(entity))
                continue;

            state.HoldMs = NextHold(state.HoldMs);
            editor.GetComponent<EditorRailHoldState>() = state;
        }

        Entity readout = editor.GetComponent<EditorRailHoldReadout>().Text;
        if (!readout.IsNull && readout.HasComponent<Text>())
        {
            string value = state.HoldMs == 0 ? "TAP" : $"HOLD {state.HoldMs}";
            ref Text text = ref readout.GetComponent<Text>();
            if (text.Value != value)
            {
                text.Value = value;
                CommandBuffer buffer = CommandBuffer;
                buffer.AddTag<Dirty>(readout.Id);
            }
        }
    }

    private bool PressedNow(Entity entity)
    {
        bool pressed = entity.Tags.Has<Pressed>();
        bool was = _held.Contains(entity.Id);
        if (pressed)
            _held.Add(entity.Id);
        else
            _held.Remove(entity.Id);
        return pressed && !was;
    }

    private void PlaceRail(KeybindAction action, Entity editor, long holdMs)
    {
        Playhead playhead = editor.GetComponent<Playhead>();
        long time = EditorTiming.Snap(playhead.TimeMs, playhead);
        EditorSnapshot before = _history.Capture();
        EditorItems.Create(_entities, new Note(
            time,
            Vector2.Zero,
            target: action,
            kind: NoteKind.Rail,
            pairIndex: NoteRules.PairIndexFor(action),
            channelSide: NoteRules.SideOf(action),
            holdMs: holdMs));
        _history.Record(before, _history.Capture());
    }

    private void PlaceEnd(ChannelSide side, Entity editor)
    {
        Playhead playhead = editor.GetComponent<Playhead>();
        long time = EditorTiming.Snap(playhead.TimeMs, playhead);
        EditorSnapshot before = _history.Capture();
        EditorItems.Create(_entities, new Note(
            time,
            Vector2.Zero,
            kind: NoteKind.ChannelEnd,
            channelSide: side));
        _history.Record(before, _history.Capture());
    }

    private static long NextHold(long current)
    {
        int index = System.Array.IndexOf(Holds, current);
        return Holds[(index + 1) % Holds.Length];
    }
}
