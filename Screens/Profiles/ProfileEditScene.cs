using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
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
/// Sets the active player's name and the tint of their note art, previewed live.
/// A spec tree: the title, then the art beside a column of controls, then done.
/// </summary>
public static class ProfileEditScene
{
    private const float ArtSize = 360;
    private const float ArtBacking = 400;
    private const float ArtZoom = 2.2f;
    private const float ControlWidth = 440;
    private const float ControlHeight = 54;
    private const float DoneWidth = 360;

    public static void Build(
        EntityStore entities,
        Entity root,
        UserProfile profile,
        IVisualRidFactory? visuals = null)
    {
        bool loadAssets = visuals.LoadsAssets;
        UiNode ui = UiNode.Layer(entities, root, UiLayers.Menu, visuals, UiStage.Full);
        Color tint = ProfileTint.Of(profile);

        // The art sits over its tilted backing, so it is out of the slot's flow.
        var art = new Custom(p => SongSelectScene.TiltedArt(p, Vector2.Zero, ArtSize,
            ProfileArt.Material(loadAssets, tint, ProfileArt.Instance(profile), ArtZoom))) { Free = true };
        var name = new Field(profile.Name, width: ControlWidth, height: ControlHeight);
        var picker = new ColorPicker(tint, alpha: false);

        ui.Page().Cross(Cross.Center).Pad(Space.Xxl).Gap(Space.Lg).Add(
            new Label(ProfilesStrings.EditTitle, UiType.Title, ink: Ink.Accent),
            new Spacer(),
            new Row(gap: Space.Xxl * 2, cross: Cross.Center)
            {
                new Column(size: new Vector2(ArtBacking, ArtBacking))
                {
                    new Box(size: new Vector2(ArtBacking, ArtBacking), tone: Tone.Solid)
                    {
                        Style = n => n.With(new UiRotation(SongSelectScene.PreviewTilt)),
                    },
                    art,
                },
                new Column(gap: Space.Md, cross: Cross.Start, width: ControlWidth)
                {
                    Caption(ProfilesStrings.Name),
                    name,
                    Caption(ProfilesStrings.Tint),
                    picker,
                },
            },
            new Spacer(),
            new Button(ProfilesStrings.Done, tone: Tone.Secondary, width: DoneWidth, height: ControlHeight)
            {
                Style = n => n.On(SceneKind.Profiles),
            });

        ui.Entity.AddComponent(new ProfileEditView(name.Node.Entity, picker.Node.Entity, art.Node.Entity)
        {
            Typed = profile.Name,
        });
    }

    private static Label Caption(LString text) => new(text, UiType.Label, ink: Ink.Dim);
}
