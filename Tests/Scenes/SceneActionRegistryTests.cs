using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Scenes;

namespace Xanadu.Tests.Scenes;

[TestFixture]
public sealed class SceneActionRegistryTests
{
    [Test]
    public void DispatchesBySceneAndAction()
    {
        var registry = new SceneActionRegistry();
        var store = new EntityStore();
        Entity entity = store.CreateEntity(new SceneCommand(SceneActionId.Exit));
        bool called = false;

        registry.Register(SceneKind.Editor, SceneActionId.Exit,
            _ => called = true);
        registry.Register(SceneKind.Main, SceneActionId.Exit,
            _ => Assert.Fail("wrong scene handler"));

        bool handled = registry.TryInvoke(
            SceneKind.Editor,
            new SceneActionContext(
                null!,
                store,
                entity,
                entity.GetComponent<SceneCommand>()));

        Assert.That(handled, Is.True);
        Assert.That(called, Is.True);
    }

    [Test]
    public void UnknownCommandIsNotHandled()
    {
        var registry = new SceneActionRegistry();
        var store = new EntityStore();
        Entity entity = store.CreateEntity(new SceneCommand(SceneActionId.Exit));

        Assert.That(
            registry.TryInvoke(
                SceneKind.Results,
                new SceneActionContext(
                    null!,
                    store,
                    entity,
                    entity.GetComponent<SceneCommand>())),
            Is.False);
    }
}
