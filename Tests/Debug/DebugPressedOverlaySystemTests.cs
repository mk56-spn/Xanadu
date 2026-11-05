using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Input;
using Xanadu.Systems.Layout;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Debug;

[TestFixture]
public sealed class DebugPressedOverlaySystemTests : EcsSystemTest
{
    private const float Frame60 = 1f / 60f;

    private ScriptedToggle _toggle = null!;
    private ScriptedPaging _paging = null!;

    protected override void OnEcsSetUp()
    {
        _toggle = new ScriptedToggle { Pressed = true };
        _paging = new ScriptedPaging();
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new DebugPressedOverlaySystem(_toggle, Store, Root, paging: _paging, visuals: new FakeVisualRidFactory()));
    }

    private List<string> Texts() => Store.Query<Text>().ToEntityList()
        .Select(entity => entity.GetComponent<Text>().Value)
        .ToList();

    private ArchetypeQuery DebugEntities => Store.Query().WithDisabled().AllTags(Tags.Get<DebugUi>());

    private void AddRectangle() =>
        Store.CreateEntity(new Rectangle(new Rect2(0, 0, 10, 10)));

    [Test]
    public void TheToggleBuildsThePanelsAndRemovesThem()
    {
        Frame();
        Assert.That(Texts(), Does.Contain("F3 ECS DEBUG"));

        _toggle.Pressed = false;
        Frame();
        _toggle.Pressed = true;
        Frame();

        Assert.That(Texts(), Does.Not.Contain("F3 ECS DEBUG"));
        Assert.That(DebugEntities.Count, Is.EqualTo(0));
    }

    [Test]
    public void HoldingToggleDoesNotRepeat()
    {
        Frame(3);

        Assert.That(Texts(), Does.Contain("F3 ECS DEBUG"));
    }

    [Test]
    public void TheSystemTableListsEverySystemWithItsTimings()
    {
        Frame();

        Assert.That(Texts(), Does.Contain("last ms").And.Contain("avg ms").And.Contain("upd"));
        Assert.That(Texts(), Does.Contain("ContainerLayout"));
    }

    [Test]
    public void TheArchetypeTableHasItsColumnsAndRows()
    {
        AddRectangle();
        Frame();

        Assert.That(Texts(), Has.Some.StartsWith("ARCHETYPES "));
        foreach (string column in new[] { "#", "n", "Δ", "cap", "use", "KB", "components", "tags" })
            Assert.That(Texts(), Does.Contain(column));
        Assert.That(Texts(), Does.Contain("Rectangle"));
    }

    [Test]
    public void ThePanelsLeaveThemselvesOutOfWhatTheyReport()
    {
        AddRectangle();
        Frame(2);

        Assert.That(Texts(), Has.Some.Matches("Entities +1 "));
        Assert.That(Texts(), Has.Some.Matches("Containers +0"));
        Assert.That(Texts(), Has.None.Contains("#DebugUi"));
    }

    [Test]
    public void AColumnGrowsToFitItsWidestCellAndThePanelFollows()
    {
        Frame(3);
        Entity panels = DebugEntities.Entities
            .First(entity => entity.HasComponent<UiBorder>());
        float before = panels.GetComponent<Rectangle>().Bounds.Size.X;

        // Enough rows that the # column needs more than one digit.
        for (int i = 0; i < 12; i++)
            Store.CreateEntity(new Rectangle(new Rect2(0, 0, 1, 1)), new Position(new Vector2(i, 0)));
        Frame(3);

        Assert.That(panels.GetComponent<Rectangle>().Bounds.Size.X, Is.GreaterThanOrEqualTo(before));
        foreach (Entity cell in Store.Query<Text>().ToEntityList())
        {
            float width = UiNode.Measure(cell.GetComponent<Text>().Value, cell.GetComponent<TextStyle>().FontSize).X;
            Assert.That(cell.GetComponent<Rectangle>().Bounds.Size.X, Is.GreaterThanOrEqualTo(width - 0.01f));
        }
    }

    [Test]
    public void EveryChildFitsInsideItsPanel()
    {
        AddRectangle();
        _paging.SelectionDown = true;
        Frame(4);

        foreach (Entity panel in DebugEntities.Entities.Where(entity => entity.HasComponent<UiBorder>()))
        {
            Rect2 frame = World(panel);
            foreach (Entity child in panel.ChildEntities)
                Assert.That(frame.Encloses(World(child)), Is.True, "a panel holds its contents");
        }
    }

    [Test]
    public void SelectingARowShowsItsFullSignatureAndSample()
    {
        Store.CreateEntity(new Rectangle(new Rect2(0, 0, 10, 10)), new Text("x"));
        Frame();
        Assert.That(Texts(), Has.None.StartsWith("COMPONENTS "), "no row is selected yet");

        _paging.SelectionDown = true;
        Frame();

        Assert.That(string.Join("", Texts().Where(text => text.StartsWith("COMPONENTS "))), Does.Not.Contain("..."));
        Assert.That(Texts(), Has.Some.StartsWith("TAGS "));
        Assert.That(Texts(), Has.Some.StartsWith("ENTITIES "));
    }

    [Test]
    public void ALongSignatureWrapsAcrossLinesRatherThanBeingCut()
    {
        var lines = UiTextFit.Wrap(new string('x', 400), 16, 200);

        Assert.That(lines.Count, Is.GreaterThan(1));
        Assert.That(string.Join("", lines), Has.Length.EqualTo(400));
        Assert.That(lines, Has.All.Matches<string>(line => UiNode.Measure(line, 16).X <= 200));
    }

    [Test]
    public void SelectionDoesNotRepeatWhileTheKeyIsHeld()
    {
        // Creation order puts the single-entity archetype first; the second holds two.
        AddRectangle();
        Store.CreateEntity(new Rectangle(new Rect2(0, 0, 10, 10)), Tags.Get<Dirty>());
        Store.CreateEntity(new Rectangle(new Rect2(0, 0, 10, 10)), Tags.Get<Dirty>());
        Frame();

        _paging.SelectionDown = true;
        Frame(3);

        string entities = Texts().Single(text => text.StartsWith("ENTITIES "));
        Assert.That(
            entities.Split(", ").Length,
            Is.EqualTo(1),
            "the held key selected the first row once and did not advance to the second");
    }

    private static Rect2 World(Entity entity)
    {
        Rect2 local = entity.GetComponent<Rectangle>().Bounds;
        Vector2 at = UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value);
        return new Rect2(at + local.Position, local.Size).Grow(0.01f);
    }

    private sealed class ScriptedToggle : IOverlayToggleSource
    {
        public bool Pressed;

        public bool IsTogglePressed() => Pressed;
    }

    private sealed class ScriptedPaging : IOverlayPageSource
    {
        public bool Previous;
        public bool Next;
        public bool SelectionUp;
        public bool SelectionDown;
        public bool Sort;

        public bool PreviousPressed() => Previous;

        public bool NextPressed() => Next;

        public bool SelectionUpPressed() => SelectionUp;

        public bool SelectionDownPressed() => SelectionDown;

        public bool SortTogglePressed() => Sort;
    }
}
