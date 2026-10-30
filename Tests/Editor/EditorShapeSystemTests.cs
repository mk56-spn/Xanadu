using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Settings;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorShapeSystemTests : EcsSystemTest
{
    [Test]
    public void AttachTurnsTheSelectedBlocksIntoAnOrderedShape()
    {
        Entity readout = Store.CreateEntity(new Text("NO SHAPE"));
        Entity editor = Store.CreateEntity(
            new Playhead(1_000, 120, 0, false),
            new NoteSelection(default),
            new EditorShapeState(0),
            new EditorShapeReadout(readout),
            new EditorNoteTarget(KeybindAction.L2));
        Entity first = Store.CreateEntity(
            new Block(new Vector2(100, 200), new Vector2(80, 40)));
        Entity second = Store.CreateEntity(
            new Block(new Vector2(300, 200), new Vector2(80, 40)));
        editor.GetComponent<NoteSelection>().SetMany(new[] { first, second });
        Entity button = Store.CreateEntity(new EditorShapeAttachButton());
        var history = new EditorHistory(Store);

        Root.Add(new EditorShapeSystem(Store, history));
        Frame();
        button.AddTag<Pressed>();
        Frame();
        button.RemoveTag<Pressed>();
        Frame();

        Block a = first.GetComponent<Block>();
        Block b = second.GetComponent<Block>();
        Assert.That(a.ShapeId, Is.EqualTo(1));
        Assert.That(b.ShapeId, Is.EqualTo(1));
        Assert.That(a.ShapePiece, Is.Zero);
        Assert.That(b.ShapePiece, Is.EqualTo(1));
        Assert.That(first.Tags.Has<ConstructionHidden>(), Is.True);
        Assert.That(second.Tags.Has<ConstructionHidden>(), Is.True);

        Note marker = Store.Query<Note>().ToEntityList()[0].GetComponent<Note>();
        Assert.That(marker.Kind, Is.EqualTo(NoteKind.Construction));
        Assert.That(marker.ShapeId, Is.EqualTo(1));
        Assert.That(marker.Target, Is.EqualTo(KeybindAction.L2));
        Assert.That(editor.GetComponent<EditorShapeState>().ShapeId,
            Is.EqualTo(1));
        Assert.That(readout.GetComponent<Text>().Value, Is.EqualTo("SHAPE 1"));
        Assert.That(history.CanUndo, Is.True);
    }
}
