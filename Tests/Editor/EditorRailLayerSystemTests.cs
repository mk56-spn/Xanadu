using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Settings;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorRailLayerSystemTests : EcsSystemTest
{
    [Test]
    public void RailButtonsPlaceTapsWithTheSelectedHoldAndSide()
    {
        Entity readout = Store.CreateEntity(new Text("TAP"));
        Entity editor = Store.CreateEntity(
            new Playhead(1_000, 120, 0, false),
            new EditorRailHoldState(0),
            new EditorRailHoldReadout(readout));
        Entity left = Button(new EditorRailButton(KeybindAction.L2));
        Entity hold = Button(new EditorRailHoldButton());
        Entity end = Button(new EditorChannelEndButton(ChannelSide.Right));
        var history = new EditorHistory(Store);

        Root.Add(new EditorRailLayerSystem(Store, history));
        Frame();

        Click(left);
        Note first = Store.Query<Note>().ToEntityList()[0].GetComponent<Note>();
        Assert.That(first.Kind, Is.EqualTo(NoteKind.Rail));
        Assert.That(first.ChannelSide, Is.EqualTo(ChannelSide.Left));
        Assert.That(first.PairIndex, Is.EqualTo(PairIndex.Two));
        Assert.That(first.HoldMs, Is.Zero);

        Click(hold);
        Assert.That(editor.GetComponent<EditorRailHoldState>().HoldMs,
            Is.EqualTo(250));
        Assert.That(readout.GetComponent<Text>().Value,
            Is.EqualTo("HOLD 250"));

        Click(left);
        Assert.That(
            Store.Query<Note>().ToEntityList()[1].GetComponent<Note>().HoldMs,
            Is.EqualTo(250));

        Click(end);
        Note endNote = Store.Query<Note>().ToEntityList()[2].GetComponent<Note>();
        Assert.That(endNote.Kind, Is.EqualTo(NoteKind.ChannelEnd));
        Assert.That(endNote.ChannelSide, Is.EqualTo(ChannelSide.Right));
        Assert.That(history.CanUndo, Is.True);
    }

    private Entity Button<T>(T component) where T : struct, IComponent =>
        Store.CreateEntity(component);

    private void Click(Entity button)
    {
        button.AddTag<Pressed>();
        Frame();
        button.RemoveTag<Pressed>();
        Frame();
    }
}
