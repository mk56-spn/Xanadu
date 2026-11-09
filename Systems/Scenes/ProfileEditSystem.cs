using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Profiles;
using Xanadu.Scenes;

namespace Xanadu.Systems.Scenes;

/// <summary>
/// Writes the edit screen's name and tint to the active profile as they change,
/// so leaving by any route keeps them. A drag changes the tint every frame, so
/// the tint reaches disk once it holds still or the screen closes.
/// </summary>
public sealed class ProfileEditSystem(ProfileStore profiles) : QuerySystem<ProfileEditView>
{
    private bool _tintUnsaved;

    protected override void OnUpdate()
    {
        bool tintChanged = false;
        foreach (Entity entity in Query.ToEntityList())
            tintChanged |= Update(entity);

        if (_tintUnsaved && !tintChanged)
        {
            profiles.Save();
            _tintUnsaved = false;
        }
        _tintUnsaved |= tintChanged;
    }

    private bool Update(Entity entity)
    {
        if (profiles.ActiveProfile is not { } profile)
            return false;

        ref ProfileEditView view = ref entity.GetComponent<ProfileEditView>();
        UpdateName(ref view, profile);
        return UpdateTint(view, profile);
    }

    private void UpdateName(ref ProfileEditView view, UserProfile profile)
    {
        ref TextField field = ref view.Name.GetComponent<TextField>();
        if (field.Value != view.Typed)
        {
            view.Typed = field.Value;
            profiles.Rename(profile.Id, field.Value);
        }

        // Typing stays as typed; a blank, overlong or taken name settles when focus leaves.
        if (!view.Name.Tags.Has<TextFieldFocused>() && field.Value != profile.Name)
        {
            field.Value = profile.Name;
            view.Typed = profile.Name;
        }
    }

    private static bool UpdateTint(in ProfileEditView view, UserProfile profile)
    {
        Color tint = new(view.Picker.GetComponent<UiColorPicker>().Color, 1);
        string hex = SelfModulate.Format(tint);
        if (hex == profile.NoteTint)
            return false;

        profile.NoteTint = hex;
        if (view.Art.TryGetComponent(out UiMaterial material))
            ProfileArt.Apply(material.Material, tint);

        return true;
    }
}
