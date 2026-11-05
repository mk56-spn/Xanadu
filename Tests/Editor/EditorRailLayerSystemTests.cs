using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Settings;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorRailLayerSystemTests : EcsSystemTest
{
    [Test]
    public void RailButtonsPlaceTapsWithTheSelectedHoldAndSide()
    {
        Entity readout = Store.CreateEntity(new Text("TAP"), new EditorReadout(EditorReadoutKind.RailHold));
        Entity editor = Store.CreateEntity(
            new Playhead(1_000, 120, 0, false),
            new EditorPlacement());
        Entity left = Store.CreateEntity(EditorButton.RailTap(KeybindAction.L2));
        Entity hold = Store.CreateEntity(new EditorButton(EditorButtonKind.RailHold));
        Entity end = Store.CreateEntity(EditorButton.ChannelEnd(ChannelSide.Right));
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
        Assert.That(editor.GetComponent<EditorPlacement>().RailHoldMs,
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

    private void Click(Entity button)
    {
        button.AddTag<Pressed>();
        Frame();
        button.RemoveTag<Pressed>();
        Frame();
    }
}
