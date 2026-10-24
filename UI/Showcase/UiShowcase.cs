using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Systems.Input;
using Writer.Systems.Layout;
using Writer.Systems.Rendering;
using Writer.Systems.Ui;
using Position = Writer.Ecs.Position;

namespace Writer.Ui.Showcase;

/// <summary>Standalone demo of the generic Writer.Ui feature set.</summary>
public partial class UiShowcase : Control
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private ShowcasePointer _pointer = null!;
    private ScrollSystem _scroll = null!;
    private Entity _dropdownList;

    public override void _Ready()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _pointer = new ShowcasePointer(this);
        var canvas = new GodotRectCanvas();

        _root.Add(new LayerParentSystem(GetCanvasItem()));
        _root.Add(new ContainerLayoutSystem());
        _root.Add(new UiAnchorSystem());
        _root.Add(new DrawableInitialiseSystem());
        _root.Add(new TransformSystem(canvas));
        _root.Add(new RectDrawSystem(canvas));
        _root.Add(new GradientDrawSystem(canvas));
        _root.Add(new TextDrawSystem(canvas));
        _root.Add(new ImageDrawSystem(canvas));
        _root.Add(new InputSystem(_pointer));
        _scroll = new ScrollSystem();
        _root.Add(_scroll);
        _root.Add(new ButtonVisualSystem());
        _root.Add(new UiSliderSystem(_pointer));
        _root.Add(new UiDebugOverlayGroup(
            canvas,
            GetCanvasItem(),
            new GodotOverlayToggleSource(Key.F4)));

        Build();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        float time = (float)Time.GetTicksMsec() / 1000f;
        _root.Update(new UpdateTick((float)delta, time));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton
            {
                Pressed: true,
                ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown,
            } wheel)
        {
            _scroll.Request(
                GetViewport().GetMousePosition(),
                wheel.ButtonIndex == MouseButton.WheelUp ? -1 : 1);
        }
    }

    private void Build()
    {
        Vector2 screen = GetViewportRect().Size;
        UiBuilder ui = UiBuilder.Begin(_store).Layer(0);

        ui.RectEntity(screen / 2, screen, new Color("#0b1020"));
        ui.RectEntity(new Vector2(120, 60), new Vector2(900, 64),
            new Color("#111827"), "WRITER.UI SHOWCASE");
        Style(ui.Current, 32, new Color("#facc15"));

        ui.Container(
            new Vector2(300, 170),
            new Vector2(520, 120),
            StackDirection.Horizontal,
            spacing: 12,
            color: new Color("#111827"),
            padding: 16);
        for (int i = 0; i < 3; i++)
        {
            Entity button = ui.RectEntity(
                Vector2.Zero, new Vector2(150, 60), new Color("#2563eb"),
                $"BUTTON {i + 1}", takesInput: true);
            Style(button, 18, new Color("#e2e8f0"));
            button.AddComponent(ButtonVisual.Create(new Color("#2563eb")));
        }
        ui.End();

        ui.Container(
            new Vector2(300, 330),
            new Vector2(520, 150),
            StackDirection.Vertical,
            spacing: 8,
            color: new Color("#1e293b"),
            padding: 14);
        for (int i = 0; i < 4; i++)
        {
            Entity row = ui.RectEntity(
                Vector2.Zero, new Vector2(460, 26), new Color("#334155"),
                $"NESTED ROW {i + 1}");
            Style(row, 15, new Color("#cbd5e1"));
        }
        ui.End();

        ui.RectEntity(new Vector2(1030, 170), new Vector2(360, 260),
            new Color("#111827"), "SCROLL VIEW");
        Style(ui.Current, 18, new Color("#facc15"));
        ui.Container(
            new Vector2(1030, 300),
            new Vector2(320, 150),
            StackDirection.Vertical,
            spacing: 2,
            clip: true,
            color: new Color("#0f172a"),
            padding: 6);
        for (int i = 0; i < 6; i++)
        {
            Entity row = ui.RectEntity(
                Vector2.Zero, new Vector2(300, 20),
                new Color(0, 0, 0, 0), $"SCROLL ROW {i + 1}");
            Style(row, 13, new Color("#94a3b8"));
        }
        ui.End();

        ui.RectEntity(new Vector2(300, 520), new Vector2(520, 180),
            new Color("#111827"));
        Entity track = ui.RectEntity(
            new Vector2(300, 570), new Vector2(380, 18),
            new Color("#334155"), null, takesInput: true);
        Entity sliderFill = ui.RectEntity(
            new Vector2(205, 570), new Vector2(190, 18),
            new Color("#38bdf8"), "");
        Entity sliderLabel = ui.RectEntity(
            new Vector2(300, 530), new Vector2(400, 24),
            new Color(0, 0, 0, 0), "SLIDER 50");
        Style(sliderLabel, 18, new Color("#e2e8f0"));
        track.AddComponent(new UiSlider(
            sliderFill, sliderLabel, 0, 100, 50, "SLIDER "));

        Entity dropdown = ui.RectEntity(
            new Vector2(1030, 540), new Vector2(300, 44),
            new Color("#2563eb"), "DROPDOWN", takesInput: true);
        Style(dropdown, 18, new Color("#e2e8f0"));
        dropdown.AddComponent(ButtonVisual.Create(new Color("#2563eb")));
        _dropdownList = ui.RectEntity(
            new Vector2(1030, 620), new Vector2(300, 110),
            new Color("#111827"));
        dropdown.AddComponent(new Dropdown(_dropdownList, 0));
        for (int i = 0; i < 3; i++)
        {
            Entity option = ui.RectEntity(
                new Vector2(1030, 590 + i * 30), new Vector2(280, 26),
                new Color("#1e293b"), $"OPTION {i + 1}", takesInput: true);
            Style(option, 14, new Color("#e2e8f0"));
            option.AddComponent(new DropdownOption(dropdown, i));
        }

        ui.RectEntity(new Vector2(300, 780), new Vector2(850, 80),
            new Color("#1e293b"),
            "RECTS · TEXT · CONTAINERS · BUTTONS · SCROLL · SLIDER · DROPDOWN");
        Style(ui.Current, 16, new Color("#e2e8f0"));
    }

    private static void Style(Entity entity, int size, Color color) =>
        entity.AddComponent(new TextStyle(size, color));

    private sealed class ShowcasePointer(UiShowcase owner) : IPointerInputSource
    {
        public PointerInput Read() => new(
            owner.GetViewport().GetMousePosition(),
            Input.IsMouseButtonPressed(MouseButton.Left),
            Input.IsKeyPressed(Key.Shift));
    }
}
