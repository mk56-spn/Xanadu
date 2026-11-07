using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Settings;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Attaches the selected blocks to a new Form shape and drops the construction
/// marker at the playhead. The blocks stay hidden until their steps are hit.
/// </summary>
public sealed class EditorShapeSystem : QuerySystem<EditorButton>
{
    private readonly EntityStore _entities;
    private readonly EditorHistory _history;
    private readonly ButtonPress _presses = new();

    public EditorShapeSystem(EntityStore entities, EditorHistory history)
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

        int shapeId = editor.GetComponent<EditorPlacement>().ShapeId;

        foreach (Entity button in Query.ToEntityList())
        {
            if (button.GetComponent<EditorButton>().Kind != EditorButtonKind.ShapeAttach)
                continue;

            if (_presses.Started(button))
                shapeId = Attach(editor, shapeId);
        }

        editor.GetComponent<EditorPlacement>().ShapeId = shapeId;
        EditorReadout.Show(
            _entities,
            EditorReadoutKind.Shape,
            shapeId == 0 ? "NO SHAPE" : $"SHAPE {shapeId}",
            CommandBuffer);
    }

    /// <summary>Attaches the selected blocks to a new shape and returns its id, or the current one when nothing is selected.</summary>
    private int Attach(Entity editor, int current)
    {
        var selected = new List<Entity>();
        NoteSelection selection = editor.GetComponent<NoteSelection>();
        foreach (Entity entity in selection.AllNotes)
        {
            if (entity.HasComponent<Block>())
                selected.Add(entity);
        }

        if (selected.Count == 0 || EditorItems.WouldOverfill(_entities, editor))
            return current;

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
        KeybindAction target = editor.HasComponent<EditorPlacement>()
            ? editor.GetComponent<EditorPlacement>().Note
            : KeybindAction.L1;
        if (target == KeybindAction.M)
            target = KeybindAction.L1;

        EditorItems.Place(_entities, new Note(
            EditorTiming.Snap(playhead.TimeMs, playhead),
            center,
            target: target,
            kind: NoteKind.Construction,
            shapeId: shapeId));
        _history.Record(before, _history.Capture());

        return shapeId;
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
