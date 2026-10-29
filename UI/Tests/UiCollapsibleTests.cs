using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Layout;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiCollapsibleTests : EcsSystemTest
{
    private Entity _first;
    private Entity _second;
    private Entity _closed;

    protected override void OnEcsSetUp()
    {
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new UiCollapsibleSystem());

        UiBuilder ui = UiBuilder.Begin(Store, new FakeVisualRidFactory()).Layer(0);
        ui.Container(
            new Vector2(300, 400),
            new Vector2(300, 800),
            StackDirection.Vertical,
            cross: StackCrossAlignment.Start,
            main: StackMainAlignment.Start);
        _first = Section(ui, "FIRST", expanded: true);
        _second = Section(ui, "SECOND", expanded: true);
        _closed = Section(ui, "CLOSED", expanded: false);
        ui.End();
        Frame(3);
    }

    private static Entity Section(UiBuilder ui, string title, bool expanded)
    {
        ui.Collapsible(title, 240, expanded);
        Entity section = ui.Current.Parent;
        ui.Button("A", new Vector2(240, 30));
        ui.Button("B", new Vector2(240, 30));
        ui.EndCollapsible();
        return section;
    }

    private static UiCollapsible State(Entity section) => section.GetComponent<UiCollapsible>();

    private static float Height(Entity entity) => entity.GetComponent<Rectangle>().Bounds.Size.Y;

    private static float Top(Entity entity)
    {
        Vector2 center = UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value);
        return center.Y - Height(entity) / 2;
    }

    private void Click(Entity entity)
    {
        entity.AddTag<Pressed>();
        Frame();
        entity.RemoveTag<Pressed>();
        Frame(2);
    }

    [Test]
    public void HeaderShowsTheTitleAndState()
    {
        Assert.That(State(_first).Header.GetComponent<Text>().Value, Is.EqualTo("▾  FIRST"));
        Assert.That(State(_closed).Header.GetComponent<Text>().Value, Is.EqualTo("▸  CLOSED"));
    }

    [Test]
    public void AStartClosedSectionIsOnlyItsHeader()
    {
        Assert.That(State(_closed).Body.Tags.Has<LayoutIgnore>(), Is.True);
        Assert.That(Height(_closed), Is.EqualTo(Height(State(_closed).Header)).Within(0.01f));
    }

    [Test]
    public void CollapsingShrinksTheSectionAndPullsTheNextOneUp()
    {
        float before = Top(_second);

        Click(State(_first).Header);

        Assert.That(State(_first).Expanded, Is.False);
        Assert.That(State(_first).Body.Tags.Has<LayoutIgnore>(), Is.True);
        Assert.That(Top(_second), Is.LessThan(before));
        Assert.That(State(_first).Header.GetComponent<Text>().Value, Is.EqualTo("▸  FIRST"));
    }

    [Test]
    public void ExpandingAgainRestoresTheLayout()
    {
        float before = Top(_second);

        Click(State(_first).Header);
        Click(State(_first).Header);

        Assert.That(State(_first).Expanded, Is.True);
        Assert.That(State(_first).Body.Tags.Has<LayoutIgnore>(), Is.False);
        Assert.That(Top(_second), Is.EqualTo(before).Within(0.01f));
    }

    [Test]
    public void HoldingTheHeaderTogglesOnce()
    {
        Entity header = State(_first).Header;
        header.AddTag<Pressed>();
        Frame(3);

        Assert.That(State(_first).Expanded, Is.False);
    }
}
