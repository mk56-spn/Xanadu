using System.Linq;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Tree;

namespace Xanadu.Tests.UI;

/// <summary>
/// Trees written as initializers (<see cref="UiSpec"/>): they build the same
/// entities the fluent calls do, under the parent they are written under, in order.
/// </summary>
[TestFixture]
public sealed class UiSpecTreeTests
{
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

    private UiNode Layer() => UiNode.Layer(_store, default, 0, _visuals);

    [Test]
    public void ChildrenLandUnderTheSpecTheyAreWrittenIn()
    {
        Label title, note;
        Row header;
        Column page = Layer().Add(new Column
        {
            (header = new Row
            {
                (title = new Label("ATLAS")),
                new Button("BACK"),
            }),
            (note = new Label("types")),
        });

        Assert.That(header.Node.Entity.Parent, Is.EqualTo(page.Node.Entity));
        Assert.That(title.Node.Entity.Parent, Is.EqualTo(header.Node.Entity));
        Assert.That(note.Node.Entity.Parent, Is.EqualTo(page.Node.Entity));
        Assert.That(page.Node.Entity.ChildEntities.ToArray(), Is.EqualTo(new[] { header.Node.Entity, note.Node.Entity }));
        Assert.That(title.Node.Entity.GetComponent<Text>().Value, Is.EqualTo("ATLAS"));
    }

    [Test]
    public void ASequenceGoesInAmongTheChildrenAndANullIsLeftOut()
    {
        bool showBack = false;
        Row row = Layer().Add(new Row
        {
            new Label("first"),
            Enumerable.Range(0, 3).Select(i => new Label($"chip {i}")),
            showBack ? new Button("BACK") : null,
            new Label("last"),
        });

        string[] texts = row.Node.Entity.ChildEntities.ToArray()
            .Select(child => child.GetComponent<Text>().Value).ToArray();
        Assert.That(texts, Is.EqualTo(new[] { "first", "chip 0", "chip 1", "chip 2", "last" }));
    }

    [Test]
    public void ASpecInsideAStackIsOneChildNotASequence()
    {
        Row outer = Layer().Add(new Row
        {
            new Column { new Label("a"), new Label("b") },
            new Label("c"),
        });

        Assert.That(outer.Node.Entity.ChildCount, Is.EqualTo(2));
    }

    [Test]
    public void ConstructorSettingsReachTheBuiltNode()
    {
        Button button = Layer().Add(new Button("GO", cmd: UiSpecs.Cmd(TestCommand.Go, 7), tip: "go there", grow: 2) { Hidden = true });

        Entity entity = button.Node.Entity;
        Assert.That(entity.GetComponent<UiCommand>().Is(TestCommand.Go), Is.True);
        Assert.That(entity.GetComponent<UiCommand>().Arg, Is.EqualTo(7));
        Assert.That(entity.GetComponent<UiTooltip>().Text, Is.EqualTo("go there"));
        Assert.That(entity.GetComponent<UiFlex>().Grow, Is.EqualTo(2));
        Assert.That(entity.Tags.Has<TakesInput>(), Is.True);
        Assert.That(UiLayout.IsHidden(entity), Is.True);
    }

    [Test]
    public void AFlexPresetGivesWayToTheStacksOwnArguments()
    {
        var fill = new Flex(Grow: 1, Cross: Cross.Stretch);
        Column column = Layer().Add(new Column(fill, grow: 3, hidden: true));

        Entity entity = column.Node.Entity;
        Assert.That(entity.GetComponent<UiFlex>().Grow, Is.EqualTo(3));
        Assert.That(entity.GetComponent<UiContainer>().Cross, Is.EqualTo(StackCrossAlignment.Stretch));
        Assert.That(UiLayout.IsHidden(entity), Is.True);
    }

    [Test]
    public void APooledListBuildsEachRowFromItsTemplate()
    {
        var names = new System.Collections.Generic.List<Label>();
        PooledList list = Layer().Add(new PooledList(24, 4)
        {
            Row = _ => [new Box(width: 4), Remember(names, new Label("", fit: true, grow: 1))],
        });

        int pool = UiLists.Pool(list.Node);
        Assert.That(pool, Is.GreaterThan(1));
        Assert.That(names, Has.Count.EqualTo(pool));
        Assert.That(names[0].Node.Entity.Parent, Is.EqualTo(UiLists.Row(list.Node, 0)));
        Assert.That(names[0].Node.Entity.Tags.Has<TextFit>(), Is.True);
    }

    private static T Remember<T>(System.Collections.Generic.List<T> into, T spec)
    {
        into.Add(spec);
        return spec;
    }

    private enum TestCommand
    {
        Go,
    }
}
