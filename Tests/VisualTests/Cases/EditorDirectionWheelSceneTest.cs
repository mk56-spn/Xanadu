using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Scenes;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Regression: dash direction is chosen with the generic Xanadu.UI wheel, which
/// appears only in MOVE mode and writes the selected wedge back to editor state.
/// </summary>
public sealed class EditorDirectionWheelSceneTest : SceneTest
{
    private EditorOverlay? _overlay;

    public override string Name => "Editor dash wheel";
    public override string Slug => "editor-dash-wheel";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the generic wheel",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<UiWheel>().Count == 1 &&
                  Context.Entities.Query<EditorDirectionWheel>().Count == 1 &&
                  VisualAssert.HasText(Context.Entities, "DASH DIRECTION"));
        AddUntilStep(
            "wheel starts hidden outside MOVE mode",
            () => Wheel().GetComponent<Position>().Value == UiLayout.Hidden);

        AddStep("click MOVE placement", () => ClickPlacement(EditorPlacementMode.Movement));
        AddWaitStep("hold MOVE", 2);
        AddStep("release MOVE", () => Context.Pointer.Release());
        AddUntilStep(
            "wheel appears in MOVE mode",
            () => PlaceMode() == EditorPlacementMode.Movement &&
                  Wheel().GetComponent<Position>().Value != UiLayout.Hidden);
        AddAssert(
            "current direction selected",
            () => Wheel().GetComponent<UiWheel>().Selected ==
                  DashDirectionWheel.IndexOf(Direction()));

        AddStep("click the left wedge", () =>
        {
            Entity wheel = Wheel();
            Vector2 center = UiTransform.WorldPosition(
                wheel,
                wheel.GetComponent<Position>().Value);
            float radius = wheel.GetComponent<Rectangle>().Bounds.Size.X / 2;
            Context.Pointer.Click(center + new Vector2(-radius * 0.6f, 0));
        });
        AddWaitStep("hold the left wedge", 2);
        AddStep("release the left wedge", () => Context.Pointer.Release());
        AddUntilStep(
            "left wedge writes the dash direction",
            () => Direction() == DashDirection.Left &&
                  Wheel().GetComponent<UiWheel>().Selected ==
                  DashDirectionWheel.IndexOf(DashDirection.Left));

        AddStep("click NOTES placement", () => ClickPlacement(EditorPlacementMode.Notes));
        AddWaitStep("hold NOTES", 2);
        AddStep("release NOTES", () => Context.Pointer.Release());
        AddUntilStep(
            "wheel hides again",
            () => PlaceMode() == EditorPlacementMode.Notes &&
                  Wheel().GetComponent<Position>().Value == UiLayout.Hidden);

        AddStep("create editor overlay", CreateOverlay);
        AddWaitStep("overlay settles", 2);
        AddStep("press the dash placement key", () => Press(Key.Key2));
        AddUntilStep(
            "dash key switches to MOVE and shows the wheel",
            () => PlaceMode() == EditorPlacementMode.Movement &&
                  Wheel().GetComponent<Position>().Value != UiLayout.Hidden);
        AddStep("press the normal placement key", () => Press(Key.Key1));
        AddUntilStep(
            "normal key switches to NOTES and hides the wheel",
            () => PlaceMode() == EditorPlacementMode.Notes &&
                  Wheel().GetComponent<Position>().Value == UiLayout.Hidden);
        AddStep("close editor overlay", () => _overlay?.Close());
    }

    private void CreateOverlay()
    {
        _overlay = new EditorOverlay(
            Context.Entities,
            Context.Systems.Commands,
            Context.Pointer,
            openSongDialog: false);
        Context.Host.AddChild(_overlay);
    }

    private void Press(Key key)
    {
        _overlay?._UnhandledInput(new InputEventKey
        {
            Pressed = true,
            Keycode = key,
            PhysicalKeycode = key,
        });
    }

    private Entity Wheel() =>
        Context.Entities.Query<UiWheel>().ToEntityList()[0];

    private EditorPlacementMode PlaceMode() =>
        Context.Entities.Query<EditorPlacementModeState>()
            .ToEntityList()[0]
            .GetComponent<EditorPlacementModeState>().Mode;

    private DashDirection Direction() =>
        Context.Entities.Query<EditorDashDirectionState>()
            .ToEntityList()[0]
            .GetComponent<EditorDashDirectionState>().Direction;

    private void ClickPlacement(EditorPlacementMode mode)
    {
        foreach (Entity entity in Context.Entities
                     .Query<EditorPlacementButton>()
                     .ToEntityList())
        {
            if (entity.GetComponent<EditorPlacementButton>().Mode != mode)
                continue;

            Context.Pointer.Click(UiTransform.WorldPosition(
                entity,
                entity.GetComponent<Position>().Value));
            return;
        }
    }
}
