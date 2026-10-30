using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Settings;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Attaches the selected blocks to a new Form shape and drops the construction
/// marker at the playhead. The blocks stay hidden until their steps are hit.
/// </summary>
public sealed class EditorShapeSystem : QuerySystem<EditorShapeAttachButton>
{
    private readonly EntityStore _entities;
    private readonly EditorHistory _history;
    private readonly HashSet<int> _held = new();

    public EditorShapeSystem(EntityStore entities, EditorHistory history)
    {
        _entities = entities;
        _history = history;
    }

    protected override void OnUpdate()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities
                     .Query<EditorShapeState, EditorShapeReadout>()
                     .ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        EditorShapeState state = editor.GetComponent<EditorShapeState>();

        foreach (Entity button in Query.ToEntityList())
        {
            bool pressed = button.Tags.Has<Pressed>();
            bool was = _held.Contains(button.Id);
            if (pressed)
                _held.Add(button.Id);
            else
                _held.Remove(button.Id);

            if (pressed && !was)
                state = Attach(editor, state);
        }

        editor.GetComponent<EditorShapeState>() = state;

        Entity readout = editor.GetComponent<EditorShapeReadout>().Text;
        if (readout.IsNull || !readout.HasComponent<Text>())
            return;

        string value = state.ShapeId == 0 ? "NO SHAPE" : $"SHAPE {state.ShapeId}";
        ref Text text = ref readout.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;
        CommandBuffer buffer = CommandBuffer;
        buffer.AddTag<Dirty>(readout.Id);
    }

    private EditorShapeState Attach(Entity editor, EditorShapeState state)
    {
        var selected = new List<Entity>();
        NoteSelection selection = editor.GetComponent<NoteSelection>();
        foreach (Entity entity in selection.AllNotes)
        {
            if (entity.HasComponent<Block>())
                selected.Add(entity);
        }

        if (selected.Count == 0)
            return state;

        int shapeId = NextShapeId();
        Vector2 center = Vector2.Zero;
        EditorSnapshot before = _history.Capture();
        for (int i = 0; i < selected.Count; i++)
        {
            Entity entity = selected[i];
            ref Block block = ref entity.GetComponent<Block>();
            block.ShapeId = shapeId;
            block.ShapePiece = i;
            entity.AddTag<ConstructionHidden>();
            center += block.Center;
        }

        center /= selected.Count;
        Playhead playhead = editor.GetComponent<Playhead>();
        KeybindAction target = editor.HasComponent<EditorNoteTarget>()
            ? editor.GetComponent<EditorNoteTarget>().Target
            : KeybindAction.L1;
        if (target == KeybindAction.M)
            target = KeybindAction.L1;

        EditorItems.Create(_entities, new Note(
            EditorTiming.Snap(playhead.TimeMs, playhead),
            center,
            target: target,
            kind: NoteKind.Construction,
            shapeId: shapeId));
        _history.Record(before, _history.Capture());

        state.ShapeId = shapeId;
        return state;
    }

    private int NextShapeId()
    {
        int max = 0;
        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
            max = System.Math.Max(max, entity.GetComponent<Block>().ShapeId);
        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
            max = System.Math.Max(max, entity.GetComponent<Note>().ShapeId);
        return max + 1;
    }
}
