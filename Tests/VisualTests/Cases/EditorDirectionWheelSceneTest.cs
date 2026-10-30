using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Scenes;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Regression: dash direction is chosen with the generic Xanadu.UI wheel, which
/// appears only in movement modes and writes the selected wedge back to editor state.
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
                  VisualAssert.HasText(Context.Entities, "MOVE DIRECTION"));
        AddUntilStep(
            "wheel starts hidden outside movement modes",
            () => Wheel().GetComponent<Position>().Value == UiLayout.Hidden);

        AddStep("click DASH placement", () => ClickPlacement(EditorPlacementMode.Dash));
        AddWaitStep("hold DASH", 2);
        AddStep("release DASH", () => Context.Pointer.Release());
        AddUntilStep(
            "wheel appears in DASH mode",
            () => PlaceMode() == EditorPlacementMode.Dash &&
                  Wheel().GetComponent<Position>().Value != UiLayout.Hidden);
        AddAssert(
            "the note key bar hides for any-key dash notes",
            () => NoteBar().GetComponent<Position>().Value == UiLayout.Hidden &&
                  NoteBar().Tags.Has<LayoutIgnore>());
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
        AddStep("press the dash placement key", () => Press(Key.Key3));
        AddUntilStep(
            "dash key switches to DASH and shows the wheel",
            () => PlaceMode() == EditorPlacementMode.Dash &&
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

    private Entity NoteBar() =>
        Context.Entities.Query<EditorNoteBarPanel>().ToEntityList()[0];

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
