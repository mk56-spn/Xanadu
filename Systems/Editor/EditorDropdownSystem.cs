using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Rendering;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>Editor display and command semantics for generic UI dropdowns.</summary>
public sealed class EditorDropdownSystem : QuerySystem<Dropdown>
{
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;
    private readonly EditorHistory _history;

    public EditorDropdownSystem(
        EntityStore entities,
        EditorCommandBus commands,
        EditorHistory history)
    {
        _entities = entities;
        _commands = commands;
        _history = history;
    }

    protected override void OnUpdate()
    {
        NoteSelection selection = default;
        foreach (Entity editor in _entities
                     .Query<NoteSelection>()
                     .ToEntityList())
        {
            selection = editor.GetComponent<NoteSelection>();
            break;
        }

        CommandBuffer buffer = CommandBuffer;
        Query.ForEachEntity((ref dropdown, entity) =>
        {
            if (!entity.HasComponent<EditorDropdownControl>())
                return;

            EditorDropdownKind kind =
                entity.GetComponent<EditorDropdownControl>().Kind;

            if (entity.TryGetComponent<DropdownChanged>(
                    out DropdownChanged changed))
            {
                dropdown.Selected = changed.Value;
                Commit(kind, changed.Value, selection);
                buffer.RemoveComponent<DropdownChanged>(entity.Id);
            }
            else
            {
                dropdown.Selected = Expected(kind, selection);
            }

            // A blend list means nothing with nothing selected; disabling it
            // also closes a list left open when the selection went away.
            if (kind == EditorDropdownKind.Blend)
                UiLayout.SetDisabled(entity, buffer, !HasItems(selection));

            SetDisplay(entity, ref dropdown, kind, buffer);
        });
    }

    private void Commit(EditorDropdownKind kind, int value, NoteSelection selection)
    {
        if (kind == EditorDropdownKind.Blend)
        {
            var items = new List<Entity>();
            foreach (Entity item in selection.AllNotes)
            {
                if (EditorItems.IsItem(item))
                    items.Add(item);
            }

            if (items.Count == 0)
                return;

            int index = Math.Clamp(value, 0, BlendModes.All.Length - 1);
            EditorSnapshot before = _history.Capture();
            if (EditorLayerEdits.SetBlend(items, BlendModes.All[index]))
                _history.Record(before, _history.Capture());
        }
    }

    private static bool HasItems(NoteSelection selection)
    {
        foreach (Entity item in selection.AllNotes)
        {
            if (EditorItems.IsItem(item))
                return true;
        }

        return false;
    }

    private static int Expected(
        EditorDropdownKind kind,
        NoteSelection selection) => kind switch
    {
        _ => BlendIndex(selection),
    };

    private static int BlendIndex(NoteSelection selection)
    {
        foreach (Entity item in selection.AllNotes)
        {
            if (!EditorItems.IsItem(item))
                continue;

            int index = Array.IndexOf(BlendModes.All, ItemBlend.Of(item));
            return index < 0 ? 0 : index;
        }

        return 0;
    }

    private static void SetDisplay(
        Entity button,
        ref Dropdown dropdown,
        EditorDropdownKind kind,
        CommandBuffer buffer)
    {
        if (!button.HasComponent<Text>())
            return;

        string label = kind switch
        {
            _ => $"BLEND  {BlendModes.Name(BlendModes.All[Math.Clamp(dropdown.Selected, 0, BlendModes.All.Length - 1)])}  ▾",
        };

        ref Text text = ref button.GetComponent<Text>();
        bool changed = false;
        if (text.Value != label)
        {
            text.Value = label;
            changed = true;
        }

        if (changed)
            buffer.AddTag<Dirty>(button.Id);
    }
}
