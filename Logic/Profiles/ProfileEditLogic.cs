using Xanadu.Core.Maths;
using Xanadu.Logic.Screens;
using Xanadu.Model.Ecs.Components;
using Xanadu.Storage.Profiles;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Logic.Profiles;

/// <summary>
/// The profile editor's entrances. <see cref="Name"/> and <see cref="Tint"/> are
/// inputs, not presses: a layout marks the control that feeds each with its
/// entrance and passes the value through <see cref="ProfileEditLogic.Rename"/>
/// and <see cref="ProfileEditLogic.SetTint"/>.
/// </summary>
public enum ProfileEditEntrance
{
    Done,
    Name,
    Tint,
}

/// <summary>
/// Edits the active profile's name and note tint. Names apply as typed; a drag
/// changes the tint every frame, so it reaches disk once it holds still or the
/// screen closes.
/// </summary>
public sealed class ProfileEditLogic(ProfileStore profiles, UserProfile profile, IScreenNavigator navigator)
    : IScreenLogic<ProfileEditEntrance>
{
    private bool _tintChanged;
    private bool _tintUnsaved;

    public ScreenContract<ProfileEditEntrance> Contract { get; } = new ScreenContract<ProfileEditEntrance>()
        .Mandatory(ProfileEditEntrance.Done)
        .Mandatory(ProfileEditEntrance.Name)
        .Optional(ProfileEditEntrance.Tint);

    public UserProfile Profile => profile;

    /// <summary>The saved name: what a blank, overlong or taken name settles back to.</summary>
    public string Name => profile.Name;

    public Color Tint => ProfileTint.Of(profile);

    public void Rename(string name) => profiles.Rename(profile.Id, name);

    public void SetTint(Color tint)
    {
        string hex = SelfModulate.Format(new Color(tint));
        if (hex == profile.NoteTint)
            return;

        profile.NoteTint = hex;
        _tintChanged = true;
    }

    public void Do(ProfileEditEntrance entrance, int arg)
    {
        if (entrance == ProfileEditEntrance.Done)
            navigator.Go(SceneKind.Profiles);
    }

    public void Update()
    {
        if (_tintUnsaved && !_tintChanged)
        {
            profiles.Save();
            _tintUnsaved = false;
        }

        _tintUnsaved |= _tintChanged;
        _tintChanged = false;
    }

    public void Close()
    {
        if (_tintUnsaved || _tintChanged)
            profiles.Save();
    }
}
