using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Tools.Animator.Commands;
using Setting = Xanadu.Tools.Animator.Commands.AnimatorClothCommands.Setting;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// The floating cloth inspector, shown in the transform inspector's spot while
/// a cloth is selected. Typed values apply when a field loses focus, as in the
/// chain inspector; the PINS and HITS buttons light while their clicks are
/// awaited and show how many there are otherwise.
/// </summary>
public sealed class AnimatorClothInspectorSystem : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Color PickingColor = Palette.Warning;
    private static readonly int Fields = Enum.GetValues<Setting>().Length;

    private readonly bool[] _wasFocused = new bool[Fields];
    private bool _fillWasFocused;
    private int _editCloth = -1;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Update(session);
    }

    private void Update(Entity session)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        if (ui.ClothInspector.IsNull || ui.ClothFields is null)
            return;

        int index = AnimatorClothCommands.Selected(session);
        bool shown = index >= 0 && !AnimatorPreviewModeCommands.IsOn(session);
        AnimatorMenuPlacement.Place(
            ui.ClothInspector,
            shown
                ? AnimatorLayout.InspectorTopLeft(ui.ScreenSize) + AnimatorLayout.ClothInspectorSize / 2
                : UiLayout.Hidden);

        XafCloth? cloth = index >= 0 ? session.GetComponent<AnimatorDocument>().Package.Cloths[index] : null;
        AnimatorClothPick picking = session.GetComponent<AnimatorCloths>().Picking;
        SetText(ui.ClothTitle, cloth is null ? "NO CLOTH" : cloth.Id.ToUpperInvariant() + "  CLOTH");
        SetButton(ui.ClothBoneButton, cloth?.BoneId ?? "", picking == AnimatorClothPick.Bone, "CLICK A BONE");
        SetButton(ui.ClothPinsButton, cloth is null ? "" : $"{cloth.Pins.Count} PINNED · EDIT",
            picking == AnimatorClothPick.Pins, "CLICK POINTS · HERE TO STOP");
        SetButton(ui.ClothCollidersButton, cloth is null ? "" : $"{cloth.Colliders.Count} BONES · EDIT",
            picking == AnimatorClothPick.Colliders, "CLICK BONES · HERE TO STOP");

        for (int i = 0; i < ui.ClothFields.Length; i++)
            Sync(session, ui.ClothFields[i], i, (Setting)i, cloth);
        SyncFill(session, ui.ClothFill, cloth);
    }

    private void Sync(Entity session, Entity field, int index, Setting setting, XafCloth? cloth)
    {
        if (field.IsNull || !field.HasComponent<TextField>())
            return;

        bool focused = field.Tags.Has<TextFieldFocused>();
        ref TextField text = ref field.GetComponent<TextField>();
        if (focused && !_wasFocused[index])
            _editCloth = session.GetComponent<AnimatorSelection>().Cloth;
        else if (!focused && _wasFocused[index] && session.GetComponent<AnimatorSelection>().Cloth == _editCloth)
        {
            if (float.TryParse(text.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                AnimatorClothCommands.Set(session, setting, value);
            else
                AnimatorSession.SetStatus(session, "NOT A NUMBER");
        }

        if (!focused)
            text.Value = cloth is null ? "" : Format(cloth, setting);
        _wasFocused[index] = focused;
    }

    private void SyncFill(Entity session, Entity field, XafCloth? cloth)
    {
        if (field.IsNull || !field.HasComponent<TextField>())
            return;

        bool focused = field.Tags.Has<TextFieldFocused>();
        ref TextField text = ref field.GetComponent<TextField>();
        if (focused && !_fillWasFocused)
            _editCloth = session.GetComponent<AnimatorSelection>().Cloth;
        else if (!focused && _fillWasFocused && session.GetComponent<AnimatorSelection>().Cloth == _editCloth)
            AnimatorClothCommands.SetFill(session, text.Value);

        if (!focused)
            text.Value = cloth is null ? "" : "#" + cloth.Fill.ToHtml(cloth.Fill.A < 1f);
        _fillWasFocused = focused;
    }

    private static string Format(XafCloth cloth, Setting setting) => setting switch
    {
        Setting.OffsetX => Number(cloth.Offset.X),
        Setting.OffsetY => Number(cloth.Offset.Y),
        Setting.Columns => cloth.Columns.ToString(CultureInfo.InvariantCulture),
        Setting.Rows => cloth.Rows.ToString(CultureInfo.InvariantCulture),
        Setting.TopWidth => Number(cloth.TopWidth),
        Setting.HemWidth => Number(cloth.HemWidth),
        Setting.Length => Number(cloth.Length),
        Setting.Gravity => Number(cloth.Gravity),
        Setting.Lean => Number(cloth.Lean),
        Setting.Damping => Number(cloth.Damping),
        Setting.Stiffness => Number(cloth.Stiffness),
        Setting.Layer => cloth.ZOrder.ToString(CultureInfo.InvariantCulture),
        _ => cloth.Colliders.Count > 0 ? Number(cloth.Colliders[0].Radius) : "",
    };

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

    /// <summary>Writes a button's value, or its prompt while its clicks are awaited, and lights it then.</summary>
    private static void SetButton(Entity button, string value, bool picking, string prompt)
    {
        SetText(button, picking ? prompt : value);
        if (button.IsNull || !button.HasComponent<ButtonVisual>())
            return;

        Color color = picking ? PickingColor : UiTheme.Button;
        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == color)
            return;

        visual.Normal = color;
        visual.Hover = color.Lerp(Colors.White, 0.16f);
    }
}
