using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Systems.Editor;
using Xanadu.Theme;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorDashStrengthSystemTests : EcsSystemTest
{
    [Test]
    public void StrengthButtonsAreExclusiveAndTintTheSelection()
    {
        Entity editor = Store.CreateEntity(
            new EditorDashStrengthState(DashStrength.Single));
        Entity single = Button(DashStrength.Single);
        Entity twice = Button(DashStrength.Double);
        Entity triple = Button(DashStrength.Triple);

        Root.Add(new EditorDashStrengthSystem(Store));

        Frame();
        Assert.That(single.GetComponent<ButtonVisual>().Normal,
            Is.EqualTo(Palette.Primary));

        triple.AddTag<Pressed>();
        Frame();

        Assert.That(
            editor.GetComponent<EditorDashStrengthState>().Strength,
            Is.EqualTo(DashStrength.Triple));
        Assert.That(triple.GetComponent<ButtonVisual>().Normal,
            Is.EqualTo(Palette.Primary));
        Assert.That(twice.GetComponent<ButtonVisual>().Normal,
            Is.EqualTo(Palette.Muted));
    }

    private Entity Button(DashStrength strength)
    {
        Entity button = Store.CreateEntity(
            new EditorDashStrengthButton(strength),
            ButtonVisual.Create(Palette.Muted));
        return button;
    }
}
