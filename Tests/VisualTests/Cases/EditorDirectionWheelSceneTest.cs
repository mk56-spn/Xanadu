using Friflo.Engine.ECS;
using Godot;
using Xanadu.Systems.Input;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Editor;
using Xanadu.Scenes;
using Xanadu.Ui.Ecs.Ui;
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
                  Context.Entities.Query<UiWheel>().WithDisabled().Count == 1 &&
                  Context.Entities.Query<EditorDirectionWheel>().Count == 1);
        AddUntilStep(
            "wheel starts hidden outside movement modes",
            () => UiLayout.IsHidden(Wheel()));

        AddStep("open the notes group", () => ClickGroupOf(EditorPlacementMode.Dash));
        AddWaitStep("hold the notes group", 2);
        AddStep("release the notes group", () => Context.Pointer.Release());
        AddUntilStep("the notes flyout opens", () => GroupIsOpen(EditorPlacementMode.Dash));
        AddStep("click DASH placement", () => ClickPlacement(EditorPlacementMode.Dash));
        AddWaitStep("hold DASH", 2);
        AddStep("release DASH", () => Context.Pointer.Release());
        AddUntilStep(
            "wheel appears in DASH mode",
            () => PlaceMode() == EditorPlacementMode.Dash &&
                  !UiLayout.IsHidden(Wheel()));
        AddAssert(
            "the note elements box stays up for dash notes",
            () => !UiLayout.IsHidden(NoteBar()));
        AddAssert(
            "the wheel sits inside the note elements box",
            () => Area(NoteBar()).Encloses(Area(Wheel())));
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

        AddStep("open the notes group", () => ClickGroupOf(EditorPlacementMode.Notes));
        AddWaitStep("hold the notes group", 2);
        AddStep("release the notes group", () => Context.Pointer.Release());
        AddUntilStep("the notes flyout opens", () => GroupIsOpen(EditorPlacementMode.Notes));
        AddStep("click NOTES placement", () => ClickPlacement(EditorPlacementMode.Notes));
        AddWaitStep("hold NOTES", 2);
        AddStep("release NOTES", () => Context.Pointer.Release());
        AddUntilStep(
            "wheel hides again while the swatches stay",
            () => PlaceMode() == EditorPlacementMode.Notes &&
                  UiLayout.IsHidden(Wheel()) &&
                  !UiLayout.IsHidden(NoteBar()));

        AddStep("create editor overlay", CreateOverlay);
        AddWaitStep("overlay settles", 2);
        AddStep("press the dash placement key", () => Press(Key.Key3));
        AddUntilStep(
            "dash key switches to DASH and shows the wheel",
            () => PlaceMode() == EditorPlacementMode.Dash &&
                  !UiLayout.IsHidden(Wheel()));
        AddStep("press the normal placement key", () => Press(Key.Key1));
        AddUntilStep(
            "normal key switches to NOTES and hides the wheel",
            () => PlaceMode() == EditorPlacementMode.Notes &&
                  UiLayout.IsHidden(Wheel()));
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

    private void Press(Key key) => Context.Systems.KeyPresses.Press(new KeyChord(key));

    private static Rect2 Area(Entity entity)
    {
        Rect2 bounds = entity.GetComponent<Rectangle>().Bounds;
        Vector2 center = UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value);
        return new Rect2(center + bounds.Position, bounds.Size);
    }

    private Entity Wheel() =>
        Context.Entities.Query<UiWheel>().WithDisabled().ToEntityList()[0];

    private Entity NoteBar() =>
        Context.Entities.Query<EditorNoteBarPanel>().WithDisabled().ToEntityList()[0];

    private EditorPlacementMode PlaceMode() =>
        Context.Entities.Query<EditorPlacement>()
            .ToEntityList()[0]
            .GetComponent<EditorPlacement>().Mode;

    private DashDirection Direction() =>
        Context.Entities.Query<EditorPlacement>()
            .ToEntityList()[0]
            .GetComponent<EditorPlacement>().Direction;

    private void ClickPlacement(EditorPlacementMode mode)
    {
        foreach (Entity entity in EditorButton.All(Context.Entities, EditorButtonKind.Place))
        {
            if ((EditorPlacementMode)entity.GetComponent<EditorButton>().Arg != mode)
                continue;

            Context.Pointer.Click(UiTransform.WorldPosition(
                entity,
                entity.GetComponent<Position>().Value));
            return;
        }
    }
    /// <summary>Presses the strip button of the group holding <paramref name="mode"/>, to open its flyout.</summary>
    private void ClickGroupOf(EditorPlacementMode mode)
    {
        foreach (Entity entity in Context.Entities.Query<EditorToolGroup>().ToEntityList())
        {
            if (!EditorToolGroups.Holds(EditorToolGroups.All[entity.GetComponent<EditorToolGroup>().Index], mode))
                continue;

            Context.Pointer.Click(UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value));
            return;
        }
    }

    private bool GroupIsOpen(EditorPlacementMode mode)
    {
        foreach (Entity entity in Context.Entities.Query<EditorToolGroup>().ToEntityList())
        {
            EditorToolGroup group = entity.GetComponent<EditorToolGroup>();
            if (EditorToolGroups.Holds(EditorToolGroups.All[group.Index], mode))
                return group.Open && !UiLayout.IsHidden(group.Flyout);
        }

        return false;
    }
}
