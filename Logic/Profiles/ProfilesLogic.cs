using System.Collections.Generic;
using System.Linq;
using Xanadu.Logic.Screens;
using Xanadu.Model.Ecs.Components;
using Xanadu.Storage.Profiles;

namespace Xanadu.Logic.Profiles;

/// <summary>The profile picker's entrances; Select and Edit take the profile's index in <see cref="ProfilesLogic.Profiles"/>.</summary>
public enum ProfilesEntrance
{
    Select,
    Create,
    Edit,
}

/// <summary>Who's playing: the saved profiles, the current one, and picking, making or editing one.</summary>
public sealed class ProfilesLogic(ProfileStore profiles, IScreenNavigator navigator) : IScreenLogic<ProfilesEntrance>
{
    private readonly ProfileStore _profiles = profiles;
    private readonly IScreenNavigator _navigator = navigator;

    public ScreenContract<ProfilesEntrance> Contract { get; } = new ScreenContract<ProfilesEntrance>()
            .Mandatory(ProfilesEntrance.Select, Enumerable.Range(0, profiles.Profiles.Count).ToArray(),
                i => profiles.Profiles[i].Name)
            .Mandatory(ProfilesEntrance.Create)
            .Optional(ProfilesEntrance.Edit);

    public IReadOnlyList<UserProfile> Profiles => _profiles.Profiles;

    public UserProfile? Active => _profiles.ActiveProfile;

    public bool IsActive(UserProfile profile) => profile.Id == _profiles.ActiveProfileId;

    public void Do(ProfilesEntrance entrance, int arg)
    {
        switch (entrance)
        {
            case ProfilesEntrance.Select when Pick(arg):
                _navigator.Go(SceneKind.Main);
                break;
            case ProfilesEntrance.Create:
                _profiles.Create();
                _navigator.Go(SceneKind.ProfileEdit);
                break;
            case ProfilesEntrance.Edit when Pick(arg):
                _navigator.Go(SceneKind.ProfileEdit);
                break;
        }
    }

    private bool Pick(int index) =>
        index >= 0 && index < _profiles.Profiles.Count && _profiles.Select(_profiles.Profiles[index].Id);
}
