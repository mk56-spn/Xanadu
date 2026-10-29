using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Theme;
using Setting = Xanadu.Tools.Animator.Commands.AnimatorChainCommands.Setting;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// The floating chain inspector. It takes the transform inspector's spot while
/// a chain is selected and shows everything about it: its art, the bone it
/// hangs from and where, its physics, and its collider. Typed values are
/// applied through <see cref="AnimatorChainCommands"/> when a field loses focus;
/// the buttons start the art picker and the pick-a-bone flows.
/// </summary>
public sealed class AnimatorChainInspectorSystem : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Color PickingColor = Palette.Warning;

    private readonly bool[] _wasFocused = new bool[Fields];
    private int _editChain = -1;

    /// <summary>The number of typed fields; they are listed in <see cref="FieldsOf"/>.</summary>
    private const int Fields = 11;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Update(session);
    }

    private static (Entity Field, Setting Setting)[] FieldsOf(AnimatorUi ui) =>
    [
        (ui.ChainOffsetX, Setting.OffsetX),
        (ui.ChainOffsetY, Setting.OffsetY),
        (ui.ChainSegments, Setting.Segments),
        (ui.ChainLength, Setting.Length),
        (ui.ChainWidth, Setting.Width),
        (ui.ChainGravity, Setting.Gravity),
        (ui.ChainLean, Setting.Lean),
        (ui.ChainDamping, Setting.Damping),
        (ui.ChainColliderX, Setting.ColliderX),
        (ui.ChainColliderY, Setting.ColliderY),
        (ui.ChainCollide, Setting.Collide),
    ];

    private void Update(Entity session)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        if (ui.ChainInspector.IsNull)
            return;

        // Preview mode gives the inspectors' corner back to the viewport.
        if (AnimatorPreviewModeCommands.IsOn(session))
        {
            AnimatorMenuPlacement.Place(ui.ChainInspector, UiLayout.Hidden);
            AnimatorMenuPlacement.Place(ui.Inspector, UiLayout.Hidden);
            return;
        }

        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        int selected = session.GetComponent<AnimatorSelection>().Chain;
        bool shown = selected >= 0 && selected < package.Chains.Count;
        XafChain? chain = shown ? package.Chains[selected] : null;

        AnimatorMenuPlacement.Place(
            ui.ChainInspector,
            shown
                ? AnimatorLayout.ChainInspectorTopLeft(ui.ScreenSize) + AnimatorLayout.ChainInspectorSize / 2
                : UiLayout.Hidden);

        // The transform inspector has nothing to show for a chain or a cloth, so it gives up its spot.
        AnimatorMenuPlacement.Place(
            ui.Inspector,
            shown || AnimatorClothCommands.Selected(session) >= 0
                ? UiLayout.Hidden
                : AnimatorLayout.InspectorCenter(ui.ScreenSize));

        SetText(ui.ChainTitle, chain is null ? "NO CHAIN" : chain.Id + "  CHAIN");
        AnimatorChainPick picking = session.GetComponent<AnimatorChains>().Picking;
        SetButton(ui.ChainArtButton, chain is null ? "" : ArtLabel(chain), UiTheme.Button);
        SetButton(ui.ChainBoneButton, chain?.BoneId ?? "",
            picking == AnimatorChainPick.Anchor ? PickingColor : UiTheme.Button,
            picking == AnimatorChainPick.Anchor ? "CLICK A BONE" : null);
        SetButton(ui.ChainColliderButton, chain is null ? "" : chain.HasCollider ? chain.CollideBoneId : "NONE",
            picking == AnimatorChainPick.Collider ? PickingColor : UiTheme.Button,
            picking == AnimatorChainPick.Collider ? "CLICK A BONE" : null);

        (Entity Field, Setting Setting)[] fields = FieldsOf(ui);
        for (int i = 0; i < fields.Length; i++)
            Sync(session, fields[i].Field, i, fields[i].Setting, chain);
    }

    private void Sync(Entity session, Entity field, int index, Setting setting, XafChain? chain)
    {
        if (field.IsNull || !field.HasComponent<TextField>())
            return;

        bool focused = field.Tags.Has<TextFieldFocused>();
        ref TextField text = ref field.GetComponent<TextField>();
        if (focused && !_wasFocused[index])
            _editChain = session.GetComponent<AnimatorSelection>().Chain;
        else if (!focused && _wasFocused[index])
            Commit(session, setting, text.Value);

        if (!focused)
            text.Value = chain is null ? "" : Format(chain, setting);
        _wasFocused[index] = focused;
    }

    /// <summary>Applies a typed value, unless the selection moved to another chain while typing.</summary>
    private void Commit(Entity session, Setting setting, string typed)
    {
        if (session.GetComponent<AnimatorSelection>().Chain != _editChain)
            return;

        if (!float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            AnimatorSession.SetStatus(session, "NOT A NUMBER");
            return;
        }

        AnimatorChainCommands.Set(session, setting, value);
    }

    private static string Format(XafChain chain, Setting setting) => setting switch
    {
        Setting.Segments => chain.Segments.ToString(CultureInfo.InvariantCulture),
        Setting.Length => Number(chain.SegmentLength),
        Setting.Width => Number(chain.Width),
        Setting.Gravity => Number(chain.Gravity),
        Setting.Lean => Number(chain.Lean),
        Setting.Collide => Number(chain.CollideRadius),
        Setting.OffsetX => Number(chain.Offset.X),
        Setting.OffsetY => Number(chain.Offset.Y),
        Setting.ColliderX => Number(chain.CollideOffset.X),
        Setting.ColliderY => Number(chain.CollideOffset.Y),
        _ => Number(chain.Damping),
    };

    /// <summary>The art as the picker names it: the file, plus the group when it differs; NONE without art.</summary>
    public static string ArtLabel(XafChain chain) =>
        chain.HasArt
            ? new AnimatorSvgChoice(chain.SvgPath, chain.Group).Label
            : "NONE";

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static void SetText(Entity entity, string value)
    {
        if (entity.IsNull || !entity.HasComponent<Text>())
            return;

        ref Text text = ref entity.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;
        entity.AddTag<Dirty>();
    }

    /// <summary>Writes a button's current value, or a prompt while it waits for a click, and lights it then.</summary>
    private static void SetButton(Entity button, string value, Color color, string? prompt = null)
    {
        SetText(button, prompt ?? value);
        if (button.IsNull || !button.HasComponent<ButtonVisual>())
            return;

        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == color)
            return;

        visual.Normal = color;
        visual.Hover = color.Lerp(Colors.White, 0.16f);
    }
}
