using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Core;
using Xanadu.Tests.Support.Fakes;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Ui.Tests;

[TestFixture]
public sealed class UiTabbedPickerTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private Entity _picker;
    private (int Tab, int Entry)? _heard;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        UiNode layer = UiNode.Layer(_store, default, 0, new FakeVisualRidFactory());
        _picker = layer.TabbedPicker(
        [
            new PickerTab("GAME", ["NOTE", "ORB", "PAD"]),
            new PickerTab("LAB", ["SIZES", "TRAILS"]),
        ], 240).Entity;
        _picker.OnPicked((tab, entry) => _heard = (tab, entry));
        _root = new SystemRoot(_store);
        _root.Add(new UiTabbedPickerSystem());
        _heard = null;
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private void Click(Entity button)
    {
        button.AddTag<Pressed>();
        Frame();
        button.RemoveTag<Pressed>();
        Frame();
    }

    private static string TextOf(Entity entity) => entity.GetComponent<Text>().Value;

    private Entity Tab(int i) => UiTabbedPickers.TabsOf(_picker).ChildEntities[i];

    [Test]
    public void ItStartsClosedReadingNone()
    {
        Frame();

        Assert.That(UiTabbedPickers.IsOpen(_picker), Is.False);
        Assert.That(TextOf(UiTabbedPickers.ButtonOf(_picker)), Is.EqualTo("NONE ▼"));
    }

    [Test]
    public void ATabListsItsEntriesAndAnEntryPicksAndCloses()
    {
        Click(UiTabbedPickers.ButtonOf(_picker));
        Assert.That(UiTabbedPickers.IsOpen(_picker), Is.True);
        Assert.That(TextOf(UiTabbedPickers.EntryOf(_picker, 2)), Is.EqualTo("PAD"));

        Click(Tab(1));
        Assert.That(_picker.GetComponent<UiTabbedPicker>().Tab, Is.EqualTo(1));
        Assert.That(UiTabbedPickers.ListOf(_picker).GetComponent<UiList>().Count, Is.EqualTo(2));
        Assert.That(TextOf(UiTabbedPickers.EntryOf(_picker, 1)), Is.EqualTo("TRAILS"));

        Click(UiTabbedPickers.EntryOf(_picker, 1));
        Assert.That(_heard, Is.EqualTo((1, 1)));
        Assert.That(UiTabbedPickers.IsOpen(_picker), Is.False);
        Assert.That(TextOf(UiTabbedPickers.ButtonOf(_picker)), Is.EqualTo("TRAILS ▼"));
    }

    [Test]
    public void ItOpensOnThePicksTabAndCodeSetsPickSilently()
    {
        UiTabbedPickers.Show(_picker, 1, 0);
        Click(UiTabbedPickers.ButtonOf(_picker));

        Assert.That(_picker.GetComponent<UiTabbedPicker>().Tab, Is.EqualTo(1));
        Assert.That(TextOf(UiTabbedPickers.ButtonOf(_picker)), Is.EqualTo("SIZES ▼"));
        Assert.That(_heard, Is.Null);
    }
}
