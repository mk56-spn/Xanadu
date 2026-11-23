using Xanadu.Core.Tweens;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Ui.Tests.Input;

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
        Color normal = Palette.Muted;
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
        Color normal = Palette.Muted;
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
        // Button colours play on the tween clock, so it moves on with each frame.
        for (int i = 0; i < count; i++)
        {
            ClipClocks.SetSeconds(ClipClocks.Seconds + 1f / 60f);
            Frame();
        }
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
