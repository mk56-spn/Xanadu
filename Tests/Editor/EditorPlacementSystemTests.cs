using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class EditorPlacementSystemTests
{
    [Test]
    public void PlacementModesAndDirectionsAreExclusive()
    {
        var store = new EntityStore();
        Entity editor = store.CreateEntity(
            new EditorPlacementModeState(EditorPlacementMode.Notes),
            new EditorDashDirectionState(DashDirection.Up));
        Entity mineButton = store.CreateEntity(
            new EditorPlacementButton(EditorPlacementMode.Mines));
        Entity moveButton = store.CreateEntity(
            new EditorPlacementButton(EditorPlacementMode.Movement));
        Entity directionButton = store.CreateEntity(
            new EditorDashDirectionButton(DashDirection.DownLeft));

        var root = new SystemRoot(store);
        root.Add(new EditorPlacementSystem(store));

        mineButton.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(
            editor.GetComponent<EditorPlacementModeState>().Mode,
            Is.EqualTo(EditorPlacementMode.Mines));

        mineButton.RemoveTag<Pressed>();
        directionButton.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(
            editor.GetComponent<EditorDashDirectionState>().Direction,
            Is.EqualTo(DashDirection.DownLeft));

        directionButton.RemoveTag<Pressed>();
        moveButton.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(
            editor.GetComponent<EditorPlacementModeState>().Mode,
            Is.EqualTo(EditorPlacementMode.Movement));
    }
}
