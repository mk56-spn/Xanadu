using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Switches tabs when a tab button is pressed, shows the selected body in its
/// slot, hides the rest and lights the selected button.
/// </summary>
public sealed class UiTabSystem : QuerySystem<UiTabs>
{
    /// <summary>Bodies float outside the strip, so a hidden strip has to hide them itself.</summary>
    public UiTabSystem() => Filter.WithDisabled();

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
                Place(tabs.Bodies[i], tabs.BodyCenter);
                UiLayout.SetHidden(tabs.Bodies[i], !strip.Enabled || i != tabs.Selected);
                Tint(tabs.Buttons[i], i == tabs.Selected ? tabs.OnColor : tabs.OffColor);
                tabs = ref strip.GetComponent<UiTabs>();
            }
        }
    }

    private static void Place(Entity body, Vector2 center)
    {
        // A body in a container's stack is placed by the stack.
        if (body.IsNull || !body.HasComponent<Position>() || UiLayout.InFlow(body))
            return;

        if (UiLayout.Move(body, center))
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
