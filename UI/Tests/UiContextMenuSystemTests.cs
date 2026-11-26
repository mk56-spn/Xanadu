using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Core;
using Xanadu.Tests.Support.Fakes;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Ui.Tests;

/// <summary>Right-click menus: open on a node with entries or anything under it, pick, confirm, close.</summary>
[TestFixture]
public sealed class UiContextMenuSystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private UiContextMenuSystem _menu = null!;
    private readonly Pointer _pointer = new();
    private readonly Keyboard _keyboard = new();
    private Entity _card, _thumb;
    private readonly List<int> _picked = [];

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        UiNode layer = UiNode.Layer(_store, default, 0, new FakeVisualRidFactory());
        UiNode card = layer.Column().Menu(new MenuItem("RENAME"), new MenuItem("DELETE", Confirm: true, "DELETE glow?"));
        _card = card.Entity;
        _thumb = card.Button("thumb").Entity;
        _card.OnMenuPicked(_picked.Add);
        _root = new SystemRoot(_store);
        _menu = new UiContextMenuSystem(_store, _pointer, _keyboard, new FakeVisualRidFactory(), new Vector2(1920, 1080));
        _root.Add(_menu);
        _picked.Clear();
        _keyboard.Escape = false;
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    /// <summary>A right press on <paramref name="on"/>, as the input system marks it, then let go.</summary>
    private void RightClick(Entity on)
    {
        _pointer.Input = new PointerInput(new Vector2(400, 300), false, SecondaryPressed: true);
        if (!on.IsNull)
            on.AddTag<SecondaryPressed>();
        Frame();
        if (!on.IsNull)
            on.RemoveTag<SecondaryPressed>();
        _pointer.Input = new PointerInput(new Vector2(400, 300), false);
        Frame();
    }

    /// <summary>A left press on <paramref name="on"/> (null: on nothing), then let go.</summary>
    private void Click(Entity on)
    {
        _pointer.Input = new PointerInput(new Vector2(400, 300), true);
        if (!on.IsNull)
            on.AddTag<Pressed>();
        Frame();
        if (!on.IsNull)
            on.RemoveTag<Pressed>();
        _pointer.Input = new PointerInput(new Vector2(400, 300), false);
        Frame();
    }

    private Entity Entry(int i) => _menu.Panel.ChildEntities[i];

    private string Label(int i) => Entry(i).GetComponent<Text>().Value;

    [Test]
    public void ARightClickOnAnythingUnderTheNodeOpensItsEntries()
    {
        RightClick(_thumb);

        Assert.That(_menu.IsOpen, Is.True);
        Assert.That(Label(0), Is.EqualTo("RENAME"));
        Assert.That(Label(1), Is.EqualTo("DELETE"));
    }

    [Test]
    public void PickingAnEntrySignalsTheNodeAndCloses()
    {
        RightClick(_thumb);
        Click(Entry(0));

        Assert.That(_picked, Is.EqualTo(new[] { 0 }));
        Assert.That(_menu.IsOpen, Is.False);
    }

    [Test]
    public void AConfirmingEntryAsksFirstAndOnlyConfirmPicks()
    {
        RightClick(_card);
        Click(Entry(1));
        Assert.That(_picked, Is.Empty);
        Assert.That(Label(0), Is.EqualTo("DELETE glow?"));

        Click(Entry(2));
        Assert.That(_picked, Is.Empty);
        Assert.That(_menu.IsOpen, Is.False);

        RightClick(_card);
        Click(Entry(1));
        Click(Entry(1));
        Assert.That(_picked, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void APressElsewhereOrEscapeClosesWithoutPicking()
    {
        RightClick(_thumb);
        Click(default);
        Assert.That(_menu.IsOpen, Is.False);

        RightClick(_thumb);
        _keyboard.Escape = true;
        Frame();
        Assert.That(_menu.IsOpen, Is.False);
        Assert.That(_picked, Is.Empty);
    }

    [Test]
    public void ARightClickOnANodeWithoutAMenuOpensNothing()
    {
        RightClick(UiNode.Layer(_store, default, 0, new FakeVisualRidFactory()).Button("plain").Entity);

        Assert.That(_menu.IsOpen, Is.False);
    }

    private sealed class Pointer : IPointerInputSource
    {
        public PointerInput Input;

        public PointerInput Read() => Input;
    }

    private sealed class Keyboard : IKeyboardState
    {
        public bool Escape;

        public bool IsPressed(Key key) => key == Key.Escape && Escape;
    }
}
