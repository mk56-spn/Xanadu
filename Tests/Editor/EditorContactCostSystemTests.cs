using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorContactCostSystemTests : EcsSystemTest
{
    [Test]
    public void ButtonsStepABarsCostAndItsReadout()
    {
        Entity readout = Store.CreateEntity(new Text(""), new EditorReadout(EditorReadoutKind.ContactSecond));
        Entity editor = Store.CreateEntity(new EditorPlacement { Costs = new BarCosts(1, 0, 0) });
        Entity secondPlus = Store.CreateEntity(EditorButton.ContactCost(1, 1));
        Entity firstMinus = Store.CreateEntity(EditorButton.ContactCost(0, -1));

        Root.Add(new EditorContactCostSystem(Store));
        Frame();

        Click(secondPlus);
        Assert.That(editor.GetComponent<EditorPlacement>().Costs, Is.EqualTo(new BarCosts(1, 1, 0)));
        Assert.That(readout.GetComponent<Text>().Value, Is.EqualTo("BAR 2: 1"));

        Click(firstMinus);
        Assert.That(editor.GetComponent<EditorPlacement>().Costs, Is.EqualTo(new BarCosts(0, 1, 0)));
    }

    [Test]
    public void ABarAsksAtMostFour()
    {
        Entity editor = Store.CreateEntity(new EditorPlacement { Costs = new BarCosts(3, 0, 0) });
        Entity firstPlus = Store.CreateEntity(EditorButton.ContactCost(0, 1));

        Root.Add(new EditorContactCostSystem(Store));
        Frame();

        Click(firstPlus);
        Click(firstPlus);
        Assert.That(editor.GetComponent<EditorPlacement>().Costs.First, Is.EqualTo(BarCosts.MaxCost));
    }

    [Test]
    public void ABarrierAsksOfAtMostTwoBars()
    {
        Entity editor = Store.CreateEntity(new EditorPlacement { Costs = new BarCosts(1, 1, 0) });
        Entity thirdPlus = Store.CreateEntity(EditorButton.ContactCost(2, 1));

        Root.Add(new EditorContactCostSystem(Store));
        Frame();

        Click(thirdPlus);
        Assert.That(editor.GetComponent<EditorPlacement>().Costs, Is.EqualTo(new BarCosts(1, 1, 0)));
    }

    [Test]
    public void TheRhombusButtonTurnsDiagonalOnAndOff()
    {
        Entity readout = Store.CreateEntity(new Text(""), new EditorReadout(EditorReadoutKind.Diagonal));
        Entity editor = Store.CreateEntity(new EditorPlacement());
        Entity toggle = Store.CreateEntity(EditorButton.Of(EditorButtonKind.Diagonal));

        Root.Add(new EditorContactCostSystem(Store));
        Frame();

        Click(toggle);
        Assert.That(editor.GetComponent<EditorPlacement>().Diagonal, Is.True);
        Assert.That(readout.GetComponent<Text>().Value, Is.EqualTo("RHOMBUS: ON"));

        Click(toggle);
        Assert.That(editor.GetComponent<EditorPlacement>().Diagonal, Is.False);
    }

    private void Click(Entity button)
    {
        button.AddTag<Pressed>();
        Frame();
        button.RemoveTag<Pressed>();
        Frame();
    }
}
