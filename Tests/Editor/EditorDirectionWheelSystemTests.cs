using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
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
            new Position(UiLayout.Hidden),
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
        _label = Store.CreateEntity(new Position(UiLayout.Hidden));
        _editor = Store.CreateEntity(
            new EditorPlacementModeState(EditorPlacementMode.Notes),
            new EditorDashDirectionState(DashDirection.UpRight),
            new EditorDirectionWheel(
                _wheel,
                _label,
                new Vector2(400, 300)));

        Root.Add(new EditorDirectionWheelSystem());
    }

    [Test]
    public void HiddenOutsideMovementMode()
    {
        Frame();

        Assert.That(_wheel.GetComponent<Position>().Value,
            Is.EqualTo(UiLayout.Hidden));
        Assert.That(_label.GetComponent<Position>().Value,
            Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void MovementModeShowsWheelAndReflectsTheCurrentDirection()
    {
        _editor.GetComponent<EditorPlacementModeState>().Mode =
            EditorPlacementMode.Movement;

        Frame();

        Assert.That(_wheel.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(400, 300)));
        Assert.That(_label.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(400, 300 - 90 - 16)));
        Assert.That(_wheel.GetComponent<UiWheel>().Selected,
            Is.EqualTo(1));
    }

    [Test]
    public void WheelSelectionUpdatesTheDashDirection()
    {
        _editor.GetComponent<EditorPlacementModeState>().Mode =
            EditorPlacementMode.Movement;
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
            EditorPlacementMode.Movement;
        Frame();
        _editor.GetComponent<EditorDashDirectionState>().Direction =
            DashDirection.DownLeft;

        Frame();

        Assert.That(_wheel.GetComponent<UiWheel>().Selected,
            Is.EqualTo(5));
    }
}
