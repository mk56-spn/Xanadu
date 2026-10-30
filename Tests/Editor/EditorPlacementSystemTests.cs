using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Systems.Editor;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorPlacementSystemTests : EcsSystemTest
{
    [Test]
    public void PlacementModesAreExclusive()
    {
        Entity editor = Store.CreateEntity(
            new EditorPlacementModeState(EditorPlacementMode.Notes));
        Entity mineButton = Store.CreateEntity(
            new EditorPlacementButton(EditorPlacementMode.Mines));
        Entity moveButton = Store.CreateEntity(
            new EditorPlacementButton(EditorPlacementMode.Dash));

        Root.Add(new EditorPlacementSystem(Store));

        mineButton.AddTag<Pressed>();
        Frame();
        Assert.That(
            editor.GetComponent<EditorPlacementModeState>().Mode,
            Is.EqualTo(EditorPlacementMode.Mines));

        mineButton.RemoveTag<Pressed>();
        moveButton.AddTag<Pressed>();
        Frame();
        Assert.That(
            editor.GetComponent<EditorPlacementModeState>().Mode,
            Is.EqualTo(EditorPlacementMode.Dash));
    }
}
