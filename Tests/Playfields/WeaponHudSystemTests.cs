using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class WeaponHudSystemTests : EcsSystemTest
{
    [Test]
    public void ShowsTheEquippedWeaponOnlyWhileArmed()
    {
        Entity label = Store.CreateEntity(new Text(""));
        Entity player = Store.CreateEntity(
            new PlayerCharacter(CharacterKind.Armed),
            new ArmedWeapon(WeaponKind.Axe),
            new WeaponHud(label));

        Root.Add(new WeaponHudSystem());
        Frame();

        Assert.That(label.GetComponent<Text>().Value, Is.EqualTo("AXE"));

        player.GetComponent<ArmedWeapon>().Kind = WeaponKind.Spear;
        Frame();
        Assert.That(label.GetComponent<Text>().Value, Is.EqualTo("SPEAR"));

        player.GetComponent<PlayerCharacter>().Kind = CharacterKind.Contact;
        Frame();
        Assert.That(label.GetComponent<Text>().Value, Is.Empty);
    }
}
