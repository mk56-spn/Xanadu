using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Editor.Commands;
using Writer.Settings;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Editor;

/// <summary>Opens and selects values from the ECS editor dropdowns.</summary>
public sealed class EditorDropdownSystem : QuerySystem<EditorDropdown>
{
    private static readonly Vector2 HiddenList = new(-10_000, -10_000);

    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;
    private readonly HashSet<int> _heldButtons = new();
    private readonly HashSet<int> _heldOptions = new();

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
        EditorNoteTarget noteTarget = default;

        foreach (Entity editor in _entities
                     .Query<Playhead, EditorNoteTarget>()
                     .ToEntityList())
        {
            playhead = editor.GetComponent<Playhead>();
            noteTarget = editor.GetComponent<EditorNoteTarget>();
            break;
        }

        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref dropdown, entity) =>
        {
            int expected = dropdown.Kind == EditorDropdownKind.Snap
                ? SnapIndex(playhead.SnapDivision)
                : (int)noteTarget.Target;

            dropdown.Selected = expected;
            SetDisplay(entity, ref dropdown, buffer);

            bool pressed = entity.Tags.Has<Pressed>();
            bool wasPressed = _heldButtons.Contains(entity.Id);

            if (pressed)
                _heldButtons.Add(entity.Id);
            else
                _heldButtons.Remove(entity.Id);

            if (!pressed || wasPressed)
                return;

            bool open = !dropdown.Open;

            foreach (Entity other in _entities.Query<EditorDropdown>().ToEntityList())
            {
                if (other.Id == entity.Id)
                    continue;

                ref EditorDropdown otherDropdown =
                    ref other.GetComponent<EditorDropdown>();
                otherDropdown.Open = false;
                Hide(otherDropdown.List, buffer);
            }

            dropdown.Open = open;
            SetListPosition(entity, ref dropdown, buffer);
        });

        foreach (Entity entity in _entities
                     .Query<EditorDropdownOption>()
                     .ToEntityList())
        {
            bool pressed = entity.Tags.Has<Pressed>();
            bool wasPressed = _heldOptions.Contains(entity.Id);

            if (pressed)
                _heldOptions.Add(entity.Id);
            else
                _heldOptions.Remove(entity.Id);

            if (!pressed || wasPressed)
                continue;

            EditorDropdownOption option = entity.GetComponent<EditorDropdownOption>();
            if (option.Owner.IsNull ||
                !option.Owner.HasComponent<EditorDropdown>())
            {
                continue;
            }

            ref EditorDropdown dropdown =
                ref option.Owner.GetComponent<EditorDropdown>();

            dropdown.Selected = option.Value;
            dropdown.Open = false;
            Hide(dropdown.List, buffer);

            if (dropdown.Kind == EditorDropdownKind.Snap)
                _commands.Enqueue(EditorCommand.SetSnap(SnapValue(option.Value)));
            else
                _commands.Enqueue(EditorCommand.SetTarget(
                    KeybindSettings.Actions[Math.Clamp(
                        option.Value,
                        0,
                        KeybindSettings.Actions.Length - 1)]));

            SetDisplay(option.Owner, ref dropdown, buffer);
        }

        foreach (Entity entity in _entities
                     .Query<EditorDropdownOption>()
                     .ToEntityList())
        {
            EditorDropdownOption option = entity.GetComponent<EditorDropdownOption>();
            if (option.Owner.IsNull ||
                !option.Owner.HasComponent<EditorDropdown>())
            {
                continue;
            }

            EditorDropdown dropdown = option.Owner.GetComponent<EditorDropdown>();

            if (!entity.HasComponent<Position>())
                continue;

            ref Position position = ref entity.GetComponent<Position>();

            if (!dropdown.Open || dropdown.List.IsNull)
            {
                position.Value = HiddenList;
            }
            else
            {
                Vector2 listPosition =
                    dropdown.List.GetComponent<Position>().Value;
                float listHeight =
                    dropdown.List.GetComponent<Rectangle>().Bounds.Size.Y;

                position.Value = new Vector2(
                    listPosition.X,
                    listPosition.Y -
                    listHeight / 2 +
                    18 +
                    option.Value * 30);
            }

            buffer.AddTag<Moved>(entity.Id);
        }
    }

    private void SetListPosition(
        Entity button,
        ref EditorDropdown dropdown,
        CommandBuffer buffer)
    {
        if (dropdown.List.IsNull)
            return;

        ref Position position = ref dropdown.List.GetComponent<Position>();
        if (!dropdown.Open)
        {
            position.Value = HiddenList;
        }
        else
        {
            ref Position buttonPosition = ref button.GetComponent<Position>();
            float buttonHeight = button.GetComponent<Rectangle>().Bounds.Size.Y;
            float listHeight = dropdown.List.GetComponent<Rectangle>().Bounds.Size.Y;

            position.Value = buttonPosition.Value + new Vector2(
                0,
                buttonHeight / 2 + listHeight / 2 + 4);
        }

        buffer.AddTag<Moved>(dropdown.List.Id);
    }

    private static void Hide(Entity list, CommandBuffer buffer)
    {
        if (list.IsNull || !list.HasComponent<Position>())
            return;

        ref Position position = ref list.GetComponent<Position>();
        position.Value = HiddenList;
        buffer.AddTag<Moved>(list.Id);
    }

    private static void SetDisplay(
        Entity button,
        ref EditorDropdown dropdown,
        CommandBuffer buffer)
    {
        if (button.IsNull || !button.HasComponent<Text>())
            return;

        string label = dropdown.Kind switch
        {
            EditorDropdownKind.Snap => $"SNAP  {SnapLabel(dropdown.Selected)}",
            _ => $"TYPE  {KeybindSettings.LabelFor(
                KeybindSettings.Actions[Math.Clamp(
                    dropdown.Selected,
                    0,
                    KeybindSettings.Actions.Length - 1)])}",
        };

        ref Text text = ref button.GetComponent<Text>();
        if (text.Value == label)
            return;

        text.Value = label;
        buffer.AddTag<Dirty>(button.Id);
    }

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
