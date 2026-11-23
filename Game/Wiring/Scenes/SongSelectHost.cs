using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Logic.Screens;
using Xanadu.Logic.SongSelect;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Screens.Shared;
using Xanadu.Screens.SongSelect;
using SceneState = Xanadu.Model.Ecs.Components.SceneState;

namespace Xanadu.Wiring.Scenes;

/// <summary>
/// Song select: loads the catalog, makes the logic over it, and does what the
/// logic asks of the game (play, edit, delete). The selection is kept in
/// <see cref="SceneState"/> so it survives leaving the screen.
/// </summary>
public sealed class SongSelectHost(SceneSystem scenes) : ScreenHost<SongSelectLogic, SongSelectEntrance>, ISongSelectActions
{
    private Entity _stateEntity;

    public override SceneKind Kind => SceneKind.SongSelect;
    public override SceneBackgroundKind Background => SceneBackgroundKind.Menu;
    public override SceneKind BackTarget => SceneKind.Main;

    public LevelEntry? SelectedEntry => _stateEntity.IsNull ? null : Logic?.SelectedEntry;

    protected override SongSelectLogic CreateLogic(SceneBuildContext c, IScreenNavigator navigator)
    {
        _stateEntity = c.StateEntity;
        List<LevelEntry> catalog;
        try
        {
            catalog = c.LoadLevels();
        }
        catch (Exception)
        {
            catalog = [];
        }

        var logic = new SongSelectLogic(
            catalog, _stateEntity.GetComponent<SceneState>().SelectedIndex, c.Profiles, navigator, this);
        Selected(logic.Selected, logic.SelectedEntry);
        return logic;
    }

    protected override IScreenLayout<SongSelectLogic>[] Layouts() => [new SongSelectLayout()];

    public override bool TryForward(SceneSystem system)
    {
        if (_stateEntity.IsNull || !Logic.CanPlay)
            return false;

        Logic.Do(SongSelectEntrance.Play, 0);
        return true;
    }

    /// <summary>Moves the selection from the list's own input (keys, drag, scrollbar), only while the screen is settled.</summary>
    public bool TryStep(int direction)
    {
        if (_stateEntity.IsNull)
            return false;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        return state.Current == SceneKind.SongSelect && state.Pending == state.Current && Logic.Step(direction);
    }

    void ISongSelectActions.Play()
    {
        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        if (!string.IsNullOrEmpty(state.SelectedLevelPath))
            scenes.Request(SceneKind.Play);
    }

    void ISongSelectActions.Edit() => scenes.EditSelected();

    void ISongSelectActions.AskDelete() => scenes.AskDeleteSelected();

    public void Selected(int index, LevelEntry? entry)
    {
        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        state.SelectedIndex = index;
        state.SelectedLevelPath = entry?.Path ?? "";
    }
}
