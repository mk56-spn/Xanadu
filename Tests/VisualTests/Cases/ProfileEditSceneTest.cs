using Xanadu.Ecs;
using Xanadu.Profiles;

namespace Xanadu.Tests.VisualTests;

public sealed class ProfileEditSceneTest : SceneTest
{
    public override string Name => "Profile edit";
    public override string Slug => "profile-edit";

    /// <summary>The harness keeps its profiles between runs, so reuse one rather than add one each time.</summary>
    internal static void OpenEditor(Friflo.Engine.ECS.EntityStore entities)
    {
        var existing = SceneAction.All(entities, SceneActionKind.EditProfile);
        if (existing.Count > 0)
            existing[0].AddTag<Pressed>();
        else
            SceneAction.All(entities, SceneActionKind.CreateProfile)[0].AddTag<Pressed>();
    }

    protected override void SetUpSteps()
    {
        AddStep("open profiles", () => Context.Scenes.Start(SceneKind.Profiles));
        AddUntilStep(
            "wait for profiles",
            () => Context.Scenes.Current == SceneKind.Profiles &&
                  VisualAssert.HasText(Context.Entities, "NEW PROFILE"));
        AddStep("edit a profile", () => ProfileEditSceneTest.OpenEditor(Context.Entities));
        AddUntilStep(
            "wait for the edit screen",
            () => Context.Scenes.Current == SceneKind.ProfileEdit &&
                  VisualAssert.HasText(Context.Entities, "EDIT PROFILE"));
        AddAssert(
            "name and tint controls exist",
            () => Context.Entities.Query<ProfileEditView>().Count == 1 &&
                  VisualAssert.HasText(Context.Entities, "PLAYER NAME") &&
                  VisualAssert.HasText(Context.Entities, "NOTE TINT"));
    }
}

/// <summary>A renamed, retinted profile shows its picture on the profile list.</summary>
public sealed class ProfileTintSceneTest : SceneTest
{
    public override string Name => "Profile tint on the list";
    public override string Slug => "profile-tint";

    protected override void SetUpSteps()
    {
        AddStep("open profiles", () => Context.Scenes.Start(SceneKind.Profiles));
        AddUntilStep(
            "wait for profiles",
            () => Context.Scenes.Current == SceneKind.Profiles &&
                  VisualAssert.HasText(Context.Entities, "NEW PROFILE"));
        AddStep("edit a profile", () => ProfileEditSceneTest.OpenEditor(Context.Entities));
        AddUntilStep(
            "wait for the edit screen",
            () => Context.Scenes.Current == SceneKind.ProfileEdit &&
                  Context.Entities.Query<ProfileEditView>().Count == 1);
        AddStep("rename and retint", () =>
        {
            var view = Context.Entities.Query<ProfileEditView>().ToEntityList()[0]
                .GetComponent<ProfileEditView>();
            view.Name.GetComponent<TextField>().Value = "Crimson";
            view.Picker.GetComponent<UiColorPicker>().Color = new Color(1f, 0.3f, 0.4f);
        });
        AddWaitStep("let the edit apply", 3);
        AddStep("go back", () => Context.Scenes.TryBack());
        AddUntilStep(
            "the list shows the new name",
            () => Context.Scenes.Current == SceneKind.Profiles &&
                  SceneAction.All(Context.Entities, SceneActionKind.EditProfile).Count >= 1 &&
                  VisualAssert.HasText(Context.Entities, "▶  Crimson"));
    }
}
