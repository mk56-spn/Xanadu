using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Host;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Ui.Showcase;

/// <summary>Standalone demo of the generic Xanadu.Ui feature set, on the MoonWorks host.</summary>
internal sealed class UiShowcase : HostApp
{

    public UiShowcase(string[] args) : base("Xanadu UI Showcase", WindowOptions(args))
    {
        Build();
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
        glowing.AddComponent(new TextGlow(new Color(0.98f, 0.8f, 0.08f, 0.5f)));

        ui.Container(
            new Vector2(300, 170),
            new Vector2(520, 120),
            StackDirection.Horizontal,
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
        scrollViewport.AddComponent(new ScrollView());
        ui.End();

        ui.RectEntity(new Vector2(300, 520), new Vector2(520, 180),
            Palette.Surface);
        Entity track = ui.RectEntity(
            new Vector2(300, 570), new Vector2(380, 18),
            Palette.Muted, null, takesInput: true);
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
