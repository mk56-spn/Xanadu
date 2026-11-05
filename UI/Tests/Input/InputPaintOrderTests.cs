using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

/// <summary>Deleted ids are recycled, so a later entity can have a lower id than an earlier one.</summary>
[TestFixture]
public sealed class InputPaintOrderTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private Pointer _pointer = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _pointer = new Pointer();
        _root = new SystemRoot(_store) { new InputSystem(_pointer) };
    }

    private Entity Hit(int z = 0)
    {
        Entity entity = _store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(-50, -50, 100, 100)),
            new Drawable(z),
            Tags.Get<TakesInput>());
        return entity;
    }

    /// <summary>Frees a run of ids and returns one entity built on a recycled, lower id.</summary>
    private Entity[] Recycled(int count)
    {
        Entity[] junk = Enumerable.Range(0, 200).Select(_ => _store.CreateEntity()).ToArray();
        Entity high = Hit();
        foreach (Entity entity in junk)
            entity.DeleteEntity();

        var result = new Entity[count + 1];
        result[0] = high;
        for (int i = 1; i <= count; i++)
            result[i] = Hit();
        return result;
    }

    private void Press()
    {
        _pointer.Current = new PointerInput(Vector2.Zero, true);
        _root.Update(new UpdateTick(1f / 60f, 0));
    }

    [Test]
    public void AChildTakesThePressFromItsParentWhateverTheirIds()
    {
        Entity[] made = Recycled(1);
        Assert.That(made[1].Id, Is.LessThan(made[0].Id), "the child needs the lower id");
        made[0].AddChild(made[1]);

        Press();

        Assert.That(made[1].Tags.Has<Pressed>(), Is.True);
        Assert.That(made[0].Tags.Has<Pressed>(), Is.False);
    }

    [Test]
    public void ALaterSiblingTakesThePressWhateverTheirIds()
    {
        Entity parent = _store.CreateEntity();
        Entity[] made = Recycled(1);
        parent.AddChild(made[0]);
        parent.AddChild(made[1]);
        UiLayout.MoveChild(parent, made[0], 0);

        Press();

        Assert.That(made[1].Tags.Has<Pressed>(), Is.True, "added last, painted last");
    }

    [Test]
    public void AHigherLayerBeatsTheTree()
    {
        Entity parent = Hit(z: 5);
        Entity child = Hit(z: 1);
        parent.AddChild(child);

        Press();

        Assert.That(parent.Tags.Has<Pressed>(), Is.True);
    }

    private sealed class Pointer : IPointerInputSource
    {
        public PointerInput Current;
        public PointerInput Read() => Current;
    }
}
