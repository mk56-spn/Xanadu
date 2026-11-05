using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Profiles;
using Xanadu.Strings;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

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

    private const float RowWidth = 640;
    private const float ArtSize = 54;
    private const float EditWidth = 110;
    private const float ArtZoom = 4.5f;
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
        UiNode ui = UiNode.Layer(entities, root, UiLayers.Menu, visuals, stage)
            .Theme(UiThemes.Menu);

        ui.Text("WHO'S PLAYING?", UiType.Title)
            .AccentText()
            .At(new Vector2(stage.Center.X, TitleY));
        ui.Text(
                "Pick a profile to load its saved scores.",
                UiType.Label)
            .DimText()
            .At(new Vector2(stage.Center.X, SubtitleY));

        float listBottom = ListBottom(profiles.Profiles.Count);
        float createY = Mathf.Min(
            stage.Height - CreateBottomInset,
            listBottom + CreateGap + RowHeight / 2);

        // A dark panel behind the rows and the create button, so they read over the moving notes.
        float top = FirstRowY - RowHeight / 2 - SceneUi.BackingPad;
        float bottom = createY + RowHeight / 2 + SceneUi.BackingPad;
        ui.Rect(
                default,
                size: new Vector2(RowWidth + SceneUi.BackingPad * 2, bottom - top))
            .Backing()
            .At(new Vector2(stage.Center.X, (top + bottom) / 2));

        ProfileList(ui, profiles, visuals is GodotVisualRidFactory);

        if (profiles.Profiles.Count == 0)
        {
            ui.Text(
                    "No profiles yet. Create one to start playing.",
                    UiType.Label)
                .DimText()
                .At(new Vector2(stage.Center.X, createY + EmptyHintOffset));
        }

        ui.Button(
                LocalizedText.Get(ProfilesStrings.NewProfile),
                UiType.Label)
            .Primary()
            .Size(CreateWidth, RowHeight)
            .At(new Vector2(stage.Center.X, createY))
            .With(new ProfileCreate());

        if (profiles.ActiveProfile is { } current)
        {
            ui.Text(
                    $"CURRENT PROFILE  ·  {current.Name}",
                    UiType.Small)
                .DimText()
                .At(new Vector2(stage.Center.X, stage.Height - FooterInset));
        }
    }

    private static float ListHeight(int count) =>
        count * RowHeight + Mathf.Max(0, count - 1) * UiMetrics.Space.Lg;

    /// <summary>Where the profile rows end; the first row's top when there are none.</summary>
    private static float ListBottom(int count) =>
        FirstRowY - RowHeight / 2 + ListHeight(count);

    /// <summary>One column of profile rows, sized to the profiles that exist.</summary>
    private static UiNode ProfileList(UiNode ui, ProfileStore profiles, bool loadAssets)
    {
        int count = profiles.Profiles.Count;
        float height = ListHeight(count);

        UiNode list = ui.Column()
            .Width(RowWidth)
            .Gap(UiMetrics.Space.Lg)
            .At(new Vector2(
                UiStage.Full.Center.X,
                FirstRowY - RowHeight / 2 + height / 2));

        foreach (UserProfile profile in profiles.Profiles)
        {
            bool active = profile.Id == profiles.ActiveProfileId;
            UiNode row = list.Row()
                .Size(RowWidth, RowHeight)
                .Gap(UiMetrics.Space.Lg)
                .Main(StackMainAlignment.Start);

            SongSelectScene.TiltedArt(
                row,
                Vector2.Zero,
                ArtSize,
                ProfileArt.Material(loadAssets, ProfileArt.Tint(profile), ProfileArt.Instance(profile), ArtZoom));

            UiNode select = row.Button(
                    $"{(active ? "▶  " : "")}{profile.Name}",
                    UiType.Label)
                .Size(RowWidth - ArtSize - EditWidth - UiMetrics.Space.Lg * 2, RowHeight)
                .With(new ProfileSelect(profile.Id));
            if (active)
                select.Primary();
            else
                select.Secondary();

            row.Button(
                    LocalizedText.Get(ProfilesStrings.Edit),
                    UiType.Small)
                .Secondary()
                .Size(EditWidth, RowHeight)
                .With(new ProfileEditAction(profile.Id));
        }

        return list;
    }
}
