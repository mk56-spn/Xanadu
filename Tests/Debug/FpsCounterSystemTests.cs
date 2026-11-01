using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Input;
using Xanadu.Systems.Layout;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Debug;

[TestFixture]
public sealed class FpsCounterSystemTests : EcsSystemTest
{
    private ScriptedToggle _toggle = null!;
    private ScriptedFps _fps = null!;

    protected override void OnEcsSetUp()
    {
        _toggle = new ScriptedToggle();
        _fps = new ScriptedFps { Value = 240 };
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new FpsCounterSystem(_toggle, Store, _fps, new FakeVisualRidFactory()));
    }

    private Entity Label => Store.Query<TextGlow>().Entities.Single();

    private void Toggle()
    {
        _toggle.Pressed = true;
        Frame();
        _toggle.Pressed = false;
        Frame();
    }

    [TestCase(1000, 240)]
    [TestCase(240, 240)]
    public void AtTheTargetOrAboveItIsPureGreen(double fps, double same) =>
        Assert.That(DebugPalette.Fps(fps), Is.EqualTo(DebugPalette.Fps(same)));

    [TestCase(60)]
    [TestCase(59)]
    [TestCase(1)]
    public void AtSixtyOrBelowItIsPureRed(double fps) =>
        Assert.That(DebugPalette.Fps(fps), Is.EqualTo(DebugPalette.FpsRed));

    [Test]
    public void ItPassesThroughYellowThenOrangeOnTheWayDown()
    {
        Assert.That(DebugPalette.Fps(240), Is.EqualTo(DebugPalette.FpsGreen));
        Assert.That(DebugPalette.Fps(180), Is.EqualTo(DebugPalette.FpsYellow));
        Assert.That(DebugPalette.Fps(120), Is.EqualTo(DebugPalette.FpsOrange));
    }

    [Test]
    public void JustUnderTheTargetIsAlreadyTurning()
    {
        Assert.That(DebugPalette.Fps(239), Is.Not.EqualTo(DebugPalette.FpsGreen));
        Assert.That(DebugPalette.Fps(61), Is.Not.EqualTo(DebugPalette.FpsRed));
    }

    [Test]
    public void BetweenStopsItBlends()
    {
        Color middle = DebugPalette.Fps(210);
        Color expected = DebugPalette.FpsYellow.Lerp(DebugPalette.FpsGreen, 0.5f);

        Assert.That(middle.IsEqualApprox(expected), Is.True);
    }

    [Test]
    public void TheToggleShowsAndRemovesIt()
    {
        Frame();
        Assert.That(Store.Query<TextGlow>().Count, Is.EqualTo(0), "off until asked for");

        Toggle();
        Assert.That(Label.GetComponent<Text>().Value, Is.EqualTo("240 FPS"));

        Toggle();
        Assert.That(Store.Query<TextGlow>().Count, Is.EqualTo(0));
        Assert.That(Store.Query().WithDisabled().AllTags(Tags.Get<DebugUi>()).Count, Is.EqualTo(0));
    }

    [Test]
    public void TheTextAndGlowFollowTheFrameRate()
    {
        Toggle();
        _fps.Value = 45;
        Frame();

        Assert.That(Label.GetComponent<Text>().Value, Is.EqualTo("45 FPS"));
        Assert.That(Label.GetComponent<TextStyle>().Color, Is.EqualTo(DebugPalette.FpsRed));
        Assert.That(Label.GetComponent<TextGlow>().Color.R, Is.EqualTo(DebugPalette.FpsRed.R));
        Assert.That(Label.GetComponent<TextGlow>().Color.A, Is.LessThan(1f), "the glow stays subtle");
    }

    [Test]
    public void TheBoxSitsInTheTopRightAndHoldsAnyReading()
    {
        Toggle();
        Frame();
        Entity box = Label.Parent;
        Rect2 local = box.GetComponent<Rectangle>().Bounds;
        Rect2 area = new(box.GetComponent<Position>().Value + local.Position, local.Size);

        Assert.That(area.End.X, Is.EqualTo(UiStage.Full.Size.X - 16).Within(0.5f));
        Assert.That(area.Position.Y, Is.EqualTo(16).Within(0.5f));
        Assert.That(box.Tags.Has<TakesInput>(), Is.False, "it does not block clicks");
        Assert.That(local.Size.X, Is.GreaterThanOrEqualTo(UiNode.Measure("9999 FPS", 22).X));
    }

    private sealed class ScriptedToggle : IOverlayToggleSource
    {
        public bool Pressed;
        public bool IsTogglePressed() => Pressed;
    }

    private sealed class ScriptedFps : IFpsSource
    {
        public double Value;
        public double GetFps() => Value;
    }
}
