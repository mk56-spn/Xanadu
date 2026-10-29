using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiIconButtonTests
{
    private string? _requested;

    [SetUp]
    public void SetUp()
    {
        _requested = null;
        UiIcons.Loader = path =>
        {
            _requested = path;
            return null;
        };
    }

    [TearDown]
    public void TearDown()
    {
        UiIcons.Loader = path =>
            ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    [Test]
    public void MissingIconShowsTheFallbackLetters()
    {
        var store = new EntityStore();
        UiBuilder ui = UiBuilder.Begin(store, new FakeVisualRidFactory()).Layer(0);

        Entity button = ui.IconButton("AK", "res://Assets/Icons/auto.svg", new Vector2(56, 56));

        Assert.That(_requested, Is.EqualTo("res://Assets/Icons/auto.svg"));
        Assert.That(button.GetComponent<Text>().Value, Is.EqualTo("AK"));
        Assert.That(button.Tags.Has<TakesInput>(), Is.True);
        Assert.That(button.HasComponent<ButtonVisual>(), Is.True);
        Assert.That(button.ChildCount, Is.Zero);
    }

    [Test]
    public void NoIconPathNeverCallsTheLoader()
    {
        var store = new EntityStore();
        UiBuilder ui = UiBuilder.Begin(store, new FakeVisualRidFactory()).Layer(0);

        Entity button = ui.IconButton("IK", null, new Vector2(56, 56));

        Assert.That(_requested, Is.Null);
        Assert.That(button.GetComponent<Text>().Value, Is.EqualTo("IK"));
    }

    [Test]
    public void ButtonLeavesTheBuilderWhereItFound()
    {
        var store = new EntityStore();
        UiBuilder ui = UiBuilder.Begin(store, new FakeVisualRidFactory()).Layer(0);
        Entity before = ui.Current;

        ui.IconButton("AK", null, new Vector2(56, 56));

        Assert.That(ui.Current, Is.EqualTo(before));
    }
}
