using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorMetadataSystemTests
{
    [Test]
    public void BackgroundColourUpdatesTheSwatchAndHexLabel()
    {
        var store = new EntityStore();
        var commands = new Xanadu.Editor.Commands.EditorCommandBus();
        store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(-190, -110, 380, 220)),
            new EditorPanel(EditorPanelKind.Metadata, Vector2.Zero));
        Entity swatch = store.CreateEntity(
            new FillColor(Colors.White),
            new EditorColorRow(EditorColorTarget.Background, hex: false));
        Entity hex = store.CreateEntity(
            new Text("DEFAULT"),
            new EditorColorRow(EditorColorTarget.Background, hex: true));
        Entity editor = store.CreateEntity(
            new LevelInfo("Level") { Background = new Color("#204060") },
            new Playhead(0, 120, 0, false));

        var root = new SystemRoot(store);
        root.Add(new EditorMetadataSystem(store, commands));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            swatch.GetComponent<FillColor>().Value,
            Is.EqualTo(new Color("#204060")));
        Assert.That(hex.GetComponent<Text>().Value, Is.EqualTo("#204060"));
    }

    [Test]
    public void EditingAStartFieldUpdatesTheSelectedStartPoint()
    {
        var store = new EntityStore();
        var commands = new Xanadu.Editor.Commands.EditorCommandBus();
        store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(-190, -110, 380, 220)),
            new EditorPanel(EditorPanelKind.Metadata, Vector2.Zero));
        Entity startPanel = store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(-190, -110, 380, 220)),
            new EditorPanel(EditorPanelKind.Start, Vector2.Zero));
        Entity start = store.CreateEntity(new StartPoint(
            "Test",
            1_000,
            new Vector2(200, 300),
            System.Numerics.Vector2.Zero,
            new Vector2(0, -1)));
        Entity editor = store.CreateEntity(
            new LevelInfo("Level"),
            new Playhead(0, 120, 0, false),
            new NoteSelection(start));
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
