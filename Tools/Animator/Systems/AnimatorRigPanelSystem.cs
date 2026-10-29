using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Keeps the right panel in step with the rig: each section of the rig list
/// shows how many it holds, the EFFECT tab names the selected effect, and the
/// tabs follow the selection, so picking an effect opens its tab and picking
/// anything else afterwards goes back to POSE.
/// </summary>
public sealed class AnimatorRigPanelSystem : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private int _effect = -1;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            XafPackage package = session.GetComponent<AnimatorDocument>().Package;

            UiCollapsibleControl.SetTitle(ui.BoneSection, Counted("SKELETON", package.Skeleton.Bones.Count));
            UiCollapsibleControl.SetTitle(ui.AttachmentSection, Counted("ATTACHMENTS", package.Attachments.Count));
            UiCollapsibleControl.SetTitle(ui.ChainSection, Counted("CHAINS", package.Chains.Count));
            UiCollapsibleControl.SetTitle(ui.EffectSection, Counted("EFFECTS", package.Effects.Count));
            UiCollapsibleControl.SetTitle(ui.ClothSection, Counted("CLOTH", package.Cloths.Count));

            int effect = Commands.AnimatorEffectCommands.Selected(session);
            SetText(ui.EffectCaption, effect >= 0
                ? $"{package.Effects[effect].Id.ToUpperInvariant()}  on {package.Effects[effect].BoneId}"
                : "NO EFFECT SELECTED");
            Follow(ui.Tabs, effect);
        }
    }

    private static string Counted(string title, int count) => $"{title}  ·  {count}";

    /// <summary>Switches tabs only on a new selection, so a tab the user picks stays picked.</summary>
    private void Follow(Entity strip, int effect)
    {
        bool picked = effect >= 0 && effect != _effect;
        bool dropped = effect < 0 && _effect >= 0;
        _effect = effect;
        if (strip.IsNull || !strip.HasComponent<UiTabs>())
            return;

        ref UiTabs tabs = ref strip.GetComponent<UiTabs>();
        if (picked)
            tabs.Selected = AnimatorLayout.EffectTab;
        else if (dropped && tabs.Selected == AnimatorLayout.EffectTab)
            tabs.Selected = AnimatorLayout.PoseTab;
    }

    private static void SetText(Entity label, string value)
    {
        if (label.IsNull || !label.HasComponent<Text>())
            return;

        ref Text text = ref label.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;
        label.AddTag<Dirty>();
    }
}
