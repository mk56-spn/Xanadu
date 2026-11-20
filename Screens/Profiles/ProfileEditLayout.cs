using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Logic.Profiles;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using Xanadu.Screens.Shared;
using Xanadu.Screens.SongSelect;
using static Xanadu.Core.Theme.UiMetrics;
using static Xanadu.Ui.Ecs.Ui.Tree.UiSpecs;

namespace Xanadu.Screens.Profiles;

/// <summary>
/// Sets the active player's name and the tint of their note art, previewed live:
/// the title, then the art beside a column of controls, then done.
/// </summary>
public sealed class ProfileEditLayout : IScreenLayout<ProfileEditLogic>
{
    private const float ArtSize = 360;
    private const float ArtBacking = 400;
    private const float ArtZoom = 2.2f;
    private const float ControlWidth = 440;
    private const float ControlHeight = 54;
    private const float DoneWidth = 360;

    private Field _name = null!;
    private ColorPicker _picker = null!;
    private Custom _art = null!;
    private string _typed = "";
    private Color _shownTint;

    public string Name => "CLASSIC";

    public void Build(UiNode layer, ProfileEditLogic logic)
    {
        bool loadAssets = layer.LoadsAssets;
        _shownTint = logic.Tint;
        _typed = logic.Name;

        // The art sits over its tilted backing, so it is out of the slot's flow.
        _art = new Custom(p => SongSelectScene.TiltedArt(p, Vector2.Zero, ArtSize,
            ProfileArt.Material(loadAssets, _shownTint, ProfileArt.Instance(logic.Profile), ArtZoom))) { Free = true };
        // The inputs carry their entrance so the contract sees them bound.
        _name = new Field(logic.Name, width: ControlWidth, height: ControlHeight)
        {
            Style = n => n.With(Cmd(ProfileEditEntrance.Name)),
        };
        _picker = new ColorPicker(_shownTint, alpha: false)
        {
            Style = n => n.With(Cmd(ProfileEditEntrance.Tint)),
        };

        layer.Page().Cross(Cross.Center).Pad(Space.Xxl).Gap(Space.Lg).Add(
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
                    _art,
                },
                new Column(gap: Space.Md, cross: Cross.Start, width: ControlWidth)
                {
                    Caption(ProfilesStrings.Name),
                    _name,
                    Caption(ProfilesStrings.Tint),
                    _picker,
                },
            },
            new Spacer(),
            new Button(ProfilesStrings.Done, tone: Tone.Secondary, cmd: Cmd(ProfileEditEntrance.Done),
                width: DoneWidth, height: ControlHeight));
    }

    public void Refresh(ProfileEditLogic logic)
    {
        Entity name = _name.Node.Entity;
        ref TextField field = ref name.GetComponent<TextField>();
        if (field.Value != _typed)
        {
            _typed = field.Value;
            logic.Rename(field.Value);
        }

        // Typing stays as typed; a blank, overlong or taken name settles when focus leaves.
        if (!name.Tags.Has<TextFieldFocused>() && field.Value != logic.Name)
        {
            field.Value = logic.Name;
            _typed = logic.Name;
        }

        Color tint = new(_picker.Node.Entity.GetComponent<UiColorPicker>().Color, 1);
        if (tint == _shownTint)
            return;

        _shownTint = tint;
        logic.SetTint(tint);
        if (_art.Node.Entity.TryGetComponent(out UiMaterial material))
            ProfileTint.Apply(material.Material, tint);
    }

    private static Label Caption(LString text) => new(text, UiType.Label, ink: Ink.Dim);
}
