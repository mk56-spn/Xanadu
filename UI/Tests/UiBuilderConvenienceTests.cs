using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Layout;
using Writer.Systems.Input;
using Writer.Systems.Ui;
using Position = Writer.Ecs.Position;
using Writer.Tests.Support;

namespace Writer.Tests.UI;

[TestFixture]
public sealed class UiBuilderConvenienceTests
{
    [Test]
    public void TextHasSensibleDefaultsAndAlignment()
    {
        var store = new EntityStore();
        UiBuilder ui = UiBuilder.Begin(store, new FakeVisualRidFactory()).Layer(0);

        Entity label = ui.Text(
            "HELLO",
            new Vector2(200, 24),
            align: TextAlign.Left);

        Assert.That(label.GetComponent<Text>().Value, Is.EqualTo("HELLO"));
        Assert.That(label.GetComponent<TextStyle>().FontSize,
            Is.EqualTo(UiTheme.FontSize));
        Assert.That(label.GetComponent<TextAlignment>().Align,
            Is.EqualTo(TextAlign.Left));
        Assert.That(label.Tags.Has<TakesInput>(), Is.False);
    }

    [Test]
    public void ButtonAddsVisualAndInputDefaults()
    {
        var store = new EntityStore();
        UiBuilder ui = UiBuilder.Begin(store, new FakeVisualRidFactory()).Layer(0);

        Entity button = ui.Button("PLAY", new Vector2(100, 32));

        Assert.That(button.Tags.Has<TakesInput>(), Is.True);
        Assert.That(button.HasComponent<ButtonVisual>(), Is.True);
        Assert.That(button.GetComponent<ButtonVisual>().Normal,
            Is.EqualTo(UiTheme.Button));
    }

    [Test]
    public void PanelUsesThemePaddingAndSpacing()
    {
        var store = new EntityStore();
        UiBuilder ui = UiBuilder.Begin(store, new FakeVisualRidFactory()).Layer(0);

        ui.Panel(new Vector2(100, 100), new Vector2(300, 200));
        Entity panel = ui.Current;

        Assert.That(panel.GetComponent<UiContainer>().Padding,
            Is.EqualTo(UiTheme.ContainerPadding));
        Assert.That(panel.GetComponent<UiContainer>().Spacing,
            Is.EqualTo(UiTheme.ContainerSpacing));
        Assert.That(panel.GetComponent<FillColor>().Value,
            Is.EqualTo(UiTheme.Panel));
    }

    [Test]
    public void BuilderDropdownOpensAndLaysOutInAContainer()
    {
        var store = new EntityStore();
        UiBuilder ui = UiBuilder.Begin(store, new FakeVisualRidFactory()).Layer(0);
        ui.Container(
            Vector2.Zero,
            new Vector2(300, 200),
            StackDirection.Vertical,
            autoHeight: true);
        UiDropdownControl dropdown = ui.Dropdown(
            "CATEGORY",
            new[] { "ONE", "TWO" },
            size: new Vector2(220, 30));
        Entity container = ui.Current;
        ui.End();

        var root = new SystemRoot(store);
        root.Add(new ContainerLayoutSystem());
        root.Add(new UiDropdownSystem(store));
        dropdown.Button.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 1f));
        root.Update(new UpdateTick(1f / 60f, 2f));

        Dropdown state = dropdown.Button.GetComponent<Dropdown>();
        Assert.That(state.Open, Is.True);
        Assert.That(dropdown.List.GetComponent<Position>().Value,
            Is.Not.EqualTo(UiLayout.Hidden));
        Assert.That(dropdown.Options[0].GetComponent<Position>().Value,
            Is.Not.EqualTo(UiLayout.Hidden));
        Assert.That(
            container.GetComponent<Rectangle>().Bounds.Size.Y,
            Is.GreaterThan(30f));
    }

    [Test]
    public void DropdownCreatesButtonInContainerAndListOnLayer()
    {
        var store = new EntityStore();
        UiBuilder ui = UiBuilder.Begin(store, new FakeVisualRidFactory()).Layer(0);
        ui.Container(
            Vector2.Zero,
            new Vector2(300, 200),
            StackDirection.Vertical,
            autoHeight: true);
        Entity container = ui.Current;

        UiDropdownControl dropdown = ui.Dropdown(
            "CATEGORY",
            new[] { "ONE", "TWO" },
            size: new Vector2(220, 30),
            closeOthers: false);
        ui.End();

        Assert.That(dropdown.Button.Parent.Id, Is.EqualTo(container.Id));
        Assert.That(dropdown.List.Parent.Id,
            Is.EqualTo(container.Id));
        Assert.That(dropdown.Options, Has.Count.EqualTo(2));
        Assert.That(
            dropdown.Options[1].GetComponent<DropdownOption>().Value,
            Is.EqualTo(1));

        Dropdown state = dropdown.Button.GetComponent<Dropdown>();
        Assert.That(state.List.Id, Is.EqualTo(dropdown.List.Id));
        Assert.That(state.CloseOthers, Is.False);
        Assert.That(state.ListSize.Y, Is.GreaterThan(0));
    }

    [Test]
    public void DeleteSubtreeRemovesTheEntityAndAllDescendants()
    {
        var store = new EntityStore();
        Entity root = store.CreateEntity();
        Entity child = store.CreateEntity();
        Entity grandchild = store.CreateEntity();
        root.AddChild(child);
        child.AddChild(grandchild);
        int before = store.Count;

        UiLayout.DeleteSubtree(root);

        Assert.That(store.Count, Is.EqualTo(before - 3));
    }

    [Test]
    public void DropdownListBuildsAListOnAnotherLayerForAnExistingButton()
    {
        var store = new EntityStore();
        var visuals = new FakeVisualRidFactory();
        UiBuilder panel = UiBuilder.Begin(store, visuals).Layer(0);
        panel.Container(Vector2.Zero, new Vector2(300, 60));
        Entity container = panel.Current;
        Entity button = panel.Button("PICK", new Vector2(160, 30));
        Entity menuLayer = UiBuilder.Begin(store, visuals).Layer(6).Current;

        // Resuming on the layer itself puts the new entities on that layer.
        UiDropdownControl dropdown = UiBuilder.Begin(store, visuals).Use(menuLayer)
            .DropdownList(button, new[] { "ONE", "TWO" });

        Assert.That(dropdown.Button.Id, Is.EqualTo(button.Id));
        Assert.That(button.Parent.Id, Is.EqualTo(container.Id));
        Assert.That(dropdown.List.Parent.Id, Is.EqualTo(menuLayer.Id));
        Assert.That(dropdown.List.GetComponent<Drawable>().ZIndex, Is.EqualTo(6));
        Assert.That(dropdown.List.GetComponent<Rectangle>().Bounds.Size.X, Is.EqualTo(160f));
        Assert.That(dropdown.Options, Has.Count.EqualTo(2));
        Assert.That(dropdown.Options[1].Parent.Id, Is.EqualTo(menuLayer.Id));
        Assert.That(dropdown.Options[1].GetComponent<Drawable>().ZIndex, Is.EqualTo(6));
        Assert.That(button.GetComponent<Dropdown>().List.Id, Is.EqualTo(dropdown.List.Id));
    }

    [Test]
    public void ADropdownStartsClosedWithItsListAndOptionsOffScreen()
    {
        var store = new EntityStore();
        UiBuilder ui = UiBuilder.Begin(store, new FakeVisualRidFactory()).Layer(0);

        UiDropdownControl dropdown = ui.Dropdown("PICK", new[] { "ONE", "TWO" });

        Assert.That(dropdown.List.GetComponent<Position>().Value, Is.EqualTo(UiLayout.Hidden));
        foreach (Entity option in dropdown.Options)
            Assert.That(option.GetComponent<Position>().Value, Is.EqualTo(UiLayout.Hidden));
    }
}
