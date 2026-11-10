using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Timeline;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Runs every ease picker: shows it while its host has it open, above the
/// control it opens from, lights the selection's curve, hands pressed curves
/// and the × to the host, and runs the preview: the curve under the pointer,
/// else the selection's, drawn with a dot travelling it and a second dot on a
/// rail showing the value alone.
/// </summary>
public sealed class EasePickerSystem : QuerySystem<EasePicker>
{
    /// <summary>One run of the preview dots, then a rest at the end before the next.</summary>
    public const float RunSeconds = 1.2f;
    public const float RestSeconds = 0.5f;

    /// <summary>Gap kept between the picker and the control it opens from.</summary>
    public const float AnchorGap = 6f;

    private const int PreviewSteps = 64;

    private readonly ButtonPress _presses = new();

    // A hidden picker still has to notice its host opening it.
    public EasePickerSystem() => Filter.WithDisabled();

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        foreach (Entity panel in Query.ToEntityList())
            Update(panel, delta);
    }

    private void Update(Entity panel, float delta)
    {
        ref EasePicker picker = ref panel.GetComponent<EasePicker>();
        IEasePickerHost host = picker.Host;
        List<Entity> cells = Cells(panel);

        // Presses first, hidden or not, so one landing as the picker hides is still let go.
        bool closed = _presses.Started(picker.Parts.Close);
        EasePreset? pressed = null;
        foreach (Entity cell in cells)
        {
            if (_presses.Started(cell))
                pressed = cell.GetComponent<EasePickerCell>().Preset;
        }

        if (host.Open && pressed is { } preset)
            host.Pick(preset);
        if (host.Open && closed)
            host.Close();

        if (!host.Open)
        {
            Hide(panel, picker.Parts);
            picker.Shown = null;
            return;
        }

        Vector2 size = EasePickerPanel.Size;
        Place(panel, Corner(host.Anchor, host.Bounds, size) + size / 2);
        UiLayout.SetHidden(panel, false);

        EasePreset? current = host.Current;
        Light(cells, current);

        EasePreset? hovered = Hovered(cells);
        EasePreset shown = hovered ?? current ?? EasePreset.Ease;
        UiText.Set(picker.Parts.Name, EaseNames.Label(shown));
        UiText.Set(picker.Parts.Description, EaseNames.Describe(shown));
        UiText.Set(picker.Parts.Hint, current is null
            ? host.EmptyHint
            : hovered is null
                ? "The selection's curve"
                : "Click to set it on the selection");

        picker.Clock = (picker.Clock + delta) % (RunSeconds + RestSeconds);
        Draw(ref picker, shown);
    }

    /// <summary>
    /// The picker's top-left: right-aligned with the anchor and above it, or
    /// below where there is no room above, and always inside the bounds.
    /// </summary>
    public static Vector2 Corner(Rect2 anchor, Rect2 bounds, Vector2 size)
    {
        float y = anchor.Position.Y - AnchorGap - size.Y;
        if (y < bounds.Position.Y)
            y = anchor.End.Y + AnchorGap;
        var corner = new Vector2(anchor.End.X - size.X, y);
        return new Vector2(
            Mathf.Clamp(corner.X, bounds.Position.X, Mathf.Max(bounds.Position.X, bounds.End.X - size.X)),
            Mathf.Clamp(corner.Y, bounds.Position.Y, Mathf.Max(bounds.Position.Y, bounds.End.Y - size.Y)));
    }

    private List<Entity> Cells(Entity panel)
    {
        var cells = new List<Entity>();
        foreach (Entity cell in Query.Store.Query<EasePickerCell, ButtonVisual>().WithDisabled().ToEntityList())
        {
            if (cell.GetComponent<EasePickerCell>().Picker.Id == panel.Id)
                cells.Add(cell);
        }

        return cells;
    }

    /// <summary>Lights the selection's curve; hidden cells too, so the picker opens already lit.</summary>
    private static void Light(List<Entity> cells, EasePreset? current)
    {
        foreach (Entity cell in cells)
        {
            bool on = current == cell.GetComponent<EasePickerCell>().Preset;
            Color target = on ? EasePickerPanel.Lit : EasePickerPanel.Cell;
            ref ButtonVisual visual = ref cell.GetComponent<ButtonVisual>();
            if (visual.Normal == target)
                continue;

            visual.Normal = target;
            visual.Hover = target.Lerp(Colors.White, 0.16f);
        }
    }

    /// <summary>The curve whose thumbnail the pointer is over, if any.</summary>
    private static EasePreset? Hovered(List<Entity> cells)
    {
        EasePreset? hovered = null;
        float best = 0.5f;
        foreach (Entity cell in cells)
        {
            float amount = cell.GetComponent<ButtonVisual>().HoverAmount;
            if (amount > best)
            {
                best = amount;
                hovered = cell.GetComponent<EasePickerCell>().Preset;
            }
        }

        return hovered;
    }

    private static void Draw(ref EasePicker picker, EasePreset preset)
    {
        EasePickerParts parts = picker.Parts;
        Rect2 box = XifEditorSystem.Area(parts.PreviewBox);
        if (box.Size == Vector2.Zero)
            return;

        float inset = EasePickerPanel.PreviewInset;
        float railRoom = EasePickerPanel.RailInset * 2;
        Vector2 plot = new(box.Size.X - inset * 2 - railRoom, box.Size.Y - inset * 2);
        Vector2 centre = new(box.Position.X + inset + plot.X / 2, box.Position.Y + box.Size.Y / 2);
        float railX = box.End.X - EasePickerPanel.RailInset - inset / 2;

        Vector2[] points = EasePickerPanel.CurvePoints(preset, plot, PreviewSteps, out float low, out float high);
        if (picker.Shown != preset)
        {
            picker.Shown = preset;
            SetPoints(parts.Curve, points);

            // Where the value starts and ends, so overshoot shows as leaving them.
            float left = -plot.X / 2;
            float right = railX - centre.X;
            Vector2 zero = EasePickerPanel.Point(0f, 0f, plot, low, high);
            Vector2 one = EasePickerPanel.Point(0f, 1f, plot, low, high);
            SetPoints(parts.Guides, new[]
            {
                new Vector2(left, zero.Y), new Vector2(right, zero.Y),
                new Vector2(right, one.Y), new Vector2(left, one.Y),
            });
            SetPoints(parts.Rail, new[]
            {
                new Vector2(0f, EasePickerPanel.Point(0f, low, plot, low, high).Y),
                new Vector2(0f, EasePickerPanel.Point(0f, high, plot, low, high).Y),
            });
        }

        float t = Mathf.Min(1f, picker.Clock / RunSeconds);
        float value = EasePresets.Progress(preset, t);
        Vector2 dot = EasePickerPanel.Point(t, value, plot, low, high);

        Show(parts.Curve, centre);
        Show(parts.Guides, centre);
        Show(parts.Rail, new Vector2(railX, centre.Y));
        Show(parts.Dot, centre + dot);
        Show(parts.RailDot, new Vector2(railX, centre.Y + dot.Y));
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

    private static void Show(Entity entity, Vector2 position)
    {
        Place(entity, position);
        UiLayout.SetHidden(entity, false);
    }

    private static void Place(Entity entity, Vector2 position)
    {
        if (entity.IsNull || !entity.HasComponent<Position>())
            return;

        if (UiLayout.Move(entity, position))
            entity.AddTag<Dirty>();
    }

    private static void Hide(Entity panel, EasePickerParts parts)
    {
        foreach (Entity entity in new[] { panel, parts.Guides, parts.Curve, parts.Rail, parts.Dot, parts.RailDot })
        {
            if (!entity.IsNull)
                UiLayout.SetHidden(entity, true);
        }
    }
}
