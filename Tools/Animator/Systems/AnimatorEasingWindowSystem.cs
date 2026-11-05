using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Keeps the EASING window above its header button while open, names the
/// selected keys' curve on that button, and runs the preview: the curve under
/// the pointer, else the selected keys' one, drawn with a dot travelling it
/// and a second dot on a rail showing the value alone.
/// </summary>
public sealed class AnimatorEasingWindowSystem : QuerySystem<AnimatorEasingWindow, AnimatorUi>
{
    /// <summary>One run of the preview dots, then a rest at the end before the next.</summary>
    public const float RunSeconds = 1.2f;
    public const float RestSeconds = 0.5f;

    private const int PreviewSteps = 64;

    /// <summary>Gap kept between the window and the button it opens from.</summary>
    private const float ButtonGap = 6f;

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        foreach (Entity session in Query.ToEntityList())
            Update(session, delta);
    }

    private static void Update(Entity session, float delta)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        ref AnimatorEasingWindow window = ref session.GetComponent<AnimatorEasingWindow>();
        XafEasePreset? current = AnimatorInterpolationSystem.CurrentPreset(session);
        UiText.Set(ui.EasingButton, current is { } set ? $"EASE: {AnimatorEasingNames.Label(set)}" : "EASING");

        // Preview mode hides the timeline the window belongs to.
        if (Commands.AnimatorPreviewModeCommands.IsOn(session))
            window.Open = false;

        if (!window.Open || ui.EasingWindow.IsNull)
        {
            Hide(ui);
            window.Shown = null;
            return;
        }

        Vector2 size = AnimatorLayout.EasingWindowSize;
        Rect2 button = AnimatorGeometry.PanelArea(ui.EasingButton);
        Vector2 corner = new(button.End.X - size.X, button.Position.Y - ButtonGap - size.Y);
        corner = new Vector2(
            Mathf.Clamp(corner.X, 0f, Mathf.Max(0f, ui.ScreenSize.X - size.X)),
            Mathf.Clamp(corner.Y, 0f, Mathf.Max(0f, ui.ScreenSize.Y - size.Y)));
        AnimatorMenuPlacement.Place(ui.EasingWindow, corner, size);

        XafEasePreset? hovered = Hovered(session.Store);
        XafEasePreset shown = hovered ?? current ?? XafEasePreset.Ease;
        UiText.Set(ui.EasingName, AnimatorEasingNames.Label(shown));
        UiText.Set(ui.EasingDescription, AnimatorEasingNames.Describe(shown));
        UiText.Set(ui.EasingHint, current is null
            ? "Select keys to set their curve"
            : hovered is null
                ? "The selected keys' curve"
                : "Click to set it on the selection");

        window.Clock = (window.Clock + delta) % (RunSeconds + RestSeconds);
        Draw(ui, ref window, shown);
    }

    /// <summary>The curve whose thumbnail the pointer is over, if any.</summary>
    private static XafEasePreset? Hovered(EntityStore store)
    {
        XafEasePreset? hovered = null;
        float best = 0.5f;
        store.Query<ButtonVisual, ToolAction>().AllTags(Tags.Get<AnimatorEasingCell>()).ForEachEntity(
            (ref ButtonVisual visual, ref ToolAction action, Entity _) =>
            {
                if (visual.HoverAmount > best &&
                    System.Enum.TryParse(action.Argument, out XafEasePreset preset))
                {
                    best = visual.HoverAmount;
                    hovered = preset;
                }
            });
        return hovered;
    }

    private static void Draw(AnimatorUi ui, ref AnimatorEasingWindow window, XafEasePreset preset)
    {
        Rect2 box = AnimatorGeometry.PanelArea(ui.EasingPreviewBox);
        if (box.Size == Vector2.Zero)
            return;

        float inset = AnimatorLayout.EasingPreviewInset;
        float railRoom = AnimatorLayout.EasingRailInset * 2;
        Vector2 plot = new(box.Size.X - inset * 2 - railRoom, box.Size.Y - inset * 2);
        Vector2 centre = new(box.Position.X + inset + plot.X / 2, box.Position.Y + box.Size.Y / 2);
        float railX = box.End.X - AnimatorLayout.EasingRailInset - inset / 2;

        Vector2[] points = AnimatorLayout.EasingCurvePoints(preset, plot, PreviewSteps, out float low, out float high);
        if (window.Shown != preset)
        {
            window.Shown = preset;
            SetPoints(ui.EasingCurve, points);

            // Where the value starts and ends, so overshoot shows as leaving them.
            float left = -plot.X / 2;
            float right = railX - centre.X;
            Vector2 zero = AnimatorLayout.EasingPoint(0f, 0f, plot, low, high);
            Vector2 one = AnimatorLayout.EasingPoint(0f, 1f, plot, low, high);
            SetPoints(ui.EasingGuides, new[]
            {
                new Vector2(left, zero.Y), new Vector2(right, zero.Y),
                new Vector2(right, one.Y), new Vector2(left, one.Y),
            });
            SetPoints(ui.EasingRail, new[]
            {
                new Vector2(0f, AnimatorLayout.EasingPoint(0f, low, plot, low, high).Y),
                new Vector2(0f, AnimatorLayout.EasingPoint(0f, high, plot, low, high).Y),
            });
        }

        float t = Mathf.Min(1f, window.Clock / RunSeconds);
        float value = XafEasing.Progress(preset, t);
        Vector2 dot = AnimatorLayout.EasingPoint(t, value, plot, low, high);

        AnimatorMenuPlacement.Place(ui.EasingCurve, centre);
        AnimatorMenuPlacement.Place(ui.EasingGuides, centre);
        AnimatorMenuPlacement.Place(ui.EasingRail, new Vector2(railX, centre.Y));
        AnimatorMenuPlacement.Place(ui.EasingDot, centre + dot);
        AnimatorMenuPlacement.Place(ui.EasingRailDot, new Vector2(railX, centre.Y + dot.Y));
    }

    private static void SetPoints(Entity shape, Vector2[] points)
    {
        if (shape.IsNull || !shape.HasComponent<UiShape>())
            return;

        shape.GetComponent<UiShape>().Points = points;
        Vector2 min = points[0];
        Vector2 max = points[0];
        foreach (Vector2 point in points)
        {
            min = min.Min(point);
            max = max.Max(point);
        }

        shape.GetComponent<Rectangle>().Bounds = new Rect2(min, max - min);
        shape.AddTag<Dirty>();
    }

    private static void Hide(AnimatorUi ui)
    {
        foreach (Entity entity in new[]
                 {
                     ui.EasingWindow, ui.EasingGuides, ui.EasingCurve, ui.EasingRail, ui.EasingDot, ui.EasingRailDot,
                 })
        {
            if (!entity.IsNull)
                AnimatorMenuPlacement.Hide(entity);
        }
    }
}
