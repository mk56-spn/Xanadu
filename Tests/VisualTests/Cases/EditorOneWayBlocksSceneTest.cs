using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// One-way blocks in the real editor: see-through with a bar along the one side
/// that collides, next to a solid block for comparison.
/// </summary>
public sealed class EditorOneWayBlocksSceneTest : SceneTest
{
    public override string Name => "Editor one-way blocks";
    public override string Slug => "editor-one-way-blocks";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the playfield",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<PlayfieldView, PlayfieldMode>().Count > 0);

        AddStep("place one block of each kind", () =>
        {
            PlayfieldView view = Context.Entities.Query<PlayfieldView, PlayfieldMode>()
                .ToEntityList()[0].GetComponent<PlayfieldView>();
            var size = new Vector2(120, 80) / view.Scale;
            BlockOneWay[] sides =
            {
                BlockOneWay.Off, BlockOneWay.Up, BlockOneWay.Right, BlockOneWay.Down, BlockOneWay.Left,
            };
            for (int i = 0; i < sides.Length; i++)
            {
                Vector2 screen = new(560 + i * 150, 480);
                EditorItems.Create(
                    Context.Entities,
                    new Block((screen - view.ScreenOrigin) / view.Scale, size, oneWay: sides[i]));
            }
        });
        AddAssert("all five are there", () => Context.Entities.Query<Block>().Count == 5);
    }
}
