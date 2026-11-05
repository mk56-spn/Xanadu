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
            new EditorPlacement { Mode = EditorPlacementMode.Notes });
        Entity mineButton = Store.CreateEntity(
            EditorButton.Place(EditorPlacementMode.Mines));
        Entity moveButton = Store.CreateEntity(
            EditorButton.Place(EditorPlacementMode.Dash));

        Root.Add(new EditorPlacementSystem(Store));

        mineButton.AddTag<Pressed>();
        Frame();
        Assert.That(
            editor.GetComponent<EditorPlacement>().Mode,
            Is.EqualTo(EditorPlacementMode.Mines));

        mineButton.RemoveTag<Pressed>();
        moveButton.AddTag<Pressed>();
        Frame();
        Assert.That(
            editor.GetComponent<EditorPlacement>().Mode,
            Is.EqualTo(EditorPlacementMode.Dash));
    }
}
