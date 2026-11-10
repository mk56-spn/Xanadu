using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Blocks and spikes with their default outline, a slightly darker white than
/// their white body, beside a spike with a thick outline of its own; selecting
/// a spike shows the outline controls.
/// </summary>
public sealed class PlayfieldOutlinesSceneTest : SceneTest
{
    private Entity _spike;

    public override string Name => "Block and spike outlines";
    public override string Slug => "playfield-outlines";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the sidebar",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorBlockOutlinePanel>().Count == 1);
        AddStep("place blocks and spikes", () =>
        {
            Entity editor = Editor();
            PlayfieldView view = editor.GetComponent<PlayfieldView>();
            Vector2 At(float x, float y) => (new Vector2(x, y) - view.ScreenOrigin) / view.Scale;
            var tile = new Vector2(90, 90) / view.Scale;

            EditorItems.Create(Context.Entities, new Block(At(500, 300), new Vector2(360, 90) / view.Scale));
            EditorItems.Create(Context.Entities, new Block(At(500, 390), new Vector2(90, 180) / view.Scale));
            float[] angles = { 0f, 90f, 180f, 45f };
            for (int i = 0; i < angles.Length; i++)
                EditorItems.Create(Context.Entities, new Spike(At(950 + i * 120, 300), tile, angles[i]));
            _spike = EditorItems.Create(
                Context.Entities,
                new Spike(At(950, 480), tile * 1.6f, 0f, 10f, new Color("#ff4060")));
            editor.GetComponent<NoteSelection>().Note = _spike;
        });
        AddUntilStep("the outline controls show for a spike", () =>
            Editor().GetComponent<EditorBlockOutlinePanel>().Section.Enabled);
        AddWaitStep("let the editor redraw", 3);
    }

    private Entity Editor() => Context.Entities.Query<EditorBlockOutlinePanel>().ToEntityList()[0];
}
