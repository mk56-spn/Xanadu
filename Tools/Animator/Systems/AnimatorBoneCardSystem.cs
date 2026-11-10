using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Shows the bone card under the transform inspector while a bone is selected
/// and the inspector is showing, and lights its joint toggles while they are
/// on for the selected bone. Hidden, it is parked off screen as well, so its
/// sliders cannot catch a press meant for the preview.
/// </summary>
public sealed class AnimatorBoneCardSystem : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Vector2 Parked = new(-10000f, -10000f);
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

        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        int bone = session.GetComponent<AnimatorSelection>().Bone;
        bool hasBone = bone >= 0 && bone < package.Skeleton.Bones.Count;
        bool shown = hasBone && !ui.Inspector.IsNull && !UiLayout.IsHidden(ui.Inspector);

        if (!shown)
        {
            AnimatorMenuPlacement.Place(ui.BoneCard, Parked);
            AnimatorMenuPlacement.Hide(ui.BoneCard);
            return;
        }

        AnimatorMenuPlacement.Place(ui.BoneCard, AnimatorLayout.BoneCardCenter(ui.ScreenSize));
        XafBone rig = package.Skeleton.Bones[bone];
        Paint(ui.BoneMoveButton, rig.CanTranslate);
        Paint(ui.BoneBendButton, rig.Bend != 0);
        Paint(ui.BoneLimitButton, rig.Limited);
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
