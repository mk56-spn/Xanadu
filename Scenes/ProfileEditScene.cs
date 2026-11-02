using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Profiles;
using Xanadu.Strings;
using Xanadu.Systems.Shaders;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>Sets the active player's name and the tint of their note art, previewed live.</summary>
public static class ProfileEditScene
{
    private const float TitleY = 110;
    private const float ArtSize = 360;
    private const float ArtBacking = 400;
    private const float ArtZoom = 2.2f;
    private const float ControlWidth = 440;
    private const float ControlHeight = 54;
    private const float DoneWidth = 360;
    private const float DoneBottomInset = 120;

    private static readonly Vector2 ArtCenter = new(640, 500);
    private const float ControlsLeft = 960;
    private const float NameLabelY = 330;
    private const float NameFieldY = 384;
    private const float TintLabelY = 480;

    /// <summary>Clear of the label above and the picker's hex readout to the right.</summary>
    private static readonly Vector2 PickerCenter = new(ControlsLeft + 60, 590);

    public static void Build(
        EntityStore entities,
        Entity root,
        UserProfile profile,
        IVisualRidFactory? visuals = null)
    {
        UiStage stage = UiStage.Full;
        bool loadAssets = visuals is GodotVisualRidFactory;
        UiNode ui = UiNode.Layer(entities, root, UiLayers.Menu, visuals, stage);
        Color tint = ProfileArt.Tint(profile);

        ui.Text(LocalizedText.Get(ProfilesStrings.EditTitle), UiType.Title, SceneUi.AccentColor)
            .At(new Vector2(stage.Center.X, TitleY));

        ui.Rect(SceneUi.PanelColor, size: new Vector2(ArtBacking, ArtBacking))
            .At(ArtCenter)
            .With(new UiRotation(SongSelectScene.PreviewTilt));
        UiNode art = SongSelectScene.TiltedArt(
            ui,
            ArtCenter,
            ArtSize,
            ProfileArt.Material(loadAssets, tint, ProfileArt.Instance(profile), ArtZoom));

        Label(ui, ProfilesStrings.Name, NameLabelY);
        UiNode name = ui.Rect(Palette.SurfaceInset, profile.Name, new Vector2(ControlWidth, ControlHeight))
            .At(new Vector2(ControlsLeft + ControlWidth / 2, NameFieldY))
            .Input()
            .With(new TextStyle(UiType.Label, SceneUi.TextColor))
            .Align(TextAlign.Left)
            .With(new TextField(profile.Name));

        Label(ui, ProfilesStrings.Tint, TintLabelY);
        UiNode picker = ui.OpaqueColorPicker(
            PickerCenter,
            tint,
            loadAssets ? ColorPickerMaterials.Square() : null,
            loadAssets ? ColorPickerMaterials.Hue() : null);

        ui.Button(LocalizedText.Get(ProfilesStrings.Done), UiType.Label, SceneUi.ButtonColor)
            .Size(DoneWidth, ControlHeight)
            .At(new Vector2(stage.Center.X, stage.Height - DoneBottomInset))
            .On(SceneKind.Profiles);

        ui.Entity.AddComponent(new ProfileEditView(name.Entity, picker.Entity, art.Entity)
        {
            Typed = profile.Name,
        });
    }

    private static void Label(UiNode ui, LString text, float y) =>
        ui.Text(LocalizedText.Get(text), UiType.Label, SceneUi.DimTextColor, TextAlign.Left)
            .Size(ControlWidth, ControlHeight)
            .At(new Vector2(ControlsLeft + ControlWidth / 2, y));
}
