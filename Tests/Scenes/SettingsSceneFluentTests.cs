using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Strings;
using Xanadu.Systems.Layout;
using Xanadu.Theme;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Scenes;

/// <summary>
/// <c>Scenes/SettingsScene.cs</c> translated to the fluent node API and split
/// into one method per piece of the screen, kept as the reference diff for the
/// migration. It is not wired to the scene controller; the assertions only
/// prove that the translation compiles and lays out.
/// </summary>
[TestFixture]
public sealed class SettingsSceneFluentTests : EcsSystemTest
{
    private static readonly Vector2 Screen = new(1920, 1080);

    private UiNode _card;

    protected override void OnEcsSetUp()
    {
        UiBuilder.DefaultVisuals = new FakeVisualRidFactory();
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new UiAnchorSystem());

        Entity root = Store.CreateEntity();
        Build(Store, root, Screen, new KeybindSettings(persist: false),
            new DisplaySettings(persist: false, apply: false));
        Frame(6);
    }

    [TearDown]
    public void TearDown() => UiBuilder.DefaultVisuals = null;

    /// <summary>What the screen is made of, in reading order.</summary>
    private void Build(
        EntityStore entities,
        Entity root,
        Vector2 screen,
        KeybindSettings keybinds,
        DisplaySettings display,
        IVisualRidFactory? visuals = null)
    {
        UiNode layer = UiNode.Layer(entities, root, SceneUi.MenuLayer, visuals);

        layer.Backdrop(screen);
        layer.Heading(LocalizedText.Get(SettingsStrings.Title), 40)
            .At(new Vector2(screen.X / 2, 90));

        _card = layer.Card(screen);
        _card.VsyncRow(display);
        _card.Heading(LocalizedText.Get(SettingsStrings.Keybinds), 22);
        _card.KeybindTable(keybinds);
        _card.Footer();

        layer.Button(LocalizedText.Get(SettingsStrings.Back), 18, SceneUi.MutedButtonColor)
            .Size(150, 44)
            .At(new Vector2(100, 44))
            .On(SceneKind.Main);
    }

    [Test]
    public void TheCardHoldsEveryKeybindAndBothColumnsAreAligned()
    {
        Assert.That(_card.Entity.ChildCount, Is.EqualTo(4),
            "vsync row, keybinds heading, grid, footer");

        var buttons = new System.Collections.Generic.List<Entity>(
            Store.Query<KeybindButton>().ToEntityList());
        Assert.That(buttons.Count, Is.EqualTo(KeybindSettings.Actions.Length));

        foreach (Entity button in buttons)
            Assert.That(Area(_card.Entity).Encloses(Area(button)), Is.True);

        Assert.That(buttons[0].GetComponent<Position>().Value.X,
            Is.EqualTo(buttons[1].GetComponent<Position>().Value.X).Within(0.5f));
        Assert.That(buttons[0].GetComponent<Position>().Value.Y,
            Is.LessThan(buttons[1].GetComponent<Position>().Value.Y));
        Assert.That(Store.Query<VSyncToggle>().Count, Is.EqualTo(1));
    }

    private static Rect2 Area(Entity entity)
    {
        Rect2 bounds = entity.GetComponent<Rectangle>().Bounds;
        Vector2 center = UiTransform.WorldPosition(
            entity,
            entity.GetComponent<Position>().Value);
        return new Rect2(center + bounds.Position, bounds.Size);
    }
}

/// <summary>
/// The pieces of the screen, one method each. Every helper takes the node it
/// builds under and returns the node it wants the caller to keep, so it cannot
/// reach outside its own subtree.
/// </summary>
internal static class SettingsSceneParts
{
    private const float CardWidth = 760;

    public static void Backdrop(this UiNode layer, Vector2 screen) =>
        layer.Rect(SceneUi.BackgroundColor).Cover(screen).Below();

    /// <summary>The panel that holds the screen, sized to the window.</summary>
    public static UiNode Card(this UiNode layer, Vector2 screen)
    {
        float height = Mathf.Min(600, Mathf.Max(500, screen.Y - 120));
        return layer.Panel().Box(new Rect2(
            new Vector2((screen.X - CardWidth) / 2, screen.Y / 2 + 40 - height / 2),
            new Vector2(CardWidth, height)));
    }

    public static UiNode Heading(this UiNode parent, string text, int size) =>
        parent.Text(text, size, SceneUi.AccentColor);

    /// <summary>One label and one switch, side by side.</summary>
    public static void VsyncRow(this UiNode card, DisplaySettings display)
    {
        string on = LocalizedText.Get(SettingsStrings.VSyncOn);
        string off = LocalizedText.Get(SettingsStrings.VSyncOff);

        UiNode row = card.Row().Gap(24);
        row.Text(LocalizedText.Get(SettingsStrings.VSync), 18, SceneUi.TextColor);
        row.Button(display.VSyncEnabled ? on : off, 16, SceneUi.MutedButtonColor)
            .Size(240, 38)
            .With(new Toggle(on, off, display.VSyncEnabled, Palette.Primary,
                SceneUi.MutedButtonColor))
            .With(new VSyncToggle());
    }

    /// <summary>Every bindable action, two columns, one row each.</summary>
    public static void KeybindTable(this UiNode card, KeybindSettings keybinds)
    {
        UiNode table = card.Grid(2, gap: 64, rowGap: 4);
        table.Column(LocalizedText.Get(SettingsStrings.Action));
        table.Column(LocalizedText.Get(SettingsStrings.Key));

        foreach (KeybindAction action in KeybindSettings.Actions)
        {
            table.Text(KeybindSettings.LabelFor(action), 18, SceneUi.TextColor)
                .Align(TextAlign.Left);
            table.Button(KeybindSettings.KeyLabel(keybinds.Get(action)), 16,
                    SceneUi.MutedButtonColor)
                .Size(240, 38)
                .With(new KeybindButton(action));
        }
    }

    public static void Column(this UiNode table, string label) =>
        table.Text(label, 14, SceneUi.DimTextColor).Align(TextAlign.Left);

    public static void Footer(this UiNode card) =>
        card.Text(LocalizedText.Get(SettingsStrings.Footer), 14, SceneUi.DimTextColor);
}

/// <summary>
/// The scene-level sugar a migration adds once, beside <c>SceneUi</c>: it
/// replaces the SceneUi.Button / ButtonEntity / CarouselButton family.
/// </summary>
internal static class UiNodeSceneActions
{
    public static UiNode On(this UiNode node, SceneKind target) =>
        node.With(new SceneAction(target));

    public static UiNode On(this UiNode node, SceneActionId action) =>
        node.With(new SceneCommand(action));
}
