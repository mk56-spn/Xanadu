using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Switches tabs when a tab button is pressed, shows the selected body in its
/// slot, parks the rest off screen and lights the selected button.
/// </summary>
public sealed class UiTabSystem : QuerySystem<UiTabs>
{
    protected override void OnUpdate()
    {
        foreach (Entity strip in Query.ToEntityList())
        {
            ref UiTabs tabs = ref strip.GetComponent<UiTabs>();
            for (int i = 0; i < tabs.Buttons.Length; i++)
            {
                bool pressed = !tabs.Buttons[i].IsNull && tabs.Buttons[i].Tags.Has<Pressed>();
                if (pressed && !tabs.Held[i])
                    tabs.Selected = i;
                tabs.Held[i] = pressed;
            }

            for (int i = 0; i < tabs.Bodies.Length; i++)
            {
                Place(tabs.Bodies[i], i == tabs.Selected ? tabs.BodyCenter : UiLayout.Hidden);
                Tint(tabs.Buttons[i], i == tabs.Selected ? tabs.OnColor : tabs.OffColor);
            }
        }
    }

    private static void Place(Entity body, Vector2 center)
    {
        if (body.IsNull || !body.HasComponent<Position>())
            return;

        ref Position position = ref body.GetComponent<Position>();
        if (position.Value == center)
            return;

        position.Value = center;
        body.AddTag<Moved>();
        body.AddTag<Dirty>();
    }

    private static void Tint(Entity button, Color normal)
    {
        if (button.IsNull || !button.HasComponent<ButtonVisual>())
            return;

        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == normal)
            return;

        visual.Normal = normal;
        visual.Hover = normal.Lerp(Colors.White, 0.16f);
        visual.Flash = Colors.White;
        button.AddTag<Dirty>();
    }
}
