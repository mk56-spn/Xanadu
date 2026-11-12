using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Flares in the real editor: a one-axis streak like an anamorphic lens
/// flare, a two-axis star, a turned one and a hot one, the selected one boxed;
/// the decorations flyout open on the flare tool, whose controls thicken the
/// selected flare's streaks through a real click.
/// </summary>
public sealed class EditorFlareSceneTest : SceneTest
{
    private Entity _selected;

    public override string Name => "Editor flares";
    public override string Slug => "editor-flares";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the sidebar",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorFlarePanel>().Count == 1);
        AddEditorSong();

        AddStep("light the level", () =>
        {
            Entity editor = Editor();
            PlayfieldView view = editor.GetComponent<PlayfieldView>();
            Vector2 At(float x, float y) => (new Vector2(x, y) - view.ScreenOrigin) / view.Scale;
            float unit = 1f / view.Scale;

            EditorItems.Create(Context.Entities, new LevelFlare(At(640, 340))
            {
                Width = 560 * unit, Height = 280 * unit,
            });
            EditorItems.Create(Context.Entities, new LevelFlare(At(1180, 340))
            {
                Width = 320 * unit, Height = 320 * unit, Axes = 2, Thickness = 0.03f,
                Color = new Color("#7cc4ff"), Halo = 0.7f, Glow = 0.25f, Heat = 0.8f,
            });
            EditorItems.Create(Context.Entities, new LevelFlare(At(640, 650))
            {
                Width = 420 * unit, Height = 200 * unit, RotationDegrees = 30f,
                Color = new Color("#ff5fa2"), Falloff = 3f, Core = 0.25f,
            });
            _selected = EditorItems.Create(Context.Entities, new LevelFlare(At(1180, 650))
            {
                Width = 480 * unit, Height = 240 * unit, Intensity = 2f,
                Color = new Color("#b6ff7a"), Glow = 0.5f,
            });
            editor.GetComponent<NoteSelection>().Note = _selected;
        });
        AddStep("open the decorations", () =>
        {
            foreach (Entity button in Context.Entities.Query<EditorToolGroup>().ToEntityList())
            {
                if (EditorToolGroups.All[button.GetComponent<EditorToolGroup>().Index].Name == "Decorations")
                    Click(button);
            }
        });
        AddWaitStep("hold the group", 2);
        AddStep("release the group", () => Context.Pointer.Release());
        AddStep("pick the flare tool", () =>
        {
            foreach (Entity button in EditorButton.All(Context.Entities, EditorButtonKind.Place))
            {
                if (button.GetComponent<UiCommand>().Arg == (int)EditorPlacementMode.Flare)
                    Click(button);
            }
        });
        AddWaitStep("hold the tool", 2);
        AddStep("release the tool", () => Context.Pointer.Release());
        AddUntilStep("the flare controls show", () =>
            Editor().GetComponent<EditorFlarePanel>().Section.Enabled);

        float before = 0f;
        AddStep("click THICKNESS +", () =>
        {
            before = _selected.GetComponent<LevelFlare>().Thickness;
            Click(StepButton(FlareSetting.Thickness, 1));
        });
        AddWaitStep("hold +", 2);
        AddStep("release +", () => Context.Pointer.Release());
        AddUntilStep("the streaks thickened", () =>
            _selected.GetComponent<LevelFlare>().Thickness > before);
        AddWaitStep("settle", 10);
    }

    private Entity StepButton(FlareSetting setting, int sign)
    {
        int arg = EditorFlarePanelSystem.StepArg(setting, sign);
        foreach (Entity button in EditorButton.All(Context.Entities, EditorButtonKind.FlareStep))
        {
            if (button.GetComponent<UiCommand>().Arg == arg)
                return button;
        }

        return default;
    }

    private void Click(Entity entity) =>
        Context.Pointer.Click(UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value));

    private Entity Editor() => Context.Entities.Query<EditorFlarePanel>().ToEntityList()[0];
}
