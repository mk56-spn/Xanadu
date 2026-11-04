using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class TextFieldDoubleClickTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private Entity _field;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _root.Add(new TextFieldDoubleClickSystem());
        _field = _store.CreateEntity(new TextField("Hello"));
    }

    private void Frame(float seconds = 1f / 60f) => _root.Update(new UpdateTick(seconds, 0));

    private void Press(float gapBefore = 1f / 60f)
    {
        Frame(gapBefore);
        _field.AddTag<Pressed>();
        Frame();
        _field.RemoveTag<Pressed>();
        Frame();
    }

    [Test]
    public void TwoQuickPressesClearTheField()
    {
        Press();
        Assert.That(_field.GetComponent<TextField>().Value, Is.EqualTo("Hello"));
        Press();
        Assert.That(_field.GetComponent<TextField>().Value, Is.Empty);
    }

    [Test]
    public void SlowPressesLeaveTheFieldAlone()
    {
        Press();
        Press(gapBefore: TextFieldDoubleClickSystem.Window + 0.2f);
        Assert.That(_field.GetComponent<TextField>().Value, Is.EqualTo("Hello"));
    }

    [Test]
    public void HoldingThePressDoesNotCountTwice()
    {
        _field.AddTag<Pressed>();
        Frame();
        Frame();
        Frame();
        Assert.That(_field.GetComponent<TextField>().Value, Is.EqualTo("Hello"));
    }

    [Test]
    public void PressesOnTwoDifferentFieldsAreNotADoubleClick()
    {
        Entity other = _store.CreateEntity(new TextField("Other"));
        Press();
        other.AddTag<Pressed>();
        Frame();
        other.RemoveTag<Pressed>();
        Frame();
        Assert.That(other.GetComponent<TextField>().Value, Is.EqualTo("Other"));
        Assert.That(_field.GetComponent<TextField>().Value, Is.EqualTo("Hello"));
    }
}
