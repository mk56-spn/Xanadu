using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Layout;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

/// <summary>
/// The fluent node API: children go to the node they were called on, styling
/// edits components in place, and a tree built this way lays out exactly like
/// the one <see cref="UiBuilder"/> builds.
/// </summary>
[TestFixture]
public sealed class UiNodeTests
{
    private static readonly Color Transparent = new(0, 0, 0, 0);
    private static readonly Color Red = new(0.9f, 0.2f, 0.2f);
    private static readonly Color Blue = new(0.2f, 0.3f, 0.9f);

    private EntityStore _store = null!;
    private FakeVisualRidFactory _visuals = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _visuals = new FakeVisualRidFactory();
        UiBuilder.DefaultVisuals = _visuals;
    }

    [TearDown]
    public void TearDown() => UiBuilder.DefaultVisuals = null;

    private UiNode Layer(int zIndex = 0) =>
        UiNode.Layer(_store, default, zIndex, _visuals);

    [Test]
    public void ChildrenGoToTheNodeTheyWereCalledOn()
    {
        UiNode layer = Layer();
        UiNode page = layer.Page();
        UiNode row = page.Row();
        row.Text("A");
        row.Text("B");
        page.Text("C");

        Assert.That(row.Entity.Parent, Is.EqualTo(page.Entity));
        Assert.That(row.Entity.ChildCount, Is.EqualTo(2));
        Assert.That(page.Entity.ChildCount, Is.EqualTo(2),
            "the row and the text called on the page, not on the row");
        Assert.That(page.Entity.Parent, Is.EqualTo(layer.Entity));
    }

    [Test]
    public void PageFillsTheStageAndCarriesIt()
    {
        UiNode layer = Layer();
        UiNode page = layer.Page();

        Assert.That(layer.Stage, Is.EqualTo(UiStage.Full));
        Assert.That(page.Stage, Is.EqualTo(UiStage.Full));
        Assert.That(page.Entity.GetComponent<Rectangle>().Bounds.Size,
            Is.EqualTo(UiStage.Design1080));
        Assert.That(page.Entity.GetComponent<Position>().Value,
            Is.EqualTo(UiStage.Full.Center));
    }

    [Test]
    public void ALayerCanBeAuthoredAgainstAnotherStage()
    {
        var stage = new UiStage(new Vector2(1920, 1080), new Vector2(2560, 1080), 1f);
        UiNode layer = UiNode.Layer(_store, default, 0, _visuals, stage);
        UiNode page = layer.Page();

        Assert.That(page.Entity.GetComponent<Rectangle>().Bounds.Size,
            Is.EqualTo(new Vector2(2560, 1080)));
    }

    [Test]
    public void ChainedCallsEditTheComponentsInPlace()
    {
        UiNode row = Layer().Row()
            .Gap(8)
            .Pad(4)
            .Main(StackMainAlignment.End)
            .Cross(StackCrossAlignment.Start);
        UiContainer container = row.Entity.GetComponent<UiContainer>();

        Assert.That(container.Spacing, Is.EqualTo(8));
        Assert.That(container.Padding, Is.EqualTo(4));
        Assert.That(container.Main, Is.EqualTo(StackMainAlignment.End));
        Assert.That(container.Cross, Is.EqualTo(StackCrossAlignment.Start));
    }

    [Test]
    public void GapMarksTheContainerAndItsParentForLayout()
    {
        UiNode page = Layer().Page();
        UiNode row = page.Row();

        page.Entity.RemoveTag<LayoutDirty>();
        row.Gap(6);

        Assert.That(row.Entity.Tags.Has<LayoutDirty>(), Is.True);
        Assert.That(row.Entity.Tags.Has<Dirty>(), Is.True);
        Assert.That(page.Entity.Tags.Has<LayoutDirty>(), Is.True,
            "the parent must re-stack its child");
    }

    [Test]
    public void SizePinsADimensionAndStopsAutoSizing()
    {
        UiNode row = Layer().Row();
        Assert.That(row.Entity.GetComponent<UiContainer>().AutoWidth, Is.True);

        row.Size(200, 40);
        UiContainer container = row.Entity.GetComponent<UiContainer>();

        Assert.That(container.AutoWidth, Is.False);
        Assert.That(container.AutoHeight, Is.False);
        Assert.That(row.Entity.GetComponent<Rectangle>().Bounds.Size,
            Is.EqualTo(new Vector2(200, 40)));
    }

    [Test]
    public void WidthKeepsTheHeightAutomatic()
    {
        UiNode column = Layer().Column().Width(180);
        UiContainer container = column.Entity.GetComponent<UiContainer>();

        Assert.That(container.AutoWidth, Is.False);
        Assert.That(container.AutoHeight, Is.True);

        column.AutoWidth();
        Assert.That(column.Entity.GetComponent<UiContainer>().AutoWidth, Is.True);
    }

    [Test]
    public void EdgeHugsItsParentAndLeavesTheFlow()
    {
        UiNode header = Layer().Row().Size(800, 80);
        UiNode left = header.Edge(UiEdge.Left, gap: 12);

        Assert.That(left.Entity.Parent, Is.EqualTo(header.Entity));
        Assert.That(left.Entity.Tags.Has<LayoutIgnore>(), Is.True);
        Assert.That(left.Entity.GetComponent<UiAnchor>().Anchor,
            Is.EqualTo(new Vector2(0, 0.5f)));
        Assert.That(left.Entity.GetComponent<UiContainer>().Spacing,
            Is.EqualTo(12));
        Assert.That(left.Entity.GetComponent<UiContainer>().Direction,
            Is.EqualTo(StackDirection.Horizontal));
        Assert.That(left.Entity.GetComponent<UiContainer>().AutoWidth, Is.True);
    }

    [Test]
    public void TextIsMeasuredFromItsContent()
    {
        UiNode label = Layer().Text("HELLO", 20);
        Vector2 size = label.Entity.GetComponent<Rectangle>().Bounds.Size;

        Assert.That(size.X,
            Is.EqualTo(
                UiTextMetrics.Width("HELLO", 20) * UiNode.MeasureSlack +
                UiNode.TextInset * 2).Within(0.01f));
        Assert.That(size.Y, Is.EqualTo(20 * UiNode.TextLineHeight));
        Assert.That(label.Entity.GetComponent<TextStyle>().FontSize,
            Is.EqualTo(20));
    }

    [Test]
    public void TextStyleChains()
    {
        UiNode label = Layer()
            .Text("HELLO")
            .Font(22)
            .TextColor(Red)
            .Bold()
            .Align(TextAlign.Left);

        TextStyle style = label.Entity.GetComponent<TextStyle>();
        Assert.That(style.FontSize, Is.EqualTo(22));
        Assert.That(style.Color, Is.EqualTo(Red));
        Assert.That(style.Bold, Is.True);
        Assert.That(label.Entity.GetComponent<TextAlignment>().Align,
            Is.EqualTo(TextAlign.Left));
    }

    [Test]
    public void SetTextUpdatesTheExistingEntity()
    {
        UiNode label = Layer().Text("0 notes");
        Entity entity = label.Entity;
        entity.RemoveTag<Dirty>();

        label.SetText("4 notes");

        Assert.That(label.Entity.Id, Is.EqualTo(entity.Id));
        Assert.That(label.Entity.GetComponent<Text>().Value, Is.EqualTo("4 notes"));
        Assert.That(label.Entity.Tags.Has<Dirty>(), Is.True);
    }

    [Test]
    public void WithAttachesToolStateWithoutTouchingTheChain()
    {
        UiNode button = Layer().Button("SAVE").With(new TestCommand(7));

        Assert.That(button.Entity.GetComponent<TestCommand>().Id, Is.EqualTo(7));
        Assert.That(button.Entity.Tags.Has<TakesInput>(), Is.True,
            "a button is hit-tested by default");
    }

    [Test]
    public void WrapContinuesAnExistingEntity()
    {
        UiNode panel = Layer().Panel();
        UiNode resumed = UiNode.Wrap(panel.Entity);

        resumed.Text("ADDED LATER");

        Assert.That(panel.Entity.ChildCount, Is.EqualTo(1));
        foreach (Entity child in panel.Entity.ChildEntities)
            Assert.That(child.GetComponent<Text>().Value, Is.EqualTo("ADDED LATER"));
    }

    [Test]
    public void DropdownKeepsItsListWithTheNodeThatOpenedIt()
    {
        UiNode panel = Layer().Panel().Size(300, 200);
        UiDropdownNode dropdown = panel.Dropdown(
            "CATEGORY",
            new[] { "ONE", "TWO" },
            size: new Vector2(200, 30));

        Assert.That(dropdown.Button.Entity.Parent, Is.EqualTo(panel.Entity));
        Assert.That(dropdown.List.Entity.Parent, Is.EqualTo(panel.Entity));
        Assert.That(dropdown.Options.Count, Is.EqualTo(2));
        Assert.That(dropdown.Options[1].Entity.GetComponent<Text>().Value,
            Is.EqualTo("TWO"));

        UiNode asNode = dropdown;
        Assert.That(asNode.Entity, Is.EqualTo(dropdown.Button.Entity));
    }

    [Test]
    public void NullNodesFailLoudlyOnCreationAndQuietlyOnStyling()
    {
        UiNode none = default;

        Assert.That(none.IsNull, Is.True);
        Assert.Throws<InvalidOperationException>(() => none.Text("x"));
        Assert.That(none.SetText("x").IsNull, Is.True);
        Assert.That(none.Fill(Red).IsNull, Is.True);
    }

    [Test]
    public void AFixedContainerLaysOutLikeTheOldBuilder()
    {
        var oldStore = new EntityStore();
        Entity oldLayer = BuildWithBuilder(oldStore);

        Entity newLayer = BuildWithNodes();

        LayOut(oldStore);
        LayOut(_store);

        Assert.That(Snapshot(newLayer), Is.EqualTo(Snapshot(oldLayer)));
    }

    private Entity BuildWithBuilder(EntityStore store)
    {
        UiBuilder builder = UiBuilder.Begin(store, _visuals).Layer(7);
        Entity layer = builder.Current;

        builder.Container(
            new Vector2(300, 200),
            new Vector2(600, 400),
            StackDirection.Vertical,
            spacing: 10,
            color: Transparent,
            cross: StackCrossAlignment.Center,
            main: StackMainAlignment.Start);
        Entity page = builder.Current;

        builder.Container(
            Vector2.Zero,
            new Vector2(400, 40),
            StackDirection.Horizontal,
            spacing: 8,
            color: Transparent,
            cross: StackCrossAlignment.Center,
            main: StackMainAlignment.Start);
        builder.Button("A", new Vector2(40, 20), Red, fontSize: 14);
        builder.Button("B", new Vector2(40, 20), Red, fontSize: 14);
        builder.End();

        builder.RectEntity(Vector2.Zero, new Vector2(200, 50), Blue, "C");
        builder.End();
        builder.End();

        return layer;
    }

    private Entity BuildWithNodes()
    {
        UiNode layer = Layer(7);
        UiNode page = layer.Column()
            .Box(new Rect2(0, 0, 600, 400))
            .Gap(10)
            .Main(StackMainAlignment.Start)
            .Cross(StackCrossAlignment.Center);

        UiNode row = page.Row()
            .Size(400, 40)
            .Gap(8)
            .Main(StackMainAlignment.Start)
            .Cross(StackCrossAlignment.Center);
        row.Button("A", 14, Red).Size(40, 20);
        row.Button("B", 14, Red).Size(40, 20);

        page.Rect(Blue, "C", new Vector2(200, 50));
        return layer.Entity;
    }

    private static void LayOut(EntityStore store)
    {
        var root = new SystemRoot(store);
        root.Add(new ContainerLayoutSystem());
        root.Update(new UpdateTick(1f / 60f, 1f));
        root.Update(new UpdateTick(1f / 60f, 2f));
    }

    private static List<string> Snapshot(Entity layer)
    {
        var rows = new List<string>();
        foreach (Entity child in layer.ChildEntities)
            Walk(child, rows);
        return rows;
    }

    private static void Walk(Entity entity, List<string> rows)
    {
        Vector2 size = entity.GetComponent<Rectangle>().Bounds.Size;
        Vector2 at = entity.GetComponent<Position>().Value;
        string text = entity.TryGetComponent(out Text value) ? value.Value : "";
        rows.Add($"{size}@{at} '{text}'");
        foreach (Entity child in entity.ChildEntities)
            Walk(child, rows);
    }

    /// <summary>Tool-owned state, standing in for SceneCommand or EditorButton.</summary>
    public struct TestCommand(int id) : IComponent
    {
        public int Id = id;
    }
}
