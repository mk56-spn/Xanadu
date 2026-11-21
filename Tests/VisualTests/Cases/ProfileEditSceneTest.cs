using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Logic.Profiles;
using Xanadu.Core.Maths;
using Friflo.Engine.ECS;

namespace Xanadu.Tests.VisualTests;

public sealed class ProfileEditSceneTest : SceneTest
{
    public override string Name => "Profile edit";
    public override string Slug => "profile-edit";

    /// <summary>The harness keeps its profiles between runs, so reuse one rather than add one each time.</summary>
    internal static void OpenEditor(EntityStore entities)
    {
        Entity edit = UiCommand.Find(entities, ProfilesEntrance.Edit);
        (edit.IsNull ? UiCommand.Find(entities, ProfilesEntrance.Create) : edit).AddTag<Pressed>();
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
            () => VisualAssert.HasText(Context.Entities, "PLAYER NAME") &&
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
                  !UiCommand.Find(Context.Entities, ProfileEditEntrance.Name).IsNull);
        AddStep("rename and retint", () =>
        {
            UiCommand.Find(Context.Entities, ProfileEditEntrance.Name).GetComponent<TextField>().Value = "Crimson";
            UiCommand.Find(Context.Entities, ProfileEditEntrance.Tint).GetComponent<UiColorPicker>().Color = new Color(1f, 0.3f, 0.4f);
        });
        AddWaitStep("let the edit apply", 3);
        AddStep("go back", () => Context.Scenes.TryBack());
        AddUntilStep(
            "the list shows the new name",
            () => Context.Scenes.Current == SceneKind.Profiles &&
                  !UiCommand.Find(Context.Entities, ProfilesEntrance.Edit).IsNull &&
                  VisualAssert.HasText(Context.Entities, "▶  Crimson"));
    }
}
