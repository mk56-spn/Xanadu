using System.Linq;
using Xanadu.Core.Theme;
using Xanadu.Logic.Profiles;
using Xanadu.Screens.Shared;
using Xanadu.Screens.SongSelect;
using Xanadu.Storage.Profiles;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using static Xanadu.Core.Theme.UiMetrics;
using static Xanadu.Ui.Ecs.Ui.Tree.UiSpecs;

namespace Xanadu.Screens.Profiles;

/// <summary>
/// The profile picker shown before the main menu: the title, then the profile
/// rows and the create button on one backing, so adding a profile only adds a row.
/// </summary>
public sealed class ProfilesLayout : IScreenLayout<ProfilesLogic>
{
    private const float RowWidth = 640;
    private const float RowHeight = 54;
    private const float ArtSize = 54;
    private const float ArtZoom = 4.5f;
    private const float EditWidth = 110;
    private const float CreateWidth = 360;

    public string Name => "CLASSIC";

    public void Build(UiNode layer, ProfilesLogic logic)
    {
        layer.Theme(UiThemes.Menu);
        bool loadAssets = layer.LoadsAssets;

        layer.Page().Cross(Cross.Center).Pad(Space.Xxl).Gap(Space.Lg).Add(
            new Label("WHO'S PLAYING?", UiType.Title, ink: Ink.Accent),
            new Label("Pick a profile to load its saved scores.", UiType.Label, ink: Ink.Dim),
            // On a backing, so the rows read over the moving notes.
            new Column(gap: Space.Lg, pad: SceneUi.BackingPad, cross: Cross.Center, tone: Tone.Backing)
            {
                logic.Profiles.Select((p, i) => ProfileRow(p, i, logic.IsActive(p), loadAssets)),
                new Button(ProfilesStrings.NewProfile, tone: Tone.Primary, cmd: Cmd(ProfilesEntrance.Create),
                    width: CreateWidth, height: RowHeight),
            },
            logic.Profiles.Count == 0
                ? new Label("No profiles yet. Create one to start playing.", UiType.Label, ink: Ink.Dim)
                : null,
            new Spacer(),
            logic.Active is { } current
                ? new Label($"CURRENT PROFILE  ·  {current.Name}", UiType.Small, ink: Ink.Dim)
                : null);
    }

    /// <summary>A profile's art, its select button taking the rest of the row, and its edit button.</summary>
    private static Row ProfileRow(UserProfile profile, int index, bool active, bool loadAssets) =>
        new(gap: Space.Lg, cross: Cross.Center, width: RowWidth, height: RowHeight)
        {
            new Custom(p => SongSelectScene.TiltedArt(p, Vector2.Zero, ArtSize,
                ProfileArt.Material(loadAssets, ProfileTint.Of(profile), ProfileArt.Instance(profile)))),
            new Button($"{(active ? "▶  " : "")}{profile.Name}",
                tone: active ? Tone.Primary : Tone.Secondary, cmd: Cmd(ProfilesEntrance.Select, index),
                height: RowHeight, grow: 1),
            new Button(ProfilesStrings.Edit, UiType.Small, tone: Tone.Secondary, cmd: Cmd(ProfilesEntrance.Edit, index),
                width: EditWidth, height: RowHeight),
        };
}
