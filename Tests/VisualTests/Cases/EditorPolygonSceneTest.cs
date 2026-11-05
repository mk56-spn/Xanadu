using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// The polygon tool in the real editor: a shape drawn click by click and
/// closed on its first point, which opens the XIF editor beside the tool
/// strip, next to turned text and turned grass.
/// </summary>
public sealed class EditorPolygonSceneTest : SceneTest
{
    private static readonly Vector2[] Outline =
    {
        new(560, 360), new(760, 300), new(900, 420), new(820, 600), new(640, 560),
    };

    public override string Name => "Editor polygons";
    public override string Slug => "editor-polygons";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the tools",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorToolGroup>().Count > 0);

        AddStep("put turned text and grass in the level", () =>
        {
            PlayfieldView view = Editor().GetComponent<PlayfieldView>();
            Vector2 At(float x, float y) => (new Vector2(x, y) - view.ScreenOrigin) / view.Scale;
            float unit = 1f / view.Scale;
            EditorItems.Create(Context.Entities, new LevelText(At(1080, 300), "TURNED", 56 * unit, 25f));
            EditorItems.Create(Context.Entities, new GrassPatch(At(1050, 680), 9)
            {
                Width = 300 * unit, Length = 70 * unit, Density = 50 * view.Scale, RotationDegrees = -30f,
            });
        });

        AddStep("pick the polygon tool", () => Click(PlaceButton(EditorPlacementMode.Polygon)));
        AddWaitStep("hold", 2);
        AddStep("release", () => Context.Pointer.Release());
        AddUntilStep("the polygon tool is out", () =>
            Editor().GetComponent<EditorPlacement>().Mode == EditorPlacementMode.Polygon);

        for (int i = 0; i <= Outline.Length; i++)
        {
            Vector2 point = Outline[i % Outline.Length];
            AddStep($"click point {i + 1}", () => Context.Pointer.Click(point));
            AddWaitStep("hold", 2);
            AddStep("release", () => Context.Pointer.Release());
            AddWaitStep("settle", 1);
        }

        AddUntilStep("the outline closed into a shape", () => Context.Entities.Query<LevelPolygon>().Count == 1);
        AddUntilStep("the XIF editor opens for it", () =>
        {
            foreach (Entity panel in Context.Entities.Query<XifEditor>().ToEntityList())
                return !UiLayout.IsHidden(panel);
            return false;
        });
        AddWaitStep("settle", 4);
    }

    private Entity Editor() => Context.Entities.Query<EditorPlacement, PlayfieldView>().ToEntityList()[0];

    private Entity PlaceButton(EditorPlacementMode mode)
    {
        foreach (Entity button in EditorButton.All(Context.Entities, EditorButtonKind.Place))
        {
            if (button.GetComponent<EditorButton>().Arg == (int)mode)
                return button;
        }

        return default;
    }

    private void Click(Entity entity) =>
        Context.Pointer.Click(UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value));
}
