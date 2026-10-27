using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>Editor display and command semantics for generic UI dropdowns.</summary>
public sealed class EditorDropdownSystem : QuerySystem<Dropdown>
{
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;

    public EditorDropdownSystem(
        EntityStore entities,
        EditorCommandBus commands)
    {
        _entities = entities;
        _commands = commands;
    }

    protected override void OnUpdate()
    {
        Playhead playhead = default;
        EditorCharacterTarget characterTarget = default;
        foreach (Entity editor in _entities
                     .Query<Playhead>()
                     .ToEntityList())
        {
            playhead = editor.GetComponent<Playhead>();
            characterTarget = editor.HasComponent<EditorCharacterTarget>()
                ? editor.GetComponent<EditorCharacterTarget>()
                : new EditorCharacterTarget(CharacterKind.Contact);
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
                Commit(kind, changed.Value);
                buffer.RemoveComponent<DropdownChanged>(entity.Id);
            }
            else
            {
                dropdown.Selected = Expected(kind, playhead, characterTarget);
            }

            SetDisplay(entity, ref dropdown, kind, buffer);
        });
    }

    private void Commit(EditorDropdownKind kind, int value)
    {
        if (kind == EditorDropdownKind.Snap)
        {
            _commands.Enqueue(EditorCommand.SetSnap(SnapValue(value)));
        }
        else if (kind == EditorDropdownKind.Character)
        {
            foreach (Entity editor in _entities
                         .Query<EditorCharacterTarget>()
                         .ToEntityList())
            {
                editor.GetComponent<EditorCharacterTarget>().Target =
                    (CharacterKind)Math.Clamp(value, 0, 0);
                break;
            }
        }

    }

    private static int Expected(
        EditorDropdownKind kind,
        Playhead playhead,
        EditorCharacterTarget characterTarget) => kind switch
    {
        EditorDropdownKind.Snap => SnapIndex(playhead.SnapDivision),
        _ => (int)characterTarget.Target,
    };

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
            EditorDropdownKind.Snap => $"SNAP  {SnapLabel(dropdown.Selected)}",
            _ => $"CHAR  {CharacterLabel(dropdown.Selected)}",
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

    private static string CharacterLabel(int index) => "CONTACT";

    private static int SnapIndex(int division) => division switch
    {
        8 => 1,
        16 => 2,
        _ => 0,
    };

    private static int SnapValue(int index) => index switch
    {
        1 => 8,
        2 => 16,
        _ => 4,
    };

    private static string SnapLabel(int index) => index switch
    {
        1 => "1/8",
        2 => "1/16",
        _ => "1/4",
    };
}
