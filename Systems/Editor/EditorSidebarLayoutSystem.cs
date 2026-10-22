using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Editor;

/// <summary>Flows the editor sidebar around open dropdown lists.</summary>
public sealed class EditorSidebarLayoutSystem : QuerySystem<EditorSidebarLayout>
{
    private const float LabelHeight = 24f;
    private const float ButtonHeight = 34f;
    private const float Gap = 16f;
    private const float PanelPadding = 20f;
    private readonly EntityStore _entities;

    public EditorSidebarLayoutSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref layout, entity) =>
        {
            float snapExtra = OpenExtra(layout.SnapButton);
            float typeExtra = OpenExtra(layout.TypeButton);

            float snapLabelY = layout.Top + LabelHeight / 2;
            float snapButtonY = layout.Top + LabelHeight + ButtonHeight / 2;
            float typeLabelY = snapLabelY + LabelHeight + ButtonHeight +
                               Gap + snapExtra + LabelHeight / 2;
            float typeButtonY = typeLabelY + LabelHeight / 2 +
                                ButtonHeight / 2;
            float toolsY = typeButtonY + ButtonHeight / 2 +
                           Gap + typeExtra + 70;
            float saveY = toolsY + 93;
            float loadY = saveY + ButtonHeight + 6;

            Move(layout.SnapLabel, snapLabelY, buffer);
            Move(layout.SnapButton, snapButtonY, buffer);
            Move(layout.TypeLabel, typeLabelY, buffer);
            Move(layout.TypeButton, typeButtonY, buffer);
            Move(layout.Tools, toolsY, buffer);
            Move(layout.SaveAs, saveY, buffer);
            Move(layout.Load, loadY, buffer);

            AlignOpenList(layout.SnapButton, buffer);
            AlignOpenList(layout.TypeButton, buffer);

            float top = layout.Top - 10;
            float bottom = loadY + ButtonHeight / 2 + PanelPadding;
            if (!layout.Panel.IsNull &&
                layout.Panel.HasComponent<Position>() &&
                layout.Panel.HasComponent<Rectangle>())
            {
                ref Position panelPosition =
                    ref layout.Panel.GetComponent<Position>();
                ref Rectangle panelRect =
                    ref layout.Panel.GetComponent<Rectangle>();
                Vector2 size = new(
                    panelRect.Bounds.Size.X,
                    bottom - top);
                Vector2 position = new(
                    panelPosition.Value.X,
                    (top + bottom) / 2);
                if (panelPosition.Value != position ||
                    panelRect.Bounds.Size != size)
                {
                    panelPosition.Value = position;
                    panelRect.Bounds = new Rect2(-size / 2, size);
                    buffer.AddTag<Moved>(layout.Panel.Id);
                    buffer.AddTag<Dirty>(layout.Panel.Id);
                }
            }

            if (entity.HasComponent<EditorSidebar>())
            {
                ref EditorSidebar sidebar = ref entity.GetComponent<EditorSidebar>();
                var rect = new Rect2(
                    layout.Panel.GetComponent<Position>().Value.X -
                    layout.Panel.GetComponent<Rectangle>().Bounds.Size.X / 2,
                    top,
                    layout.Panel.GetComponent<Rectangle>().Bounds.Size.X,
                    bottom - top);
                sidebar.Bounds = rect;
            }
        });
    }

    private void AlignOpenList(Entity button, CommandBuffer buffer)
    {
        if (button.IsNull ||
            !button.HasComponent<Dropdown>() ||
            !button.HasComponent<Position>() ||
            !button.HasComponent<Rectangle>())
        {
            return;
        }

        Dropdown dropdown = button.GetComponent<Dropdown>();
        if (!dropdown.Open ||
            dropdown.List.IsNull ||
            !dropdown.List.HasComponent<Position>())
        {
            return;
        }

        Position buttonPosition = button.GetComponent<Position>();
        float buttonHeight = button.GetComponent<Rectangle>().Bounds.Size.Y;
        Vector2 listSize = dropdown.ListSize;
        Vector2 target = buttonPosition.Value + new Vector2(
            0,
            buttonHeight / 2 + listSize.Y / 2 + 4);

        ref Position listPosition = ref dropdown.List.GetComponent<Position>();
        if (listPosition.Value != target)
        {
            listPosition.Value = target;
            buffer.AddTag<Moved>(dropdown.List.Id);
        }

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

            float listTop = target.Y - listSize.Y / 2;
            ref Position optionPosition = ref option.GetComponent<Position>();
            Vector2 optionTarget = new(
                target.X,
                listTop + 18 + binding.Value * 30);
            if (optionPosition.Value != optionTarget)
            {
                optionPosition.Value = optionTarget;
                buffer.AddTag<Moved>(option.Id);
            }
        }
    }

    private static float OpenExtra(Entity button)
    {
        if (button.IsNull || !button.HasComponent<Dropdown>())
            return 0;

        Dropdown dropdown = button.GetComponent<Dropdown>();
        return dropdown.Open
            ? dropdown.ListSize.Y + 8
            : 0;
    }

    private static void Move(Entity entity, float y, CommandBuffer buffer)
    {
        if (entity.IsNull || !entity.HasComponent<Position>())
            return;

        ref Position position = ref entity.GetComponent<Position>();
        if (position.Value.Y == y)
            return;

        position.Value = new Vector2(position.Value.X, y);
        buffer.AddTag<Moved>(entity.Id);
    }
}
