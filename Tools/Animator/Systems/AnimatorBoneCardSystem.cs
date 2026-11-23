using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Shows the bone card and the overrides card under the transform inspector,
/// in the SELECTED tab, while a bone is selected and the inspector is showing,
/// and lights its joint toggles while they are on for the selected bone. The
/// rig-building rows of the bone and part cards only show while EDIT RIG is on.
/// </summary>
public sealed class AnimatorBoneCardSystem : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Color Lit = Palette.Primary;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Update(session);
    }

    private static void Update(Entity session)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        if (ui.BoneCard.IsNull)
            return;

        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        int bone = session.GetComponent<AnimatorSelection>().Bone;
        bool hasBone = bone >= 0 && bone < package.Skeleton.Bones.Count;
        bool shown = hasBone && !ui.Inspector.IsNull && !UiLayout.IsHidden(ui.Inspector);

        bool editingRig = session.GetComponent<AnimatorModes>().Rest;
        ShowRig(ui.BoneRigRows, ui.BoneRigHint, editingRig);
        ShowRig(ui.PartRigRows, ui.PartRigHint, editingRig);

        UiLayout.SetHidden(ui.BoneCard, !shown);
        UiLayout.SetHidden(ui.OverrideCard, !shown);
        if (!shown)
            return;

        RigBone rig = package.Skeleton.Bones[bone];
        Paint(ui.BoneMoveButton, rig.CanTranslate);
        Paint(ui.BoneBendButton, rig.Bend != 0);
        Paint(ui.BoneLimitButton, rig.Limited);
    }

    /// <summary>Rig-building rows while EDIT RIG is on, the hint pointing at it otherwise.</summary>
    private static void ShowRig(Entity rows, Entity hint, bool rig)
    {
        if (!rows.IsNull && UiLayout.IsHidden(rows) == rig)
            UiLayout.SetHidden(rows, !rig);
        if (!hint.IsNull && UiLayout.IsHidden(hint) != rig)
            UiLayout.SetHidden(hint, rig);
    }

    private static void Paint(Entity button, bool on)
    {
        if (button.IsNull || !button.HasComponent<ButtonVisual>())
            return;

        Color color = on ? Lit : UiTheme.Button;
        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == color)
            return;

        visual.Normal = color;
        visual.Hover = color.Lerp(Colors.White, 0.16f);
    }
}
