using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Layout;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Timeline;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.UI;

/// <summary>
/// The shared ease picker against a fake host, as the animator and the level
/// editor's group panel host it: shown only while open, placed by its anchor,
/// and handing picks and closing back to the host.
/// </summary>
[TestFixture]
public sealed class EasePickerTests : EcsSystemTest
{
    private static readonly Rect2 Screen = new(0, 0, 1920, 1080);

    private FakeHost _host = null!;
    private Entity _panel;

    private sealed class FakeHost : IEasePickerHost
    {
        public bool Open { get; set; }
        public Rect2 Anchor { get; set; } = new(1500, 900, 100, 28);
        public Rect2 Bounds => Screen;
        public EasePreset? Current { get; set; }
        public string EmptyHint => "Select something";
        public List<EasePreset> Picks { get; } = new();
        public int Closes { get; private set; }

        public void Pick(EasePreset preset)
        {
            Picks.Add(preset);
            Current = preset;
        }

        public void Close()
        {
            Closes++;
            Open = false;
        }
    }

    protected override void OnEcsSetUp()
    {
        _host = new FakeHost();
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new EasePickerSystem());
        UiNode layer = UiNode.Layer(Store, default, 5, new FakeVisualRidFactory());
        _panel = EasePickerPanel.Build(layer, _host);
        Frame(2);
    }

    private EasePickerParts Parts => _panel.GetComponent<EasePicker>().Parts;

    private Entity Cell(EasePreset preset)
    {
        foreach (Entity cell in Store.Query<EasePickerCell>().WithDisabled().ToEntityList())
        {
            if (cell.GetComponent<EasePickerCell>().Preset == preset)
                return cell;
        }

        Assert.Fail($"No cell for {preset}.");
        return default;
    }

    private void Click(Entity entity)
    {
        entity.AddTag<Pressed>();
        Frame();
        entity.RemoveTag<Pressed>();
        Frame();
    }

    [Test]
    public void ItHasACellForEveryPreset()
    {
        foreach (EasePreset preset in System.Enum.GetValues<EasePreset>())
            Assert.That(Cell(preset).IsNull, Is.False);
    }

    [Test]
    public void ItShowsOnlyWhileTheHostHasItOpen()
    {
        Assert.That(_panel.Enabled, Is.False);

        _host.Open = true;
        Frame();
        Assert.That(_panel.Enabled, Is.True);
        Assert.That(Parts.Curve.Enabled, Is.True);

        _host.Open = false;
        Frame();
        Assert.That(_panel.Enabled, Is.False);
        Assert.That(Parts.Dot.Enabled, Is.False);
    }

    [Test]
    public void ItSitsAboveItsAnchorOrBelowWhenThereIsNoRoom()
    {
        Vector2 size = EasePickerPanel.Size;
        var low = new Rect2(1500, 900, 100, 28);
        Vector2 above = EasePickerSystem.Corner(low, Screen, size);
        Assert.That(above.Y + size.Y, Is.LessThanOrEqualTo(low.Position.Y));
        Assert.That(above.X + size.X, Is.EqualTo(low.End.X).Within(0.01f), "right-aligned with its anchor");

        var high = new Rect2(1500, 40, 100, 28);
        Vector2 below = EasePickerSystem.Corner(high, Screen, size);
        Assert.That(below.Y, Is.GreaterThanOrEqualTo(high.End.Y));

        var corner = new Rect2(10, 40, 100, 28);
        Vector2 kept = EasePickerSystem.Corner(corner, Screen, size);
        Assert.That(kept.X, Is.GreaterThanOrEqualTo(0f), "kept on screen");
    }

    [Test]
    public void PressingACellPicksItThroughTheHostAndLightsIt()
    {
        _host.Open = true;
        Frame();

        Click(Cell(EasePreset.ElasticOut));

        Assert.That(_host.Picks, Is.EqualTo(new[] { EasePreset.ElasticOut }));
        Assert.That(_host.Open, Is.True, "the picker stays open");
        Assert.That(Cell(EasePreset.ElasticOut).GetComponent<ButtonVisual>().Normal, Is.EqualTo(EasePickerPanel.Lit));
        Assert.That(Cell(EasePreset.Linear).GetComponent<ButtonVisual>().Normal, Is.EqualTo(EasePickerPanel.Cell));
        Assert.That(Parts.Name.GetComponent<Text>().Value, Is.EqualTo("ELASTIC OUT"));
    }

    [Test]
    public void ACellPressedWhileClosedPicksNothing()
    {
        Click(Cell(EasePreset.Step));

        Assert.That(_host.Picks, Is.Empty);
    }

    [Test]
    public void TheCrossClosesItThroughTheHost()
    {
        _host.Open = true;
        Frame();

        Click(Parts.Close);

        Assert.That(_host.Closes, Is.EqualTo(1));
        Assert.That(_panel.Enabled, Is.False);
    }

    [Test]
    public void WithNothingSelectedTheHintIsTheHosts()
    {
        _host.Open = true;
        Frame();

        Assert.That(Parts.Hint.GetComponent<Text>().Value, Is.EqualTo("Select something"));
    }
}
