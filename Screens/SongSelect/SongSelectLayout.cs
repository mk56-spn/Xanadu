using Friflo.Engine.ECS;
using Xanadu.Logic.SongSelect;
using Xanadu.Model.Ecs.Components;
using Xanadu.Screens.Shared;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.SongSelect;

/// <summary>
/// The classic song select: the staggered level list, preview, player card and
/// scores (<see cref="SongSelectScene"/>). Its list systems keep their own view
/// state in <see cref="SongSelectState"/>; the selection itself is the logic's.
/// </summary>
public sealed class SongSelectLayout : IScreenLayout<SongSelectLogic>
{
    private Entity _view;
    private int _shown = -1;

    public string Name => "CLASSIC";

    public void Build(UiNode layer, SongSelectLogic logic)
    {
        SongSelectState state = SongSelectScene.Build(layer, logic.Catalog, logic.Selected, logic.Profiles);
        layer.Entity.AddComponent(state);
        _view = layer.Entity;
        _shown = logic.Version;
    }

    public void Refresh(SongSelectLogic logic)
    {
        if (_shown == logic.Version || _view.IsNull)
            return;

        _shown = logic.Version;
        ref SongSelectState state = ref _view.GetComponent<SongSelectState>();
        SongSelectScene.Refresh(ref state, logic.Catalog, logic.Selected, logic.Profiles);
    }
}
