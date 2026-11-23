using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Core.Timeline;
using Xanadu.Core.Tweens;
using Xanadu.Host;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using static Xanadu.Core.Theme.UiMetrics;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Showcase;

/// <summary>
/// Standalone demo of the generic Xanadu.Ui feature set, on the MoonWorks host.
/// Opens on every shared control in each theme; <c>--page=values</c> shows a
/// value field of every shape, <c>--page=demo</c> the old free-placed demo.
/// </summary>
internal sealed class UiShowcase : HostApp
{

    public UiShowcase(string[] args) : base("Xanadu UI Showcase", WindowOptions(args))
    {
        if (Array.Exists(args, arg => arg == "--page=values"))
            BuildValues();
        else if (Array.Exists(args, arg => arg == "--page=demo"))
            Build();
        else
            BuildControls();
    }

    /// <summary>How wide each theme's column of controls is.</summary>
    private const float SetWidth = 460;

    /// <summary>
    /// The default page: every shared control (<see cref="UiNode"/>'s builders and
    /// the tree's specs), once per built-in theme side by side. Anything two
    /// screens both need should be one of these; when a screen builds its own
    /// version instead, this page is where the two would differ.
    /// </summary>
    private void BuildControls()
    {
        var screen = new Vector2(Width, Height);
        UiNode page = UiNode.Layer(Entities, default, UiLayers.Base).Page(screen).Pad(Space.Xl).Gap(Space.Md).Backing();
        page.Add(
            new Label("COMMON CONTROLS", UiType.Heading, ink: Ink.Accent),
            new Label("Every shared control, once per theme. Two screens that need the same thing use one of these.", UiType.Small, ink: Ink.Dim));
        UiNode columns = page.Row().Gap(Space.Xl).Cross(StackCrossAlignment.Start);
        foreach (UiThemeRoles theme in new[] { UiThemes.Tools, UiThemes.Menu, UiThemes.Hud })
            columns.Add(Controls(theme));
    }

    /// <summary>One theme's full set of controls, top to bottom.</summary>
    private static Column Controls(UiThemeRoles theme) =>
        new(gap: Space.Sm, pad: Space.Md, cross: Cross.Start, width: SetWidth, tone: Tone.Solid, style: n => n.Theme(theme))
        {
            new Label(theme.Name.ToUpperInvariant(), UiType.Section, ink: Ink.Accent),

            Caption("TEXT"),
            new Row(gap: Space.Md, cross: Cross.End)
            {
                new Label("Title", UiType.Title),
                new Label("Heading", UiType.Heading),
                new Label("Section", UiType.Section),
            },
            new Row(gap: Space.Md, cross: Cross.End)
            {
                new Label("Label", UiType.Label),
                new Label("Body"),
                new Label("Small", UiType.Small),
                new Label("Caption", UiType.Caption),
                new Label("dim", ink: Ink.Dim),
                new Label("accent", ink: Ink.Accent),
            },

            Caption("BUTTONS"),
            new Row(gap: Space.Sm)
            {
                new Button("PRIMARY", tone: Tone.Primary, tip: "The main action of a screen or group"),
                new Button("SECONDARY", tone: Tone.Secondary, tip: "Every other action"),
                new Button("QUIET", tone: Tone.Quiet, tip: "An action that should not draw the eye"),
                new Button("DANGER", tone: Tone.Danger, tip: "An action that destroys something"),
            },
            new Row(gap: Space.Sm)
            {
                new ToggleButton("ON", "OFF", value: true, tip: "A toggle: bright while on"),
                new ToggleButton("ON", "OFF", value: false, tip: "A toggle: muted while off"),
                new Choice(["ONE", "TWO", "THREE"], 1, 140, tip: "A pick that reads what is picked"),
                new Custom(p => p.Dropdown("MENU ▼", ["NEW", "OPEN", "SAVE"], 0, new Vector2(120, Control.Xs)).Button, tip: "A menu: its text stays"),
            },

            Caption("ROWS"),
            new Slider("SLIDER", 0, 1, 0.4f, SetWidth - Space.Md * 2, tip: "Drag along the track"),
            new Stepper("STEPPER", "3 of 8", SetWidth - Space.Md * 2),
            new Field("a text field", width: SetWidth - Space.Md * 2, tip: "Click to type"),

            Caption("VALUES"),
            new ValueField("FLOAT", UiValueShape.Float, [0.5f], SetWidth - Space.Md * 2),
            new ValueField("INT", UiValueShape.Int, [4], SetWidth - Space.Md * 2),
            new ValueField("FLOAT3", UiValueShape.Vector(3), [0, 1, 0], SetWidth - Space.Md * 2),
            new ValueField("BOOL3", UiValueShape.Vector(3, UiScalar.Bool), [1, 0, 1], SetWidth - Space.Md * 2),

            Caption("METERS"),
            new Row(gap: Space.Sm)
            {
                new Meter(0.4f, width: 130, height: 10, tip: "Well under its limit"),
                new Meter(0.8f, width: 130, height: 10, tip: "Close to its limit"),
                new Meter(0.95f, width: 130, height: 10, tip: "At its limit"),
            },
        };

    private static Label Caption(string text) => new(text, UiType.Caption, ink: Ink.Dim);

    /// <summary><c>--page=values</c>: a value field of every shape a shader uniform can take.</summary>
    private void BuildValues()
    {
        var screen = new Vector2(Width, Height);
        UiNode page = UiNode.Layer(Entities, default, UiLayers.Base).Page(screen).Pad(32).Gap(16).Backing();
        page.Text("VALUE FIELDS", UiType.Heading).AccentText();
        UiNode echo = page.Text("drag a slot sideways, click to type, right-click to reset").DimText().Size(900, 24);

        UiNode columns = page.Row().Gap(32).Cross(StackCrossAlignment.Start);
        UiNode left = columns.Column().Gap(14).Cross(StackCrossAlignment.Start);
        UiNode middle = columns.Column().Gap(14).Cross(StackCrossAlignment.Start);
        UiNode right = columns.Column().Gap(14).Cross(StackCrossAlignment.Start);

        void Field(UiNode column, string label, string type, float[] values)
        {
            UiValueShape shape = UiValueShape.Parse(type) ?? UiValueShape.Float;
            Entity field = column.ValueField($"{label}  ({type})", shape, values, 300).Entity;
            field.OnValueChanged((now, live) =>
                UiText.Set(echo, $"{label} = {string.Join(", ", Array.ConvertAll(now, v => v.ToString("0.###")))}{(live ? "  (dragging)" : "")}"));
        }

        Field(left, "strength", "float", [0.5f]);
        Field(left, "steps", "int", [4]);
        Field(left, "count", "uint", [3]);
        Field(left, "enabled", "bool", [1]);
        Field(left, "mask", "bool3", [1, 0, 1]);

        Field(middle, "offset", "float2", [0.25f, -0.5f]);
        Field(middle, "direction", "float3", [0, 1, 0]);
        Field(middle, "plane", "float4", [1, 0, 0, 2]);
        Field(middle, "cell", "int2", [3, -2]);
        Field(middle, "size", "uint3", [16, 16, 1]);

        Field(right, "turn", "float2x2", [0.7f, -0.7f, 0.7f, 0.7f]);
        Field(right, "basis", "float3x3", [1, 0, 0, 0, 1, 0, 0, 0, 1]);
        Field(right, "shear", "float2x3", [1, 0.5f, 0, 0, 1, 0]);
        Field(right, "world", "float4x4", [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);

        Field(columns.Column().Gap(14).Cross(StackCrossAlignment.Start), "tint", "colour", [0.98f, 0.8f, 0.08f, 1]);
    }

    /// <summary><c>--resolution=WxH</c> (default 1600x900), <c>--shot=png</c> and <c>--shot-frame=n</c>.</summary>
    private static HostWindowOptions WindowOptions(string[] args)
    {
        uint width = 1600, height = 900;
        string? shot = null;
        int frame = 60;
        foreach (string arg in args)
        {
            if (arg.StartsWith("--shot=", StringComparison.Ordinal))
                shot = System.IO.Path.GetFullPath(arg["--shot=".Length..]);
            else if (arg.StartsWith("--shot-frame=", StringComparison.Ordinal))
                frame = int.Parse(arg["--shot-frame=".Length..]);
            else if (arg.StartsWith("--resolution=", StringComparison.Ordinal))
            {
                string[] size = arg["--resolution=".Length..].Split('x');
                width = uint.Parse(size[0]);
                height = uint.Parse(size[1]);
            }
        }

        return new HostWindowOptions(width, height, shot, frame);
    }

    protected override void Tick(float delta, float time) { }

    private void Build()
    {
        var screen = new Vector2(Width, Height);
        UiBuilder ui = UiBuilder.Begin(Entities).Layer(UiLayers.Base);

        ui.RectEntity(screen / 2, screen, Palette.Background);
        ui.RectEntity(new Vector2(120, 60), new Vector2(900, 64),
            Palette.Surface, "XANADU.UI SHOWCASE");
        Style(ui.Current, 32, Palette.Accent);

        Entity glowing = ui.RectEntity(new Vector2(705, 250), new Vector2(260, 70),
            new Color(0, 0, 0, 0), "GLOWING TEXT");
        Style(glowing, 34, new Color("#fef9c3"));
        glowing.AddComponent(new Glow(new Color(0.98f, 0.8f, 0.08f, 0.5f)));

        ui.Container(
            new Vector2(300, 170),
            new Vector2(520, 120),
            spacing: 12,
            color: Palette.Surface,
            padding: 16);
        for (int i = 0; i < 3; i++)
        {
            Entity button = ui.RectEntity(
                Vector2.Zero, new Vector2(150, 60), Palette.Primary,
                $"BUTTON {i + 1}", takesInput: true);
            Style(button, 18, Palette.Text);
            button.AddComponent(ButtonVisual.Create(Palette.Primary));
            button.AddComponent(new UiTooltip($"Button {i + 1} explains itself"));
            if (i == 1)
            {
                // Tweens stack: a bob that never ends, plus a nudge right on top of it.
                UiTween.Play(button, Tween.MoveBy(new Vector2(0, -14), Clip.Seconds(0f, 0.4f).Eased(EasePreset.SineInOut).Bounced()).Looped());
                UiTween.MoveBy(button, new Vector2(24, 0), Clip.FromNow(1.2f).Eased(EasePreset.CubicOut));
            }
        }
        ui.End();

        ui.Container(
            new Vector2(300, 330),
            new Vector2(520, 150),
            StackDirection.Vertical,
            spacing: 8,
            color: Palette.SurfaceAlt,
            padding: 14);
        for (int i = 0; i < 4; i++)
        {
            Entity row = ui.RectEntity(
                Vector2.Zero, new Vector2(460, 26), Palette.Muted,
                $"NESTED ROW {i + 1}");
            Style(row, 15, Palette.TextMuted);
            // After layout: the container keeps the rows' places, they slide in to them.
            UiTween.MoveIn(row, new Vector2(-260, 0), Clip.FromNow(2.4f).Eased(EasePreset.CubicOut).Staggered(i, 0.25f));
        }
        ui.End();

        ui.RectEntity(new Vector2(1030, 170), new Vector2(360, 260),
            Palette.Surface, "SCROLL VIEW");
        Style(ui.Current, 18, Palette.Accent);
        const float scrollContentHeight = 220f;
        const float scrollViewportHeight = 150f;
        float scrollBaseY = -scrollViewportHeight / 2 + scrollContentHeight / 2;
        ui.Container(
            new Vector2(1030, 300),
            new Vector2(320, scrollViewportHeight),
            StackDirection.Vertical,
            spacing: 0,
            clip: true,
            color: Palette.SurfaceDeep,
            padding: 0,
            takesInput: true);
        Entity scrollViewport = ui.Current;
        ui.Container(
            new Vector2(0, scrollBaseY),
            new Vector2(320, scrollContentHeight),
            StackDirection.Vertical,
            spacing: 2,
            color: new Color(0, 0, 0, 0),
            padding: 6);
        Entity scrollContent = ui.Current;
        for (int i = 0; i < 6; i++)
        {
            Entity row = ui.RectEntity(
                Vector2.Zero, new Vector2(300, 20),
                new Color(0, 0, 0, 0), $"SCROLL ROW {i + 1}");
            Style(row, 13, Palette.TextDim);
        }
        ui.End();
        scrollViewport.AddComponent(new ScrollOffset());
        ui.End();

        ui.RectEntity(new Vector2(300, 520), new Vector2(520, 180),
            Palette.Surface);
        Entity track = ui.RectEntity(
            new Vector2(300, 570), new Vector2(380, 18),
            Palette.Muted, takesInput: true);
        Entity sliderFill = ui.RectEntity(
            new Vector2(205, 570), new Vector2(190, 18),
            Palette.Info, "");
        Entity sliderLabel = ui.RectEntity(
            new Vector2(300, 530), new Vector2(400, 24),
            new Color(0, 0, 0, 0), "SLIDER 50");
        Style(sliderLabel, 18, Palette.Text);
        track.AddComponent(new UiSlider(
            sliderFill, sliderLabel, 0, 100, 50, "SLIDER "));

        ui.Container(
            new Vector2(1030, 570),
            new Vector2(320, 170),
            StackDirection.Vertical,
            spacing: 8,
            color: Palette.Surface,
            padding: 12,
            cross: StackCrossAlignment.Center,
            autoHeight: true);
        for (int i = 0; i < 3; i++)
        {
            ui.Dropdown(
                $"DROPDOWN {i + 1}",
                new[] { "OPTION 1", "OPTION 2", "OPTION 3" },
                size: new Vector2(280, 32));
        }
        ui.End();

        ui.Container(
            new Vector2(1465, 570),
            new Vector2(220, 170),
            StackDirection.Vertical,
            spacing: 8,
            color: Palette.Surface,
            padding: 10,
            cross: StackCrossAlignment.Center,
            autoHeight: true);
        for (int i = 0; i < 2; i++)
        {
            ui.Dropdown(
                $"CATEGORY {(char)('A' + i)}",
                ["ONE", "TWO", "THREE"],
                size: new Vector2(190, 32),
                closeOthers: false,
                buttonColor: Palette.Selected,
                optionColor: _ => Palette.SelectedDeep);
        }
        ui.End();

        ui.Panel(
            new Vector2(1440, 330),
            new Vector2(300, 360),
            cross: StackCrossAlignment.Start,
            autoHeight: true,
            main: StackMainAlignment.Start);
        ui.Text("TREE", new Vector2(270, 22), UiTheme.FontSize, UiTheme.Accent);
        Entity tree = ui.Tree(270);
        ui.SetTreeItems(tree, new UiTreeItem[]
        {
            new("world", null, "World"),
            new("hero", "world", "Hero"),
            new("hero-sword", "hero", "Sword"),
            new("hero-cape", "hero", "Cape"),
            new("hero-cape-tip", "hero-cape", "Cape tip"),
            new("lantern", "world", "Lantern"),
            new("ground", null, "Ground"),
        });
        UiTreeControl.Select(tree, "hero-sword", raiseChanged: false);
        ui.End();

        ui.RectEntity(new Vector2(900, 770), new Vector2(360, 220),
            Palette.Surface);
        Entity cheeseLabel = ui.RectEntity(
            new Vector2(900, 686), new Vector2(320, 24),
            new Color(0, 0, 0, 0), "CHEESE SELECTOR");
        Style(cheeseLabel, 16, Palette.Accent);
        ui.Wheel(
            new Vector2(900, 792),
            82,
            new[] { "1", "2", "3", "4", "5", "6", "7", "8" },
            selected: 1);

        ui.RectEntity(new Vector2(1330, 770), new Vector2(320, 220),
            Palette.Surface);
        Entity dialLabel = ui.RectEntity(
            new Vector2(1330, 686), new Vector2(300, 24),
            new Color(0, 0, 0, 0), "DIAL SELECTOR");
        Style(dialLabel, 16, Palette.Info);
        ui.Wheel(
            new Vector2(1330, 792),
            76,
            new[] { "N", "E", "S", "W" },
            selected: 0,
            hubRadius: 0.32f,
            gap: 0.05f);

        ui.RectEntity(new Vector2(300, 780), new Vector2(850, 80),
            Palette.SurfaceAlt,
            "RECTS · TEXT · CONTAINERS · BUTTONS · SCROLL · SLIDER · DROPDOWN · WHEEL");
        Style(ui.Current, 16, Palette.Text);
    }

    private static void Style(Entity entity, int size, Color color) =>
        entity.AddComponent(new TextStyle(size, color));
}
