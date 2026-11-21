using NUnit.Framework;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class NoteReachTests
{
    private static PlayfieldView View(float scale) =>
        new(Vector2.Zero, scale, new Vector2(1_000, 1_000));

    [Test]
    public void NoteInsideTheSphereIsInReach()
    {
        Assert.That(NoteReach.Contains(new Vector2(500, 500), View(1f), new Vector2(599, 500)), Is.True);
        Assert.That(NoteReach.Contains(new Vector2(500, 500), View(1f), new Vector2(500, 401)), Is.True);
    }

}
