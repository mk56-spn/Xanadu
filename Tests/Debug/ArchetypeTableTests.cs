using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Debug;

namespace Xanadu.Tests.Debug;

[TestFixture]
public sealed class ArchetypeTableTests
{
    private EntityStore _store = null!;
    private ArchetypeTable _table = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _table = new ArchetypeTable();
    }

    [Test]
    public void RowsKeepCreationOrderWhileCountsChange()
    {
        AddRect();
        AddDirtyRect();
        AddDirtyRect();
        AddMovedRect();

        _table.Refresh(_store, 0);
        Archetype[] before = _table.Rows.Select(row => row.Archetype).ToArray();

        // If the table sorted by count this archetype would jump to the front.
        AddRect();
        AddRect();
        _table.Refresh(_store, 100);

        Assert.That(_table.Rows.Select(row => row.Archetype), Is.EqualTo(before));
        Assert.That(_table.Rows.Select(row => row.Count), Is.EqualTo(new[] { 3, 2, 1 }));
        Assert.That(_table.Rows.Select(row => row.Order), Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void CountsUpdateBeforeTheSampleWindow()
    {
        AddRect();
        _table.Refresh(_store, 0);
        Assert.That(_table.Rows[0].Count, Is.EqualTo(1));
        Assert.That(_table.Rows[0].Delta, Is.Zero);

        AddRect();
        AddRect();
        _table.Refresh(_store, 100);

        Assert.That(_table.Rows[0].Count, Is.EqualTo(3));
        Assert.That(
            _table.Rows[0].Delta,
            Is.Zero,
            "deltas are sampled on the interval, not every frame");
    }

    [Test]
    public void DeltaIsSampledOnTheInterval()
    {
        AddRect();
        _table.Refresh(_store, 0);

        AddRect();
        AddRect();
        _table.Refresh(_store, 600);

        Assert.That(_table.Rows[0].Delta, Is.EqualTo(2));
        Assert.That(_table.Rows[0].Growth, Is.EqualTo(1));
    }

    [Test]
    public void GrowthStreakResetsWhenARowStopsGrowing()
    {
        AddRect();
        _table.Refresh(_store, 0);

        AddRect();
        _table.Refresh(_store, 600);
        AddRect();
        _table.Refresh(_store, 1200);
        Assert.That(_table.Rows[0].Growth, Is.EqualTo(2));

        _table.Refresh(_store, 1800);
        Assert.That(_table.Rows[0].Delta, Is.Zero);
        Assert.That(_table.Rows[0].Growth, Is.Zero);
    }

    [Test]
    public void SignaturesAreBuiltOncePerArchetype()
    {
        AddRect();
        _table.Refresh(_store, 0);
        ArchetypeRow first = _table.Rows[0];
        string components = first.Components;
        string tags = first.Tags;

        // A new archetype forces the table to add a row and re-sort the view.
        AddDirtyRect();
        _table.Refresh(_store, 600);

        ArchetypeRow again = _table.Rows.Single(row => row.Order == first.Order);
        Assert.That(again.Components, Is.SameAs(components));
        Assert.That(again.Tags, Is.SameAs(tags));
    }

    [Test]
    public void SortByCountReordersWithoutLosingTheSelection()
    {
        AddRect();
        AddDirtyRect();
        AddDirtyRect();
        AddDirtyRect();
        _table.Refresh(_store, 0);

        _table.MoveSelection(1);   // first move selects the first row
        _table.MoveSelection(1);   // then step to the larger archetype
        ArchetypeRow selected = _table.Selected!;

        _table.CycleSort();

        Assert.That(_table.Sort, Is.EqualTo(ArchetypeSortOrder.Count));
        Assert.That(_table.Rows[0].Archetype, Is.SameAs(selected.Archetype));
        Assert.That(_table.Selected, Is.SameAs(selected));
        Assert.That(_table.SelectedIndex, Is.Zero);
    }

    [Test]
    public void SortCyclesBackToCreationOrder()
    {
        AddRect();
        AddDirtyRect();
        _table.Refresh(_store, 0);

        _table.CycleSort();
        _table.CycleSort();
        _table.CycleSort();

        Assert.That(_table.Sort, Is.EqualTo(ArchetypeSortOrder.Creation));
        Assert.That(_table.Rows.Select(row => row.Order), Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void SelectionPullsThePage()
    {
        _table.PageSize = 2;
        AddRect();
        AddDirtyRect();
        AddMovedRect();
        AddPressedRect();
        AddDirtyMovedRect();
        _table.Refresh(_store, 0);

        Assert.That(_table.Rows, Has.Count.EqualTo(5));
        Assert.That(_table.PageCount, Is.EqualTo(3));

        _table.MoveSelection(1);
        _table.MoveSelection(3);

        Assert.That(_table.SelectedIndex, Is.EqualTo(3));
        Assert.That(_table.Page, Is.EqualTo(1));
    }

    [Test]
    public void SelectionClampsAtTheEnds()
    {
        AddRect();
        AddDirtyRect();
        _table.Refresh(_store, 0);

        _table.MoveSelection(1);
        Assert.That(_table.SelectedIndex, Is.Zero);

        _table.MoveSelection(-1);
        Assert.That(_table.SelectedIndex, Is.Zero);

        _table.MoveSelection(5);
        Assert.That(_table.SelectedIndex, Is.EqualTo(1));
    }

    [Test]
    public void ReserveIsReportedFromCapacityAndComponentSizes()
    {
        AddRect();
        _table.Refresh(_store, 0);

        ArchetypeRow row = _table.Rows[0];
        Assert.That(row.BytesPerEntity, Is.GreaterThan(0));
        Assert.That(row.Capacity, Is.GreaterThanOrEqualTo(row.Count));
        Assert.That(row.ReservedBytes, Is.EqualTo(row.BytesPerEntity * row.Capacity));
        Assert.That(row.Used, Is.InRange(0f, 1f));
    }

    [Test]
    public void EntitySampleListsMembersOfTheSelection()
    {
        Entity first = AddRect();
        Entity second = AddRect();
        _table.Refresh(_store, 0);
        _table.MoveSelection(1);

        var ids = new List<int>();
        _table.FillEntitySample(ids, 4);

        Assert.That(ids, Is.EquivalentTo(new[] { first.Id, second.Id }));
    }

    [Test]
    public void EmptyTableHasOnePageAndNoSelection()
    {
        _table.Refresh(_store, 0);

        Assert.That(_table.Rows, Is.Empty);
        Assert.That(_table.PageCount, Is.EqualTo(1));
        Assert.That(_table.Selected, Is.Null);
        Assert.That(_table.SelectedIndex, Is.EqualTo(-1));

        _table.MoveSelection(1);
        Assert.That(_table.Selected, Is.Null);
    }

    private Entity AddRect() =>
        _store.CreateEntity(new Rectangle(new Rect2(0, 0, 1, 1)));

    private Entity AddDirtyRect() =>
        _store.CreateEntity(new Rectangle(new Rect2(0, 0, 1, 1)), Tags.Get<Dirty>());

    private Entity AddMovedRect() =>
        _store.CreateEntity(new Rectangle(new Rect2(0, 0, 1, 1)), Tags.Get<Moved>());

    private Entity AddPressedRect() =>
        _store.CreateEntity(new Rectangle(new Rect2(0, 0, 1, 1)), Tags.Get<Pressed>());

    private Entity AddDirtyMovedRect() =>
        _store.CreateEntity(new Rectangle(new Rect2(0, 0, 1, 1)), Tags.Get<Dirty, Moved>());
}
