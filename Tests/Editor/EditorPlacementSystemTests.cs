using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Writer.Tests.Support;
using Writer.Ecs;
using Writer.Systems.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class EditorPlacementSystemTests : EcsSystemTest
{
    [Test]
    public void PlacementModesAndDirectionsAreExclusive()
    {
        Entity editor = Store.CreateEntity(
            new EditorPlacementModeState(EditorPlacementMode.Notes),
            new EditorDashDirectionState(DashDirection.Up));
        Entity mineButton = Store.CreateEntity(
            new EditorPlacementButton(EditorPlacementMode.Mines));
        Entity moveButton = Store.CreateEntity(
            new EditorPlacementButton(EditorPlacementMode.Movement));
        Entity directionButton = Store.CreateEntity(
            new EditorDashDirectionButton(DashDirection.DownLeft));

        Root.Add(new EditorPlacementSystem(Store));

        mineButton.AddTag<Pressed>();
        Frame();
        Assert.That(
            editor.GetComponent<EditorPlacementModeState>().Mode,
            Is.EqualTo(EditorPlacementMode.Mines));

        mineButton.RemoveTag<Pressed>();
        directionButton.AddTag<Pressed>();
        Frame();
        Assert.That(
            editor.GetComponent<EditorDashDirectionState>().Direction,
            Is.EqualTo(DashDirection.DownLeft));

        directionButton.RemoveTag<Pressed>();
        moveButton.AddTag<Pressed>();
        Frame();
        Assert.That(
            editor.GetComponent<EditorPlacementModeState>().Mode,
            Is.EqualTo(EditorPlacementMode.Movement));
    }
}
