using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Profiles;
using Xanadu.Strings;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>
/// Local profile picker shown before the main menu. The profiles flow in one
/// column; the create button follows the list, so adding a profile never moves
/// anything by hand.
/// </summary>
public static class ProfilesScene
{
    private const float TitleY = 110;
    private const float SubtitleY = 164;

    /// <summary>Centre of the first profile row.</summary>
    private const float FirstRowY = 246;

    private const float RowWidth = 440;
    private const float RowHeight = 54;
    private const float CreateWidth = 360;

    /// <summary>Space between the last row and the create button.</summary>
    private const float CreateGap = 30;
    private const float EmptyHintOffset = 62;
    private const float CreateBottomInset = 120;
    private const float FooterInset = 44;

    public static void Build(
        EntityStore entities,
        Entity root,
        ProfileStore profiles,
        IVisualRidFactory? visuals = null)
    {
        UiStage stage = UiStage.Full;
        UiNode ui = UiNode.Layer(entities, root, SceneUi.MenuLayer, visuals, stage);

        ui.Rect(SceneUi.BackgroundColor).Cover().Below();
        ui.Text("WHO'S PLAYING?", UiType.Title, SceneUi.AccentColor)
            .At(new Vector2(stage.Center.X, TitleY));
        ui.Text(
                "Pick a profile to load its saved scores.",
                UiType.Label,
                SceneUi.DimTextColor)
            .At(new Vector2(stage.Center.X, SubtitleY));

        UiNode list = ProfileList(ui, profiles, out float listBottom);

        float createY = Mathf.Min(
            stage.Height - CreateBottomInset,
            listBottom + CreateGap);

        if (profiles.Profiles.Count == 0)
        {
            ui.Text(
                    "No profiles yet. Create one to start playing.",
                    UiType.Label,
                    SceneUi.DimTextColor)
                .At(new Vector2(stage.Center.X, createY + EmptyHintOffset));
        }

        ui.Button(
                LocalizedText.Get(ProfilesStrings.NewProfile),
                UiType.Label,
                SceneUi.ButtonColor)
            .Size(CreateWidth, RowHeight)
            .At(new Vector2(stage.Center.X, createY))
            .With(new ProfileCreate());

        if (profiles.ActiveProfile is { } current)
        {
            ui.Text(
                    $"CURRENT PROFILE  ·  {current.Name}",
                    UiType.Small,
                    SceneUi.DimTextColor)
                .At(new Vector2(stage.Center.X, stage.Height - FooterInset));
        }
    }

    /// <summary>
    /// One column of profile rows, sized to the profiles that exist. Returns the
    /// column node and, through <paramref name="bottom"/>, where it ends.
    /// </summary>
    private static UiNode ProfileList(UiNode ui, ProfileStore profiles, out float bottom)
    {
        int count = profiles.Profiles.Count;
        float height = count * RowHeight + Mathf.Max(0, count - 1) * UiMetrics.Space.Lg;

        UiNode list = ui.Column()
            .Width(RowWidth)
            .Gap(UiMetrics.Space.Lg)
            .At(new Vector2(
                UiStage.Full.Center.X,
                FirstRowY - RowHeight / 2 + height / 2));

        foreach (UserProfile profile in profiles.Profiles)
        {
            bool active = profile.Id == profiles.ActiveProfileId;
            list.Button(
                    $"{(active ? "▶  " : "")}{profile.Name}",
                    UiType.Label,
                    active ? SceneUi.ButtonColor : SceneUi.MutedButtonColor)
                .Size(RowWidth, RowHeight)
                .With(new ProfileSelect(profile.Id));
        }

        bottom = count == 0 ? FirstRowY - RowHeight / 2 : FirstRowY - RowHeight / 2 + height;
        return list;
    }
}
