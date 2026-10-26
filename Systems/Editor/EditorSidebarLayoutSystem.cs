using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Editor;

/// <summary>Flows the editor's right-side fields around open dropdowns.</summary>
public sealed class EditorSidebarLayoutSystem : QuerySystem<EditorSidebarLayout>
{
    private const float LabelHeight = 24f;
    private const float ButtonHeight = 34f;
    private const float PanelPadding = 20f;
    public EditorSidebarLayoutSystem(EntityStore entities)
    {
        _ = entities;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref layout, entity) =>
        {
            float characterExtra = OpenExtra(layout.CharacterButton);

            float characterLabelY = layout.Top + LabelHeight / 2;
            float characterButtonY = characterLabelY + LabelHeight / 2 +
                                     ButtonHeight / 2;

            Move(layout.CharacterLabel, characterLabelY, buffer);
            Move(layout.CharacterButton, characterButtonY, buffer);

            float top = layout.Top - 10;
            float bottom = characterButtonY + ButtonHeight / 2 +
                           characterExtra + PanelPadding;
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
                if (!layout.Panel.IsNull &&
                    layout.Panel.HasComponent<Position>() &&
                    layout.Panel.HasComponent<Rectangle>())
                {
                    var rect = new Rect2(
                        layout.Panel.GetComponent<Position>().Value.X -
                        layout.Panel.GetComponent<Rectangle>().Bounds.Size.X / 2,
                        top,
                        layout.Panel.GetComponent<Rectangle>().Bounds.Size.X,
                        bottom - top);
                    sidebar.Bounds = rect;
                }
            }
        });
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
