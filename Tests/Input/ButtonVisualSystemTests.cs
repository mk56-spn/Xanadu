using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Input;

namespace Writer.Tests.Input;

[TestFixture]
public sealed class ButtonVisualSystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store) { new ButtonVisualSystem() };
    }

    [Test]
    public void HoverFadesToHoverColorAndBack()
    {
        Color normal = new("#334155");
        Entity button = AddButton(normal);
        Color hover = button.GetComponent<ButtonVisual>().Hover;

        button.AddTag<Hovered>();
        Frames(30);
        AssertColor(button, hover);

        button.RemoveTag<Hovered>();
        Frames(30);
        AssertColor(button, normal);
    }

    [Test]
    public void PressFlashesAndThenFadesBack()
    {
        Color normal = new("#334155");
        Entity button = AddButton(normal);

        button.AddTag<Pressed>();
        Frame();
        Assert.That(button.GetComponent<FillColor>().Value.R,
            Is.GreaterThan(normal.R));

        button.RemoveTag<Pressed>();
        Frames(30);
        AssertColor(button, normal);
    }

    private Entity AddButton(Color color)
    {
        return _store.CreateEntity(
            new FillColor(color),
            ButtonVisual.Create(color));
    }

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
            Frame();
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private static void AssertColor(Entity entity, Color expected)
    {
        Color actual = entity.GetComponent<FillColor>().Value;
        Assert.That(actual.R, Is.EqualTo(expected.R).Within(0.001f));
        Assert.That(actual.G, Is.EqualTo(expected.G).Within(0.001f));
        Assert.That(actual.B, Is.EqualTo(expected.B).Within(0.001f));
        Assert.That(actual.A, Is.EqualTo(expected.A).Within(0.001f));
    }
}
