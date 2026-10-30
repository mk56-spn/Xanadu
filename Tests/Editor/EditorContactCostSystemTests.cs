using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorContactCostSystemTests : EcsSystemTest
{
    [Test]
    public void ButtonsChangeTheLeftAndRightCostAndTheReadout()
    {
        Entity readout = Store.CreateEntity(new Text("L1  R1"));
        Entity editor = Store.CreateEntity(
            new EditorContactCostState(1, 1),
            new EditorContactCostReadout(readout));
        Entity leftPlus = Button(left: true, delta: 1);
        Entity rightMinus = Button(left: false, delta: -1);

        Root.Add(new EditorContactCostSystem(Store));
        Frame();

        Click(leftPlus);
        Assert.That(editor.GetComponent<EditorContactCostState>().Left, Is.EqualTo(2));
        Assert.That(readout.GetComponent<Text>().Value, Is.EqualTo("L2  R1"));

        Click(rightMinus);
        Assert.That(editor.GetComponent<EditorContactCostState>().Right, Is.Zero);
        Assert.That(readout.GetComponent<Text>().Value, Is.EqualTo("L2  R0"));
    }

    [Test]
    public void CostsAreClampedToTheAuthoringRange()
    {
        Entity readout = Store.CreateEntity(new Text("L1  R1"));
        Entity editor = Store.CreateEntity(
            new EditorContactCostState(1, 1),
            new EditorContactCostReadout(readout));
        Entity leftMinus = Button(left: true, delta: -1);
        Entity rightPlus = Button(left: false, delta: 1);

        Root.Add(new EditorContactCostSystem(Store));
        Frame();

        Click(leftMinus);
        Assert.That(editor.GetComponent<EditorContactCostState>().Left, Is.Zero);

        Click(rightPlus);
        Click(rightPlus);
        Click(rightPlus);
        Assert.That(editor.GetComponent<EditorContactCostState>().Right,
            Is.EqualTo(3));
    }

    private Entity Button(bool left, int delta) =>
        Store.CreateEntity(new EditorContactCostButton(left, delta));

    private void Click(Entity button)
    {
        button.AddTag<Pressed>();
        Frame();
        button.RemoveTag<Pressed>();
        Frame();
    }
}
