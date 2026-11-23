using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Ui;
using Xanadu.Ui.Ecs.Ui;
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

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Update(session);
    }

    private void Update(Entity session)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        if (ui.ClothInspector.IsNull)
            return;

        int index = AnimatorClothCommands.Selected(session);
        bool shown = index >= 0 && !AnimatorPreviewModeCommands.IsOn(session);
        UiLayout.SetHidden(ui.ClothInspector, !shown);

        RigCloth? cloth = index >= 0 ? session.GetComponent<AnimatorDocument>().Package.Cloths[index] : null;
        AnimatorClothPick picking = session.GetComponent<AnimatorCloths>().Picking;
        UiText.Set(ui.ClothTitle, cloth is null ? "NO CLOTH" : cloth.Id.ToUpperInvariant() + "  CLOTH");
        SetButton(ui.ClothBoneButton, cloth?.BoneId ?? "", picking == AnimatorClothPick.Bone, "CLICK A BONE");
        SetButton(ui.ClothPinsButton, cloth is null ? "" : $"{cloth.Pins.Count} PINNED · {cloth.Tethers.Count} TIED",
            picking == AnimatorClothPick.Pins, "CLICK POINTS · HERE TO STOP");
        SetButton(ui.ClothCollidersButton, cloth is null ? "" : $"{cloth.Colliders.Count} BONES · EDIT",
            picking == AnimatorClothPick.Colliders, "CLICK BONES · HERE TO STOP");
        string shader = cloth?.Node.ShaderRef.Path is { Length: > 0 } path ? System.IO.Path.GetFileNameWithoutExtension(path).ToUpperInvariant() : "PLAIN";
        SetButton(ui.ClothArtButton, cloth is null ? "" : $"{cloth.Node.Polygons.Count} SHAPES · {shader} · EDIT XIF", false, "");
    }

    /// <summary>Writes a button's value, or its prompt while its clicks are awaited, and lights it then.</summary>
    private static void SetButton(Entity button, string value, bool picking, string prompt)
    {
        UiText.Set(button, picking ? prompt : value);
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
