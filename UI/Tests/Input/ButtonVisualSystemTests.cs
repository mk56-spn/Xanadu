using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Systems.Input;

namespace Xanadu.Tests.UI.Input;

[TestFixture]
public sealed class ButtonVisualSystemTests : EcsSystemTest
{

    protected override void OnEcsSetUp()
    {
        Root.Add(new ButtonVisualSystem());
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
        return Store.CreateEntity(
            new FillColor(color),
            ButtonVisual.Create(color));
    }

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
            Frame();
    }

    private static void AssertColor(Entity entity, Color expected)
    {
        Color actual = entity.GetComponent<FillColor>().Value;
        Assert.That(actual.R, Is.EqualTo(expected.R).Within(0.001f));
        Assert.That(actual.G, Is.EqualTo(expected.G).Within(0.001f));
        Assert.That(actual.B, Is.EqualTo(expected.B).Within(0.001f));
        Assert.That(actual.A, Is.EqualTo(expected.A).Within(0.001f));
    }
}
