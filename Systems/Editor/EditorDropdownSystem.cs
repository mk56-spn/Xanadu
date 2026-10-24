using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Editor.Commands;
using Writer.Settings;
using Writer.Systems.Playfields;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Editor;

/// <summary>Opens and selects values from the ECS editor dropdowns.</summary>
public sealed class EditorDropdownSystem : QuerySystem<Dropdown>
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
        EditorCharacterTarget characterTarget = default;

        foreach (Entity editor in _entities
                     .Query<Playhead, EditorNoteTarget>()
                     .ToEntityList())
        {
            playhead = editor.GetComponent<Playhead>();
            noteTarget = editor.GetComponent<EditorNoteTarget>();
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

            int expected = kind switch
            {
                EditorDropdownKind.Snap => SnapIndex(playhead.SnapDivision),
                EditorDropdownKind.Character => (int)characterTarget.Target,
                _ => (int)noteTarget.Target,
            };

            dropdown.Selected = expected;
            SetDisplay(entity, ref dropdown, kind, buffer);

            bool pressed = entity.Tags.Has<Pressed>();
            bool wasPressed = _heldButtons.Contains(entity.Id);

            if (pressed)
                _heldButtons.Add(entity.Id);
            else
                _heldButtons.Remove(entity.Id);

            if (!pressed || wasPressed)
                return;

            bool open = !dropdown.Open;

            foreach (Entity other in _entities.Query<Dropdown>().ToEntityList())
            {
                if (other.Id == entity.Id)
                    continue;

                ref Dropdown otherDropdown =
                    ref other.GetComponent<Dropdown>();
                otherDropdown.Open = false;
                Hide(otherDropdown.List, buffer);
            }

            dropdown.Open = open;
            SetListPosition(entity, ref dropdown, buffer);
        });

        foreach (Entity entity in _entities
                     .Query<DropdownOption>()
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

            DropdownOption option = entity.GetComponent<DropdownOption>();
            if (option.Owner.IsNull ||
                !option.Owner.HasComponent<Dropdown>() ||
                !option.Owner.HasComponent<EditorDropdownControl>())
            {
                continue;
            }

            ref Dropdown dropdown =
                ref option.Owner.GetComponent<Dropdown>();
            EditorDropdownKind kind =
                option.Owner.GetComponent<EditorDropdownControl>().Kind;

            dropdown.Selected = option.Value;
            dropdown.Open = false;
            Hide(dropdown.List, buffer);

            if (kind == EditorDropdownKind.Snap)
            {
                _commands.Enqueue(EditorCommand.SetSnap(SnapValue(option.Value)));
            }
            else if (kind == EditorDropdownKind.Character)
            {
                Entity editor = option.Owner.Store
                    .Query<EditorCharacterTarget>()
                    .ToEntityList()[0];
                if (!editor.IsNull)
                {
                    editor.GetComponent<EditorCharacterTarget>().Target =
                        (CharacterKind)Math.Clamp(
                            option.Value,
                            0,
                            3);
                }
            }
            else
            {
                _commands.Enqueue(EditorCommand.SetTarget(
                    KeybindSettings.Actions[Math.Clamp(
                        option.Value,
                        0,
                        KeybindSettings.Actions.Length - 1)]));
            }

            SetDisplay(option.Owner, ref dropdown, kind, buffer);
        }

        foreach (Entity entity in _entities
                     .Query<DropdownOption>()
                     .ToEntityList())
        {
            DropdownOption option = entity.GetComponent<DropdownOption>();
            if (option.Owner.IsNull ||
                !option.Owner.HasComponent<Dropdown>())
            {
                continue;
            }

            Dropdown dropdown = option.Owner.GetComponent<Dropdown>();

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
        ref Dropdown dropdown,
        CommandBuffer buffer)
    {
        if (dropdown.List.IsNull)
            return;

        ref Position position = ref dropdown.List.GetComponent<Position>();
        ref Rectangle rectangle = ref dropdown.List.GetComponent<Rectangle>();
        Vector2 targetSize = dropdown.Open
            ? (dropdown.ListSize == Vector2.Zero
                ? rectangle.Bounds.Size
                : dropdown.ListSize)
            : Vector2.Zero;
        if (rectangle.Bounds.Size != targetSize)
        {
            rectangle.Bounds = new Rect2(rectangle.Bounds.Position, targetSize);
            buffer.AddTag<Dirty>(dropdown.List.Id);
        }

        if (!dropdown.Open)
        {
            position.Value = HiddenList;
        }
        else
        {
            ref Position buttonPosition = ref button.GetComponent<Position>();
            float buttonHeight = button.GetComponent<Rectangle>().Bounds.Size.Y;
            float listHeight = targetSize.Y;

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
        if (list.HasComponent<Rectangle>())
        {
            ref Rectangle rectangle = ref list.GetComponent<Rectangle>();
            if (rectangle.Bounds.Size != Vector2.Zero)
            {
                rectangle.Bounds = new Rect2(
                    rectangle.Bounds.Position,
                    Vector2.Zero);
                buffer.AddTag<Dirty>(list.Id);
            }
        }
        buffer.AddTag<Moved>(list.Id);
    }

    private static void SetDisplay(
        Entity button,
        ref Dropdown dropdown,
        EditorDropdownKind kind,
        CommandBuffer buffer)
    {
        if (button.IsNull || !button.HasComponent<Text>())
            return;

        string label = kind switch
        {
            EditorDropdownKind.Snap => $"SNAP  {SnapLabel(dropdown.Selected)}",
            EditorDropdownKind.Character => $"CHAR  {CharacterLabel(dropdown.Selected)}",
            _ => $"TYPE  {KeybindSettings.LabelFor(
                KeybindSettings.Actions[Math.Clamp(
                    dropdown.Selected,
                    0,
                    KeybindSettings.Actions.Length - 1)])}",
        };

        ref Text text = ref button.GetComponent<Text>();
        bool changed = false;
        if (text.Value != label)
        {
            text.Value = label;
            changed = true;
        }

        if (kind == EditorDropdownKind.NoteType &&
            button.HasComponent<ButtonVisual>())
        {
            Color color = PlayfieldRenderer.ColorFor(
                KeybindSettings.Actions[Math.Clamp(
                    dropdown.Selected,
                    0,
                    KeybindSettings.Actions.Length - 1)]);
            ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
            if (visual.Normal != color)
            {
                visual.Normal = color;
                visual.Hover = color.Lerp(Colors.White, 0.16f);
                visual.Flash = Colors.White;
                changed = true;
            }
        }

        if (changed)
            buffer.AddTag<Dirty>(button.Id);
    }

    private static string CharacterLabel(int index) => index switch
    {
        1 => "ARMED",
        2 => "SPLIT LINE",
        3 => "MAGE",
        _ => "CONTACT",
    };

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
