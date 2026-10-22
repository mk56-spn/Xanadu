using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Editor;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class EditorMetadataSystemTests
{
    [Test]
    public void EditingAStartFieldUpdatesTheSelectedStartPoint()
    {
        var store = new EntityStore();
        var commands = new Writer.Editor.Commands.EditorCommandBus();
        Entity panel = store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(-190, -110, 380, 220)));
        Entity startPanel = store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(-190, -110, 380, 220)));
        Entity start = store.CreateEntity(new StartPoint(
            "Test",
            1_000,
            new Vector2(200, 300),
            System.Numerics.Vector2.Zero,
            new Vector2(0, -1)));
        Entity editor = store.CreateEntity(
            new LevelInfo("Level", ""),
            new Playhead(0, 120, 0, false),
            new NoteSelection(start),
            new EditorMetadataPanel(panel, Vector2.Zero),
            new EditorStartPanel(startPanel, Vector2.Zero));
        Entity field = store.CreateEntity(
            new TextField("", true),
            new Text(""),
            new EditorField(EditorFieldKind.StartTime));

        var root = new SystemRoot(store);
        root.Add(new EditorMetadataSystem(store, commands));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(startPanel.GetComponent<Position>().Value,
            Is.EqualTo(Vector2.Zero));

        field.GetComponent<TextField>().Value = "2500";
        field.AddTag<TextFieldFocused>();
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(start.GetComponent<StartPoint>().TimeMs,
            Is.EqualTo(2_500));
    }
}
