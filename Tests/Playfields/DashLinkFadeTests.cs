using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class DashLinkFadeTests : EcsSystemTest
{
    [Test]
    public void TheExitOutlastsTheConnector() =>
        Assert.That(DashLinkFade.ExitLifetime, Is.GreaterThan(DashLinkFade.ConnectorLifetime));

    [Test]
    public void TheFadeStaysUntilTheExitIsGone()
    {
        Entity fade = Store.CreateEntity(new DashLinkFade(Vector2.Zero, new Vector2(250, 0), Colors.Cyan, 36f));
        Root.Add(new DashLinkFadeSystem());

        Frame(1, DashLinkFade.ConnectorLifetime + 0.01f);
        Assert.That(fade.IsNull, Is.False);
        Assert.That(Store.Query<DashLinkFade>().Count, Is.EqualTo(1));

        Frame(1, DashLinkFade.ExitLifetime - DashLinkFade.ConnectorLifetime);
        Assert.That(Store.Query<DashLinkFade>().Count, Is.Zero);
    }

    [Test]
    public void AHitDashPopsWithSparksAndLeavesItsLinkToFade()
    {
        var view = new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000));
        var note = new Note(0, new Vector2(100, 100), kind: NoteKind.Dash, dashDirection: DashDirection.Right);
        Entity noteEntity = Store.CreateEntity(note);

        HitEffects.SpawnDash(Store, noteEntity, note, view);

        Assert.That(Store.Query<NotePop>().ToEntityList()[0].GetComponent<NotePop>().Position,
            Is.EqualTo(new Vector2(100, 100)));
        Assert.That(Store.Query<ParticleBurst>().ToEntityList()[0].GetComponent<ParticleBurst>().Position,
            Is.EqualTo(new Vector2(100, 100)));
        DashLinkFade fade = Store.Query<DashLinkFade>().ToEntityList()[0].GetComponent<DashLinkFade>();
        Assert.That(fade.From, Is.EqualTo(new Vector2(100, 100)));
        Assert.That(fade.To, Is.EqualTo(new Vector2(350, 100)));
    }
}
