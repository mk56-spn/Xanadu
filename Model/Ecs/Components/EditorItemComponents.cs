using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ui.Systems.Interaction;

namespace Xanadu.Model.Ecs.Components;

/// <summary>Marks an editable playfield entity. Editor-only; never serialized.</summary>
public struct EditorItem : ITag;

/// <summary>Editor selection backed by the shared UI selection set.</summary>
public struct NoteSelection : IComponent
{
    private readonly UiSelectionSet _selection = new();

    public NoteSelection(Entity note)
    {
        _selection = new UiSelectionSet();
        Note = note;
    }

    public Entity Note
    {
        readonly get => _selection.Primary;
        set
        {
            if (!value.IsNull && !value.Tags.Has<EditorItem>())
                value.AddTag<EditorItem>();
            _selection.Set(value);
        }
    }

    public readonly IReadOnlyList<Entity> Notes => _selection.All;

    public readonly IReadOnlyList<Entity> AllNotes => _selection.All;

    public readonly bool Contains(Entity entity) => _selection.Contains(entity);

    public void SetMany(IEnumerable<Entity> notes)
    {
        foreach (Entity entity in notes)
        {
            if (!entity.IsNull && !entity.Tags.Has<EditorItem>())
                entity.AddTag<EditorItem>();
        }

        _selection.SetMany(notes);
    }

    public void Toggle(Entity entity)
    {
        if (!entity.IsNull && !entity.Tags.Has<EditorItem>())
            entity.AddTag<EditorItem>();
        _selection.Toggle(entity);
    }

    public void Clear() => _selection.Clear();
}

/// <summary>
/// The level editor's hold on the level's groups (<see cref="LevelGroups"/>):
/// which one is picked, whether keys go to one selected member instead of the
/// whole group (the escape hatch; groups are the usual way), and whether the
/// ease picker is open for the keys at the playhead. Editor state, never saved.
/// </summary>
public struct EditorGroupEdit : IComponent
{
    /// <summary>The picked group's index, or -1.</summary>
    public int Picked;

    public bool OneMember;

    public bool Easing;

    /// <summary>The group the panel's top row shows, when there are more than its rows fit.</summary>
    public int FirstRow;

    public static EditorGroupEdit None => new() { Picked = -1 };
}
