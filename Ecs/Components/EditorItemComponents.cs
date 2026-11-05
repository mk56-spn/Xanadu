using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Settings;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

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

/// <summary>Current level file path for Save (empty until saved).</summary>
public struct LevelFile(string path) : IComponent
{
    public string Path = path;
}
