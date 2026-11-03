using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;
using Xanadu.Tests.Support;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorDirectionWheelSystemTests : EcsSystemTest
{
    private Entity _editor;
    private Entity _wheel;
    private Entity _label;

    protected override void OnEcsSetUp()
    {
        _wheel = Store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(-90, -90, 180, 180)),
            new UiWheel
            {
                Selected = 0,
                Hover = -1,
                Labels = new[] { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" },
                HubRadius = 0.3f,
                StartAngle = UiWheelGeometry.DefaultStartAngle,
                Gap = 0.04f,
            });
        _label = Store.CreateEntity(new Position(Vector2.Zero));
        _editor = Store.CreateEntity(
            new EditorPlacementModeState(EditorPlacementMode.Notes),
            new EditorDashDirectionState(DashDirection.UpRight),
            new EditorDirectionWheel(_wheel, _label));

        Root.Add(new EditorDirectionWheelSystem());
    }

    [Test]
    public void HiddenOutsideMovementMode()
    {
        Frame();

        Assert.That(UiLayout.IsHidden(_wheel), Is.True);
        Assert.That(UiLayout.IsHidden(_label), Is.True);
    }

    [Test]
    public void MovementModeShowsWheelAndReflectsTheCurrentDirection()
    {
        _editor.GetComponent<EditorPlacementModeState>().Mode =
            EditorPlacementMode.Dash;

        Frame();

        Assert.That(UiLayout.IsHidden(_wheel), Is.False);
        Assert.That(UiLayout.IsHidden(_label), Is.False);
        Assert.That(_wheel.GetComponent<UiWheel>().Selected,
            Is.EqualTo(1));
    }

    [Test]
    public void TheWheelTakesTheColourOfTheNoteBeingPlaced()
    {
        _editor.GetComponent<EditorPlacementModeState>().Mode = EditorPlacementMode.Dash;
        _editor.AddComponent(new EditorNoteTarget(KeybindAction.L3));
        Root.Update(new UpdateTick(1f / 60f, 0));

        Color color = PlayfieldRenderer.ColorFor(KeybindAction.L3);
        UiWheel wheel = _wheel.GetComponent<UiWheel>();
        Assert.That(wheel.SelectedColor, Is.EqualTo(color));
        Assert.That(wheel.Colors![0], Is.EqualTo(EditorNoteTypeBarSystem.Dimmed(color)));
        Assert.That(_wheel.Tags.Has<Dirty>(), Is.True);

        _editor.GetComponent<EditorNoteTarget>().Target = KeybindAction.R1;
        Root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            _wheel.GetComponent<UiWheel>().SelectedColor,
            Is.EqualTo(PlayfieldRenderer.ColorFor(KeybindAction.R1)));
    }

    [Test]
    public void WheelSelectionUpdatesTheDashDirection()
    {
        _editor.GetComponent<EditorPlacementModeState>().Mode =
            EditorPlacementMode.Dash;
        Frame();
        _wheel.AddComponent(new UiWheelChanged(6));

        Frame();

        Assert.That(
            _editor.GetComponent<EditorDashDirectionState>().Direction,
            Is.EqualTo(DashDirection.Left));
        Assert.That(_wheel.GetComponent<UiWheel>().Selected,
            Is.EqualTo(6));
        Assert.That(_wheel.HasComponent<UiWheelChanged>(), Is.False);
    }

    [Test]
    public void DirectionChangesSelectTheMatchingWedge()
    {
        _editor.GetComponent<EditorPlacementModeState>().Mode =
            EditorPlacementMode.Dash;
        Frame();
        _editor.GetComponent<EditorDashDirectionState>().Direction =
            DashDirection.DownLeft;

        Frame();

        Assert.That(_wheel.GetComponent<UiWheel>().Selected,
            Is.EqualTo(5));
    }
}
