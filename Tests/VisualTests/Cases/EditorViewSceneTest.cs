using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// The editor's view of a 5000-unit field: it opens at the usual scale on the
/// field's bottom middle, zooms out about the pointer, and past the field's
/// edges the grid carries on, fainter, instead of stopping dead.
/// </summary>
public sealed class EditorViewSceneTest : SceneTest
{
    private float _openScale;

    public override string Name => "Editor view and grid";
    public override string Slug => "editor-view";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the editor",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorViewNavigation>().Count == 1);
        AddAssert("a new level's field is 5000 units", () =>
            Editor().GetComponent<Playfield>() is { Width: 5000, Height: 5000 });
        AddStep("remember the opening scale", () => _openScale = Editor().GetComponent<PlayfieldView>().Scale);
        AddAssert("it opens at a readable scale", () => _openScale > 0.5f);
        AddStep("point at the middle of the field area", () =>
            Context.Pointer.Position = EditorViewport.Area(Editor().GetComponent<PlayfieldView>().ScreenSize).GetCenter());
        AddStep("zoom out ten notches", () => Editor().GetComponent<EditorViewNavigation>().ZoomSteps = -10);
        AddWaitStep("let the view settle", 3);
        AddAssert("the view zoomed out", () => Editor().GetComponent<PlayfieldView>().Scale < _openScale * 0.5f);
        AddStep("move the pointer away", () => Context.Pointer.Position = new Vector2(20, 20));
        AddWaitStep("let the tooltip go", 3);
    }

    private Entity Editor() => Context.Entities.Query<EditorViewNavigation>().Entities.First();
}
