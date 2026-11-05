using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>The sidebar's colour field on the editor entity.</summary>
public struct EditorSelfModulateField(Entity field) : IComponent
{
    public Entity Field = field;
}

/// <summary>
/// Sets the self modulate of the selected notes, mines, blocks and spikes from
/// the sidebar's colour field, as one undo step. A typed colour goes to what was
/// selected when typing began, since clicking another item is what ends typing.
/// A dragged picker colours them as it moves, and the whole drag is one step.
/// </summary>
public sealed class EditorSelfModulateSystem(EditorHistory history) : QuerySystem<NoteSelection, EditorSelfModulateField>
{
    private readonly List<Entity> _targets = new();
    private EditorSnapshot? _before;
    private bool _wasEditing;

    protected override void OnUpdate()
    {
        foreach (Entity editor in Query.ToEntityList())
            Update(editor);
    }

    private void Update(Entity editor)
    {
        Entity field = editor.GetComponent<EditorSelfModulateField>().Field;
        if (field.IsNull || !field.HasComponent<UiColorField>())
            return;

        NoteSelection selection = editor.GetComponent<NoteSelection>();
        bool editing = UiColorField.IsEditing(field);
        if (editing && !_wasEditing)
        {
            _targets.Clear();
            foreach (Entity item in selection.AllNotes)
            {
                if (Tintable(item))
                    _targets.Add(item);
            }

            _before = history.Capture();
        }
        _wasEditing = editing;

        if (UiColorField.TakeLive(field, out Color live))
            Put(live);
        if (UiColorField.TakeCommit(field, out Color color))
            Apply(color);
        if (!editing)
            _before = null;

        Color shown = Colors.White;
        foreach (Entity item in selection.AllNotes)
        {
            if (!Tintable(item))
                continue;
            shown = SelfModulate.Of(item);
            break;
        }
        UiColorField.Show(field, shown);
    }

    private void Put(Color color)
    {
        _targets.RemoveAll(item => item.IsNull);
        foreach (Entity item in _targets)
            SelfModulate.Put(item, color);
    }

    /// <summary>Records against the state from before the edit began, so colours shown live are part of the step.</summary>
    private void Apply(Color color)
    {
        _targets.RemoveAll(item => item.IsNull);
        if (_targets.Count == 0)
            return;

        EditorSnapshot before = _before ?? history.Capture();
        Put(color);
        history.Record(before, history.Capture());
    }

    private static bool Tintable(Entity item) =>
        !item.IsNull &&
        (item.HasComponent<Note>() || item.HasComponent<Mine>() ||
         item.HasComponent<Block>() || item.HasComponent<Spike>() ||
         item.HasComponent<LevelText>() || item.HasComponent<GrassPatch>() ||
         item.HasComponent<LevelPolygon>());
}
