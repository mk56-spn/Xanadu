using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;

namespace Xanadu.Tests.VisualTests;

/// <summary>A faint spike under the pointer shows where, and which way, a click would paint.</summary>
public sealed class EditorPlacementGhostSceneTest : SceneTest
{
    public override string Name => "Editor placement ghost";
    public override string Slug => "editor-placement-ghost";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep("wait for the editor", () =>
            Context.Scenes.Current == SceneKind.Editor &&
            Context.Entities.Query<EditorPlacementGhost>().Count == 1);
        AddStep("pick spikes, pointing right", () =>
        {
            Entity editor = Context.Entities.Query<EditorPlacementGhost>().ToEntityList()[0];
            editor.GetComponent<EditorPlacementModeState>().Mode = EditorPlacementMode.Spikes;
            editor.GetComponent<EditorPlacementBrush>().Degrees = 90f;
            Context.Pointer.Position = new Vector2(760, 460);
        });
        AddUntilStep("the ghost shows", () =>
            Context.Entities.Query<EditorPlacementGhost>().ToEntityList()[0]
                .GetComponent<EditorPlacementGhost>().Visible);
    }
}
