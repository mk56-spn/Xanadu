using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Levels;

namespace Xanadu.Systems.Editor;

public sealed partial class EditorCommandSystem
{
    private static void ImportShader(EditorCommand command, Entity editor)
    {
        string source = command.Text2 ?? "";
        if (!LevelShaders.IsSlot(command.IntValue) ||
            !editor.HasComponent<LevelShaders>() ||
            !ShaderSourceRules.IsAcceptable(source, out _))
        {
            return;
        }

        editor.GetComponent<LevelShaders>().Slots[command.IntValue - 1] = new ShaderSlot
        {
            Name = command.Text ?? "",
            Source = source,
        };
    }

    private void RemoveShader(int slot, Entity editor)
    {
        if (!LevelShaders.IsSlot(slot) || !editor.HasComponent<LevelShaders>())
            return;

        // Not recorded for undo: the source itself is gone, so undoing only the
        // assignments would leave items pointing at an empty slot.
        ref LevelShaders shaders = ref editor.GetComponent<LevelShaders>();
        shaders.Slots[slot - 1] = null;
        if (shaders.PlayerSlot == slot)
            shaders.PlayerSlot = 0;
        if (shaders.FieldSlot == slot)
            shaders.FieldSlot = 0;

        _afterQuery.Add(() =>
        {
            foreach (Entity item in _entities.Query<ItemShader>().ToEntityList())
            {
                if (item.GetComponent<ItemShader>().Slot == slot)
                    item.RemoveComponent<ItemShader>();
            }
        });
    }

    private void AssignShader(int slot, Entity editor)
    {
        if ((slot != 0 && !LevelShaders.IsSlot(slot)) || !editor.HasComponent<NoteSelection>())
            return;

        var items = new List<Entity>();
        foreach (Entity entity in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (!entity.IsNull &&
                (entity.HasComponent<Note>() || entity.HasComponent<Mine>() || entity.HasComponent<Block>()))
            {
                items.Add(entity);
            }
        }

        if (items.Count == 0)
            return;

        EditorSnapshot before = _history.Capture();
        _afterQuery.Add(() =>
        {
            foreach (Entity item in items)
            {
                if (slot == 0)
                    item.RemoveComponent<ItemShader>();
                else
                    item.AddComponent(new ItemShader(slot));
            }

            _history.Record(before, _history.Capture());
        });
    }

    private static void SetSpecialShader(EditorCommand command, Entity editor)
    {
        int slot = command.IntValue;
        if ((slot != 0 && !LevelShaders.IsSlot(slot)) || !editor.HasComponent<LevelShaders>())
            return;

        ref LevelShaders shaders = ref editor.GetComponent<LevelShaders>();
        if (command.Type == EditorCommandType.SetPlayerShader)
            shaders.PlayerSlot = slot;
        else
            shaders.FieldSlot = slot;
    }
}
