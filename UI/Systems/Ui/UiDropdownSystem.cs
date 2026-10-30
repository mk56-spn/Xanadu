using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>Generic ECS dropdown open/close, list layout, and option selection.</summary>
public sealed class UiDropdownSystem : QuerySystem<Dropdown>
{

    private readonly EntityStore _entities;
    private readonly HashSet<int> _heldButtons = new();
    private readonly HashSet<int> _heldOptions = new();

    public UiDropdownSystem(EntityStore entities)
    {
        _entities = entities;
        Filter.WithoutAnyTags(Tags.Get<UiDisabled>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        // A disabled dropdown must not leave its list floating over the scene.
        foreach (Entity disabled in _entities
                     .Query<Dropdown>()
                     .AllTags(Tags.Get<UiDisabled>())
                     .ToEntityList())
        {
            ref Dropdown closed = ref disabled.GetComponent<Dropdown>();
            if (!closed.Open)
                continue;

            closed.Open = false;
            LayOut(disabled, ref closed, buffer);
        }

        foreach (Entity entity in Query.ToEntityList())
        {
            ref Dropdown dropdown = ref entity.GetComponent<Dropdown>();
            bool pressed = entity.Tags.Has<Pressed>();
            bool held = _heldButtons.Contains(entity.Id);

            if (pressed)
                _heldButtons.Add(entity.Id);
            else
                _heldButtons.Remove(entity.Id);

            if (pressed && !held)
            {
                bool open = !dropdown.Open;
                if (open && dropdown.CloseOthers)
                {
                    foreach (Entity other in Query.ToEntityList())
                    {
                        if (other.Id == entity.Id)
                            continue;
                        ref Dropdown otherDropdown = ref other.GetComponent<Dropdown>();
                        otherDropdown.Open = false;
                        LayOut(other, ref otherDropdown, buffer);
                    }
                }

                dropdown.Open = open;
                LayOut(entity, ref dropdown, buffer);
            }
        }

        foreach (Entity entity in Query.ToEntityList())
        {
            ref Dropdown dropdown = ref entity.GetComponent<Dropdown>();
            if (dropdown.Open)
                LayOut(entity, ref dropdown, buffer);
        }

        // Options hide themselves when pressed, and still have to let go of the press.
        foreach (Entity entity in _entities
                     .Query<DropdownOption>()
                     .WithDisabled()
                     .ToEntityList())
        {
            bool pressed = entity.Tags.Has<Pressed>();
            bool held = _heldOptions.Contains(entity.Id);
            if (pressed)
                _heldOptions.Add(entity.Id);
            else
                _heldOptions.Remove(entity.Id);
            if (!pressed || held)
                continue;

            DropdownOption option = entity.GetComponent<DropdownOption>();
            if (option.Owner.IsNull ||
                !option.Owner.HasComponent<Dropdown>() ||
                option.Owner.Tags.Has<UiDisabled>())
            {
                continue;
            }

            ref Dropdown dropdown = ref option.Owner.GetComponent<Dropdown>();
            dropdown.Selected = option.Value;
            dropdown.Open = false;
            buffer.AddComponent(option.Owner.Id, new DropdownChanged(option.Value));
            LayOut(option.Owner, ref dropdown, buffer);
        }
    }

    private void LayOut(Entity button, ref Dropdown dropdown, CommandBuffer buffer)
    {
        if (dropdown.List.IsNull ||
            !dropdown.List.HasComponent<Rectangle>() ||
            !button.HasComponent<Position>() ||
            !button.HasComponent<Rectangle>())
        {
            return;
        }

        ref Position listPosition = ref dropdown.List.GetComponent<Position>();
        ref Rectangle listRectangle = ref dropdown.List.GetComponent<Rectangle>();
        float buttonHeight = button.GetComponent<Rectangle>().Bounds.Size.Y;
        Vector2 buttonWorld = UiTransform.WorldPosition(
            button,
            button.GetComponent<Position>().Value);
        Vector2 listParentWorld = UiTransform.WorldPosition(
            dropdown.List,
            Vector2.Zero);
        Vector2 size = dropdown.ListSize == Vector2.Zero
            ? listRectangle.Bounds.Size
            : dropdown.ListSize;

        if (!dropdown.Open)
        {
            UiLayout.SetHidden(dropdown.List, buffer, true);
            SetOptionsHidden(button, buffer, true);
            return;
        }

        if (listRectangle.Bounds.Size != size)
        {
            listRectangle.Bounds = new Rect2(-size / 2, size);
            buffer.AddTag<Dirty>(dropdown.List.Id);
        }

        Vector2 listWorld = buttonWorld +
                            new Vector2(0, buttonHeight / 2 + size.Y / 2 + 4);
        listPosition.Value = listWorld - listParentWorld;
        UiLayout.SetHidden(dropdown.List, buffer, false);
        SetOptionsHidden(button, buffer, false);
        buffer.AddTag<Moved>(dropdown.List.Id);
        buffer.AddTag<Dirty>(dropdown.List.Id);

        float top = listWorld.Y - size.Y / 2 + 2;
        var options = new List<Entity>();
        foreach (EntityLink<DropdownOption> link in button.GetIncomingLinks<DropdownOption>())
            options.Add(link.Entity);

        options.Sort(static (left, right) =>
            left.GetComponent<DropdownOption>().Value.CompareTo(
                right.GetComponent<DropdownOption>().Value));

        foreach (Entity option in options)
        {
            float height = option.GetComponent<Rectangle>().Bounds.Size.Y;
            ref Position optionPosition = ref option.GetComponent<Position>();
            Vector2 optionParentWorld = UiTransform.WorldPosition(
                option,
                Vector2.Zero);
            optionPosition.Value = new Vector2(
                listWorld.X,
                top + height / 2) - optionParentWorld;
            top += height + 2;
            buffer.AddTag<Moved>(option.Id);
            buffer.AddTag<Dirty>(option.Id);
        }
    }

    private static void SetOptionsHidden(Entity button, CommandBuffer buffer, bool hidden)
    {
        foreach (EntityLink<DropdownOption> link in button.GetIncomingLinks<DropdownOption>())
            UiLayout.SetHidden(link.Entity, buffer, hidden);
    }
}
