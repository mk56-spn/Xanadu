using Xanadu.Logic.Screens;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Logic.MainMenu;

/// <summary>The title screen's ways out.</summary>
public enum MainMenuEntrance
{
    Play,
    Settings,
    Profiles,
    Editor,
    OffsetWizard,
    Bench,
}

/// <summary>The title screen: nothing to show, only where the player can go from it.</summary>
public sealed class MainMenuLogic(IScreenNavigator navigator) : IScreenLogic<MainMenuEntrance>
{
    public ScreenContract<MainMenuEntrance> Contract { get; } = new ScreenContract<MainMenuEntrance>()
        .Mandatory(MainMenuEntrance.Play)
        .Mandatory(MainMenuEntrance.Settings)
        .Mandatory(MainMenuEntrance.Profiles)
        .Optional(MainMenuEntrance.Editor)
        .Optional(MainMenuEntrance.OffsetWizard)
        .Optional(MainMenuEntrance.Bench);

    public void Do(MainMenuEntrance entrance, int arg) => navigator.Go(entrance switch
    {
        MainMenuEntrance.Play => SceneKind.SongSelect,
        MainMenuEntrance.Settings => SceneKind.Settings,
        MainMenuEntrance.Profiles => SceneKind.Profiles,
        MainMenuEntrance.Editor => SceneKind.Editor,
        MainMenuEntrance.OffsetWizard => SceneKind.OffsetWizard,
        _ => SceneKind.Bench,
    });
}
