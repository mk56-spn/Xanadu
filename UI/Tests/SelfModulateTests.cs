using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Tests;

[TestFixture]
public sealed class SelfModulateTests
{
    private static readonly Color Tint = new("#80ff4020");

    [TestCase("", true)]
    [TestCase("#ffffff", true)]
    [TestCase("80ff4020", true)]
    [TestCase("#ff", false)]
    [TestCase("nothex", false)]
    public void TypedColoursAreChecked(string typed, bool valid) =>
        Assert.That(SelfModulate.TryParse(typed, out _), Is.EqualTo(valid));

    [Test]
    public void PutSetsAndClearsTheColour()
    {
        Entity item = new EntityStore().CreateEntity();
        SelfModulate.Put(item, Tint);
        Assert.That(SelfModulate.Of(item), Is.EqualTo(Tint));
        SelfModulate.Put(item, Colors.White);
        Assert.That(SelfModulate.Of(item), Is.EqualTo(Colors.White));
    }
}
