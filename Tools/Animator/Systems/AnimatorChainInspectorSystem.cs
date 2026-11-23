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

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Update(session);
    }

    private void Update(Entity session)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        if (ui.ChainInspector.IsNull)
            return;

        // Preview mode gives the inspectors' corner back to the viewport.
        if (AnimatorPreviewModeCommands.IsOn(session))
        {
            AnimatorMenuPlacement.Hide(ui.ChainInspector);
            AnimatorMenuPlacement.Hide(ui.Inspector);
            return;
        }

        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        int selected = session.GetComponent<AnimatorSelection>().Chain;
        bool shown = selected >= 0 && selected < package.Chains.Count;
        RigChain? chain = shown ? package.Chains[selected] : null;

        UiLayout.SetHidden(ui.ChainInspector, !shown);

        // The transform inspector has nothing to show for a chain or a cloth, so it gives up its spot.
        UiLayout.SetHidden(ui.Inspector, shown || AnimatorClothCommands.Selected(session) >= 0);

        UiText.Set(ui.ChainTitle, chain is null ? "NO CHAIN" : chain.Id + "  CHAIN");
        AnimatorChainPick picking = session.GetComponent<AnimatorChains>().Picking;
        SetButton(ui.ChainSheetButton, chain is null ? "" : chain.Sheet ? "SHEET" : "PIECES", UiTheme.Button);
        SetButton(ui.ChainBoneButton, chain?.BoneId ?? "",
            picking == AnimatorChainPick.Anchor ? PickingColor : UiTheme.Button,
            picking == AnimatorChainPick.Anchor ? "CLICK A BONE" : null);
        SetButton(ui.ChainColliderButton, chain is null ? "" : chain.HasCollider ? chain.CollideBoneId : "NONE",
            picking == AnimatorChainPick.Collider ? PickingColor : UiTheme.Button,
            picking == AnimatorChainPick.Collider ? "CLICK A BONE" : null);

    }

    /// <summary>Writes a button's current value, or a prompt while it waits for a click, and lights it then.</summary>
    private static void SetButton(Entity button, string value, Color color, string? prompt = null)
    {
        UiText.Set(button, prompt ?? value);
        if (button.IsNull || !button.HasComponent<ButtonVisual>())
            return;

        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == color)
            return;

        visual.Normal = color;
        visual.Hover = color.Lerp(Colors.White, 0.16f);
    }
}
