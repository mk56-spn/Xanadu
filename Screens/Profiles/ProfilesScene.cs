using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Profiles;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using Xanadu.Screens.Shared;
using Xanadu.Screens.SongSelect;
using Xanadu.Logic.Profiles;
using static Xanadu.Core.Theme.UiMetrics;

namespace Xanadu.Screens.Profiles;

/// <summary>
/// Local profile picker shown before the main menu, as a spec tree: the title,
/// then the profile rows and the create button on one backing, so adding a
/// profile only adds a row.
/// </summary>
public static class ProfilesScene
{
    private const float RowWidth = 640;
    private const float RowHeight = 54;
    private const float ArtSize = 54;
    private const float ArtZoom = 4.5f;
    private const float EditWidth = 110;
    private const float CreateWidth = 360;

    public static void Build(
        EntityStore entities,
        Entity root,
        ProfileStore profiles,
        IVisualItemFactory? visuals = null)
    {
        UiNode ui = UiNode.Layer(entities, root, UiLayers.Menu, visuals, UiStage.Full)
            .Theme(UiThemes.Menu);
        bool loadAssets = visuals.LoadsAssets;

        ui.Page().Cross(Cross.Center).Pad(Space.Xxl).Gap(Space.Lg).Add(
            new Label("WHO'S PLAYING?", UiType.Title, ink: Ink.Accent),
            new Label("Pick a profile to load its saved scores.", UiType.Label, ink: Ink.Dim),
            // On a backing, so the rows read over the moving notes.
            new Column(gap: Space.Lg, pad: SceneUi.BackingPad, cross: Cross.Center, tone: Tone.Backing)
            {
                profiles.Profiles.Select(p => ProfileRow(p, p.Id == profiles.ActiveProfileId, loadAssets)),
                new Button(ProfilesStrings.NewProfile, tone: Tone.Primary,
                    width: CreateWidth, height: RowHeight) { Style = n => n.With(SceneAction.CreateProfile()) },
            },
            profiles.Profiles.Count == 0
                ? new Label("No profiles yet. Create one to start playing.", UiType.Label, ink: Ink.Dim)
                : null,
            new Spacer(),
            profiles.ActiveProfile is { } current
                ? new Label($"CURRENT PROFILE  ·  {current.Name}", UiType.Small, ink: Ink.Dim)
                : null);
    }

    /// <summary>A profile's art, its select button taking the rest of the row, and its edit button.</summary>
    private static Row ProfileRow(UserProfile profile, bool active, bool loadAssets) =>
        new(gap: Space.Lg, cross: Cross.Center, width: RowWidth, height: RowHeight)
        {
            new Custom(p => SongSelectScene.TiltedArt(p, Vector2.Zero, ArtSize,
                ProfileArt.Material(loadAssets, ProfileTint.Of(profile), ProfileArt.Instance(profile), ArtZoom))),
            new Button($"{(active ? "▶  " : "")}{profile.Name}",
                tone: active ? Tone.Primary : Tone.Secondary, height: RowHeight, grow: 1)
            {
                Style = n => n.With(SceneAction.SelectProfile(profile.Id)),
            },
            new Button(ProfilesStrings.Edit, UiType.Small, tone: Tone.Secondary,
                width: EditWidth, height: RowHeight)
            {
                Style = n => n.With(SceneAction.EditProfile(profile.Id)),
            },
        };
}
