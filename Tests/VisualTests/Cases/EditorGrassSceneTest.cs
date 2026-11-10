using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Grass in the real editor: patches of different lengths, densities and
/// colours, one with no colour variation, the selected one boxed, and the
/// grass tool's controls lengthening it through a real click.
/// </summary>
public sealed class EditorGrassSceneTest : SceneTest
{
    private Entity _selected;

    public override string Name => "Editor grass";
    public override string Slug => "editor-grass";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the sidebar",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorGrassPanel>().Count == 1);
        AddEditorSong();

        AddStep("grow grass in the level", () =>
        {
            Entity editor = Editor();
            PlayfieldView view = editor.GetComponent<PlayfieldView>();
            Vector2 At(float x, float y) => (new Vector2(x, y) - view.ScreenOrigin) / view.Scale;
            float unit = 1f / view.Scale;

            EditorItems.Create(Context.Entities, new GrassPatch(At(560, 330), 1)
            {
                Width = 420 * unit, Length = 70 * unit, Density = 30 * view.Scale,
            });
            EditorItems.Create(Context.Entities, new GrassPatch(At(1060, 330), 2)
            {
                Width = 420 * unit, Length = 110 * unit, Density = 60 * view.Scale, Sway = 30,
                Color = new Color("#c8e05a"), Variation = 0.6f,
            });
            EditorItems.Create(Context.Entities, new GrassPatch(At(560, 560), 3)
            {
                Width = 420 * unit, Length = 45 * unit, Density = 90 * view.Scale, Variation = 0f,
                Color = new Color("#2e8b57"),
            });
            _selected = EditorItems.Create(Context.Entities, new GrassPatch(At(1060, 560), 4)
            {
                Width = 420 * unit, Length = 60 * unit, Density = 45 * view.Scale,
                Color = new Color("#7fd6a0"),
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
        AddStep("pick the grass tool", () =>
        {
            foreach (Entity button in EditorButton.All(Context.Entities, EditorButtonKind.Place))
            {
                if (button.GetComponent<EditorButton>().Arg == (int)EditorPlacementMode.Grass)
                    Click(button);
            }
        });
        AddWaitStep("hold the tool", 2);
        AddStep("release the tool", () => Context.Pointer.Release());
        AddUntilStep("the grass controls show", () =>
            Editor().GetComponent<EditorGrassPanel>().Section.Enabled);

        float before = 0f;
        AddStep("click LENGTH +", () =>
        {
            before = _selected.GetComponent<GrassPatch>().Length;
            Click(StepButton(GrassSetting.Length, 1));
        });
        AddWaitStep("hold +", 2);
        AddStep("release +", () => Context.Pointer.Release());
        AddUntilStep("the grass grew", () =>
            _selected.GetComponent<GrassPatch>().Length > before);
        AddWaitStep("let it sway", 20);
    }

    private Entity StepButton(GrassSetting setting, int sign)
    {
        int arg = EditorGrassPanelSystem.StepArg(setting, sign);
        foreach (Entity button in EditorButton.All(Context.Entities, EditorButtonKind.GrassStep))
        {
            if (button.GetComponent<EditorButton>().Arg == arg)
                return button;
        }

        return default;
    }

    private void Click(Entity entity) =>
        Context.Pointer.Click(UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value));

    private Entity Editor() => Context.Entities.Query<EditorGrassPanel>().ToEntityList()[0];
}
