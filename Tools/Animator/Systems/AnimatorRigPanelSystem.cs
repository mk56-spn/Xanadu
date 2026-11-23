using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Animation;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Keeps the right panel in step with the selection: the part card shows and names the selected part,
/// and a new selection opens the SELECTED tab.
/// </summary>
public sealed class AnimatorRigPanelSystem : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private (int, int, int, int, int) _selected = (-1, -1, -1, -1, -1);

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            RigPackage package = session.GetComponent<AnimatorDocument>().Package;


            int part = Commands.AnimatorEffectCommands.Selected(session);
            UiText.Set(ui.EffectCaption, part >= 0
                ? $"{package.Attachments[part].Id.ToUpperInvariant()}  on {package.Attachments[part].BoneId}"
                : "NO PART SELECTED");
            UiLayout.SetHidden(ui.PartCard, part < 0);
            Follow(session, ui.Tabs);
        }
    }

    /// <summary>
    /// Opens the SELECTED tab when something new is selected, so its card is
    /// in view; a tab the user picks afterwards stays picked.
    /// </summary>
    private void Follow(Entity session, Entity strip)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        var now = (selection.Bone, Commands.AnimatorEffectCommands.Selected(session), selection.Chain,
            Commands.AnimatorClothCommands.Selected(session), Commands.AnimatorEventCommands.Selected(session, out _));
        bool picked = now != _selected && (now.Item1 >= 0 || now.Item2 >= 0 || now.Item3 >= 0 || now.Item4 >= 0 || now.Item5 >= 0);
        _selected = now;
        if (picked && !strip.IsNull && strip.HasComponent<UiTabs>())
            strip.GetComponent<UiTabs>().Selected = AnimatorLayout.SelectedTab;
    }
}
