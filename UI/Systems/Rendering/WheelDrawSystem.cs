using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Rendering;

/// <summary>
/// Paints dirty <see cref="UiWheel"/> entities as a ring of wedge buttons with
/// an inner hub and one label per wedge. Unselected wedges are blue; the
/// selected wedge is yellow and reaches the outer rim. Hovering only lightens
/// a wedge.
/// </summary>
public sealed class WheelDrawSystem : QuerySystem<UiWheel, Rectangle, Drawable, VisualRid>
{
    private const int SegmentsPerWedge = 8;
    private const float SelectionInset = 2.5f;

    private static readonly Color Unselected = new("#2563eb");
    private static readonly Color Selected = new("#facc15");
    private static readonly Color Rim = new("#1e40af");
    private static readonly Color Hub = new("#0b1020");
    private static readonly Color UnselectedLabel = new("#f8fafc");
    private static readonly Color SelectedLabel = new("#1e293b");
    private static readonly Color HubLabel = new("#fde047");

    private readonly IRectCanvas _canvas;

    public WheelDrawSystem(IRectCanvas canvas)
    {
        _canvas = canvas;
        Filter.AllTags(Tags.Get<Dirty>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref wheel, ref rect, ref _, ref visual, entity) =>
        {
            _canvas.Clear(visual.Value);
            DrawWheel(wheel, rect, visual.Value);
            buffer.RemoveTag<Dirty>(entity.Id);
        });
    }

    private void DrawWheel(UiWheel wheel, Rectangle rect, Rid canvas)
    {
        float radius = Mathf.Min(rect.Bounds.Size.X, rect.Bounds.Size.Y) / 2;
        if (radius <= 1 || wheel.Labels is not { Length: > 0 })
            return;

        float step = Mathf.Tau / wheel.Labels.Length;
        float hub = radius * Mathf.Clamp(wheel.HubRadius, 0.08f, 0.6f);

        for (int i = 0; i < wheel.Labels.Length; i++)
        {
            bool selected = i == wheel.Selected;
            bool hovered = i == wheel.Hover;
            Color color = selected
                ? Selected.Lerp(Colors.White, hovered ? 0.12f : 0f)
                : WedgeColor(wheel, i).Lerp(
                    Colors.White,
                    hovered ? 0.16f : 0f);

            float outer = selected ? radius : radius - SelectionInset;
            DrawWedge(
                canvas,
                outer,
                step,
                i,
                wheel.StartAngle,
                wheel.Gap,
                color);

            DrawWedgeLabel(canvas, wheel, radius, step, i, selected);
        }

        _canvas.DrawCircleOutline(canvas, Vector2.Zero, radius, Rim, 2f, 64);
        _canvas.DrawCircle(canvas, Vector2.Zero, hub, Hub);
        _canvas.DrawCircleOutline(canvas, Vector2.Zero, hub, Rim, 2f, 48);
        DrawHubLabel(canvas, wheel, hub);
    }

    private void DrawWedge(
        Rid canvas,
        float radius,
        float step,
        int index,
        float startAngle,
        float gap,
        Color color)
    {
        float center = startAngle + index * step;
        float half = Mathf.Max(0.02f, step / 2 - gap / 2);
        int segments = Mathf.Max(3, SegmentsPerWedge);

        var points = new Vector2[segments + 2];
        points[0] = Vector2.Zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = center - half + (half * 2) * i / segments;
            points[i + 1] = new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)) * radius;
        }

        _canvas.DrawPolygon(canvas, points, color);
    }

    private void DrawWedgeLabel(
        Rid canvas,
        UiWheel wheel,
        float radius,
        float step,
        int index,
        bool selected)
    {
        string label = index < wheel.Labels.Length
            ? wheel.Labels[index]
            : "";
        if (label.Length == 0)
            return;

        float center = wheel.StartAngle + index * step;
        const float radial = 0.64f;
        Vector2 position = new Vector2(
            Mathf.Cos(center),
            Mathf.Sin(center)) * (radius * radial);
        float width = Mathf.Clamp(radius * 0.72f, 20, 110);
        var bounds = new Rect2(
            position - new Vector2(width / 2, 12),
            new Vector2(width, 24));

        _canvas.DrawTextStyled(
            canvas,
            bounds,
            label,
            selected ? SelectedLabel : UnselectedLabel,
            label.Length <= 2 ? 16 : 12);
    }

    private void DrawHubLabel(Rid canvas, UiWheel wheel, float hub)
    {
        if (wheel.Selected < 0 ||
            wheel.Selected >= wheel.Labels.Length)
        {
            return;
        }

        string label = wheel.Labels[wheel.Selected];
        if (label.Length == 0)
            return;

        int fontSize = Mathf.Clamp((int)(hub * 0.85f), 10, 22);
        var bounds = new Rect2(
            new Vector2(-hub, -hub * 0.5f),
            new Vector2(hub * 2, hub));
        _canvas.DrawTextStyled(canvas, bounds, label, HubLabel, fontSize);
    }

    private static Color WedgeColor(UiWheel wheel, int index)
    {
        if (wheel.Colors is { Length: > 0 } colors)
            return colors[index % colors.Length];

        return Unselected;
    }
}
