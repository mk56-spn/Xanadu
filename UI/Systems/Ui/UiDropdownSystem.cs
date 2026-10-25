using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Ui;

/// <summary>Generic ECS dropdown open/close, list layout, and option selection.</summary>
public sealed class UiDropdownSystem : QuerySystem<Dropdown>
{
    private static readonly Vector2 Hidden = new(-10_000, -10_000);

    private readonly EntityStore _entities;
    private readonly HashSet<int> _heldButtons = new();
    private readonly HashSet<int> _heldOptions = new();

    public UiDropdownSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

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
                bool wasOpen = dropdown.Open;
                bool open = !dropdown.Open;
                if (open)
                {
                    foreach (Entity other in Query.ToEntityList())
                    {
                        if (other.Id == entity.Id)
                            continue;
                        ref Dropdown otherDropdown = ref other.GetComponent<Dropdown>();
                        otherDropdown.Open = false;
                        LayOut(other, ref otherDropdown, buffer);
                        MarkContainerDirty(other, buffer);
                    }
                }

                dropdown.Open = open;
                LayOut(entity, ref dropdown, buffer);
                if (wasOpen != open)
                    MarkContainerDirty(entity, buffer);
            }
        }

        foreach (Entity entity in Query.ToEntityList())
        {
            ref Dropdown dropdown = ref entity.GetComponent<Dropdown>();
            if (dropdown.Open)
                LayOut(entity, ref dropdown, buffer);
        }

        foreach (Entity entity in _entities
                     .Query<DropdownOption>()
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
                !option.Owner.HasComponent<Dropdown>())
            {
                continue;
            }

            ref Dropdown dropdown = ref option.Owner.GetComponent<Dropdown>();
            dropdown.Selected = option.Value;
            dropdown.Open = false;
            buffer.AddComponent(option.Owner.Id, new DropdownChanged(option.Value));
            LayOut(option.Owner, ref dropdown, buffer);
            MarkContainerDirty(option.Owner, buffer);
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
            listPosition.Value = Hidden;
            if (listRectangle.Bounds.Size != Vector2.Zero)
                listRectangle.Bounds = new Rect2(Vector2.Zero, Vector2.Zero);
            buffer.AddTag<Moved>(dropdown.List.Id);
            HideOptions(button, buffer);
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
        buffer.AddTag<Moved>(dropdown.List.Id);

        float top = listWorld.Y - size.Y / 2 + 2;
        var options = new List<Entity>();
        foreach (Entity option in _entities
                     .Query<DropdownOption>()
                     .ToEntityList())
        {
            DropdownOption binding = option.GetComponent<DropdownOption>();
            if (binding.Owner.Id == button.Id &&
                option.HasComponent<Position>() &&
                option.HasComponent<Rectangle>())
            {
                options.Add(option);
            }
        }

        options.Sort(static (left, right) =>
            left.GetComponent<DropdownOption>().Value.CompareTo(
                right.GetComponent<DropdownOption>().Value));

        foreach (Entity option in options)
        {
            float height = option.GetComponent<Rectangle>().Bounds.Size.Y;
            ref Position optionPosition = ref option.GetComponent<Position>();
            optionPosition.Value = new Vector2(
                listWorld.X,
                top + height / 2);
            top += height + 2;
            buffer.AddTag<Moved>(option.Id);
        }
    }

    private static void MarkContainerDirty(
        Entity button,
        CommandBuffer buffer)
    {
        Entity parent = button.Parent;
        if (!parent.IsNull && parent.HasComponent<UiContainer>())
            buffer.AddTag<LayoutDirty>(parent.Id);
    }

    private void HideOptions(Entity button, CommandBuffer buffer)
    {
        foreach (Entity option in _entities
                     .Query<DropdownOption>()
                     .ToEntityList())
        {
            DropdownOption binding = option.GetComponent<DropdownOption>();
            if (binding.Owner.Id != button.Id ||
                !option.HasComponent<Position>())
            {
                continue;
            }

            ref Position position = ref option.GetComponent<Position>();
            position.Value = Hidden;
            buffer.AddTag<Moved>(option.Id);
        }
    }
}
