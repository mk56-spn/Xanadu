using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class NotePopSystemTests : EcsSystemTest
{
    [Test]
    public void PopAgesAndIsRemoved()
    {
        Entity pop = Store.CreateEntity(
            new NotePop(
                new Note(0, Vector2.Zero),
                new Vector2(10, 20),
                Colors.Cyan,
                36f,
                lifetime: 0.1f));
        Root.Add(new NotePopSystem());

        Frame(1, 0.05f);
        Assert.That(pop.GetComponent<NotePop>().Age, Is.EqualTo(0.05f).Within(0.001f));
        Assert.That(pop.IsNull, Is.False);

        Frame(1, 0.06f);
        Assert.That(Store.Query<NotePop>().Count, Is.Zero);
    }
}
