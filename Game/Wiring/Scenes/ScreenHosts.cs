using Xanadu.Storage.Settings;
using Xanadu.Logic.Audio;
using Xanadu.Logic.Calibration;
using Xanadu.Logic.MainMenu;
using Xanadu.Logic.Profiles;
using Xanadu.Logic.Results;
using Xanadu.Logic.Screens;
using Xanadu.Logic.Settings;
using Xanadu.Model.Ecs.Components;
using Xanadu.Screens.Calibration;
using Xanadu.Screens.MainMenu;
using Xanadu.Screens.Profiles;
using Xanadu.Screens.Results;
using Xanadu.Screens.Settings;
using Xanadu.Screens.Shared;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Wiring.Scenes;

// One host per player-facing screen: which logic it makes, from what, and its layouts.

public sealed class MainMenuHost : ScreenHost<MainMenuLogic, MainMenuEntrance>
{
    public override SceneKind Kind => SceneKind.Main;
    public override SceneBackgroundKind Background => SceneBackgroundKind.Menu;

    protected override MainMenuLogic CreateLogic(SceneBuildContext c, IScreenNavigator navigator) => new(navigator);
    protected override IScreenLayout<MainMenuLogic>[] Layouts() => [new MainMenuLayout()];

    public override bool TryForward(SceneSystem system)
    {
        Logic.Do(MainMenuEntrance.Play, 0);
        return true;
    }
}

/// <summary>Settings: two layouts over one logic, to prove the split.</summary>
public sealed class SettingsHost : ScreenHost<SettingsLogic, SettingsEntrance>
{
    public override SceneKind Kind => SceneKind.Settings;
    public override SceneKind BackTarget => SceneKind.Main;

    protected override SettingsLogic CreateLogic(SceneBuildContext c, IScreenNavigator navigator) =>
        new(c.Keybinds, c.Display, navigator);

    protected override IScreenLayout<SettingsLogic>[] Layouts() => [new CardSettingsLayout(), new SplitSettingsLayout()];
}

public sealed class ResultsHost(SceneSystem scenes) : ScreenHost<ResultsLogic, ResultsEntrance>, IPlayAgain
{
    public override SceneKind Kind => SceneKind.Results;
    public override SceneKind BackTarget => SceneKind.SongSelect;

    protected override ResultsLogic CreateLogic(SceneBuildContext c, IScreenNavigator navigator) =>
        new(c.Results ?? default, c.CanWatchReplay, navigator, this);

    protected override IScreenLayout<ResultsLogic>[] Layouts() => [new ResultsLayout()];

    public override bool TryForward(SceneSystem system)
    {
        Logic.Do(ResultsEntrance.SongSelect, 0);
        return true;
    }

    void IPlayAgain.Retry() => scenes.RetryPlay();
    void IPlayAgain.WatchLastReplay() => scenes.WatchLastReplay();
}

public sealed class ProfilesHost : ScreenHost<ProfilesLogic, ProfilesEntrance>
{
    public override SceneKind Kind => SceneKind.Profiles;
    public override SceneBackgroundKind Background => SceneBackgroundKind.Menu;

    protected override ProfilesLogic CreateLogic(SceneBuildContext c, IScreenNavigator navigator) => new(c.Profiles, navigator);
    protected override IScreenLayout<ProfilesLogic>[] Layouts() => [new ProfilesLayout()];
}

public sealed class ProfileEditHost : ScreenHost<ProfileEditLogic, ProfileEditEntrance>
{
    public override SceneKind Kind => SceneKind.ProfileEdit;
    public override SceneBackgroundKind Background => SceneBackgroundKind.Menu;
    public override SceneKind BackTarget => SceneKind.Profiles;

    /// <summary>There is nothing to edit without an active profile: back to the picker.</summary>
    protected override bool CanOpen(SceneBuildContext c)
    {
        if (c.Profiles.ActiveProfile is not null)
            return true;

        c.StateEntity.GetComponent<SceneState>().Pending = SceneKind.Profiles;
        return false;
    }

    protected override ProfileEditLogic CreateLogic(SceneBuildContext c, IScreenNavigator navigator) =>
        new(c.Profiles, c.Profiles.ActiveProfile!, navigator);

    protected override IScreenLayout<ProfileEditLogic>[] Layouts() => [new ProfileEditLayout()];
}

/// <summary>Calibration needs its own click track player and the keyboard, so the game root makes it.</summary>
public sealed class OffsetWizardHost(ITrackAudio audio, OffsetSettings settings, IKeyboardState keyboard)
    : ScreenHost<OffsetWizardLogic, OffsetWizardEntrance>
{
    public override SceneKind Kind => SceneKind.OffsetWizard;
    public override SceneKind BackTarget => SceneKind.Main;

    protected override OffsetWizardLogic CreateLogic(SceneBuildContext c, IScreenNavigator navigator) =>
        new(audio, settings, keyboard, navigator);

    protected override IScreenLayout<OffsetWizardLogic>[] Layouts() => [new OffsetWizardLayout()];
}
