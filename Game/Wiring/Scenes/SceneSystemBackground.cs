using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Screens.Shared;
using SceneState = Xanadu.Model.Ecs.Components.SceneState;

namespace Xanadu.Wiring.Scenes;

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    private SceneBackgroundKind BackgroundOf(SceneKind scene) =>
        _controllers.Get(scene)?.Background ?? SceneBackgroundKind.None;

    private bool SharesBackground(SceneKind current, SceneKind next)
    {
        SceneBackgroundKind background = BackgroundOf(current);
        return background != SceneBackgroundKind.None && background == BackgroundOf(next);
    }

    /// <summary>Keeps the background when the next scene wants the same one, else replaces it.</summary>
    private void SyncBackground(SceneKind next)
    {
        SceneBackgroundKind wanted = BackgroundOf(next);
        SceneBackgroundKind shown = _background.IsNull
            ? SceneBackgroundKind.None
            : _background.GetComponent<SceneBackground>().Kind;
        if (wanted == shown)
            return;

        if (!_background.IsNull)
        {
            UiLayout.DeleteSubtree(_background);
            _background = default;
        }

        if (wanted != SceneBackgroundKind.None)
            _background = SceneBackgrounds.Build(_entities, wanted, _visuals);
    }
}
