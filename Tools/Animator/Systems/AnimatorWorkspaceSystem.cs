using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Lights the header's workspace switch on the workspace the animator is in:
/// RIG orange (changes there move every clip), ANIMATE the usual bright blue,
/// PREVIEW violet like its bar. The others stay at rest.
/// </summary>
public sealed class AnimatorWorkspaceSystem : QuerySystem<AnimatorUi>
{
    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            AnimatorWorkspace current = AnimatorWorkspaceCommands.Current(session);
            // One pass, so an item listed for both workspaces is set once.
            foreach (Entity item in (Entity[])[.. ui.HiddenInRig ?? [], .. ui.HiddenInPreview ?? []])
            {
                // Mode toggles are the mode system's to show and hide.
                if (item.IsNull || item.HasComponent<ModeToggle>())
                    continue;
                bool hidden = current == AnimatorWorkspace.Rig && Array.IndexOf(ui.HiddenInRig ?? [], item) >= 0 ||
                              current == AnimatorWorkspace.Preview && Array.IndexOf(ui.HiddenInPreview ?? [], item) >= 0;
                UiLayout.SetHidden(item, hidden);
            }

            ShowTabs(ui, current == AnimatorWorkspace.Rig);

            if (ui.WorkspaceButtons is not { } buttons)
                continue;
            for (int i = 0; i < buttons.Length; i++)
                Light(buttons[i], i == (int)current ? Lit((AnimatorWorkspace)i) : UiTheme.Button);
        }
    }

    /// <summary>KEYS and FLOW are about clips, so RIG hides their tabs, leaving SELECTED if one was open.</summary>
    private static void ShowTabs(AnimatorUi ui, bool rig)
    {
        if (ui.Tabs.IsNull || !ui.Tabs.HasComponent<UiTabs>())
            return;
        ref UiTabs tabs = ref ui.Tabs.GetComponent<UiTabs>();
        if (tabs.Buttons.Length <= AnimatorLayout.FlowTab ||
            UiLayout.IsHidden(tabs.Buttons[AnimatorLayout.KeysTab]) == rig)
            return;

        foreach (int tab in new[] { AnimatorLayout.KeysTab, AnimatorLayout.FlowTab })
        {
            UiLayout.SetHidden(tabs.Buttons[tab], rig);
            if (rig && tabs.Selected == tab)
                tabs.Selected = AnimatorLayout.SelectedTab;
        }

        // The tabs left share the strip's width, as all of them do.
        int shown = rig ? tabs.Buttons.Length - 2 : tabs.Buttons.Length;
        float width = ui.Tabs.GetComponent<Rectangle>().Bounds.Size.X / shown;
        foreach (Entity button in tabs.Buttons)
        {
            Vector2 size = button.GetComponent<Rectangle>().Bounds.Size;
            UiNode.Wrap(button).Size(new Vector2(width, size.Y));
        }
    }

    private static Color Lit(AnimatorWorkspace workspace) => workspace switch
    {
        AnimatorWorkspace.Rig => Palette.Warning,
        AnimatorWorkspace.Preview => Palette.Violet,
        _ => UiTheme.ButtonActive,
    };

    private static void Light(Entity button, Color color)
    {
        if (button.IsNull || !button.HasComponent<ButtonVisual>())
            return;
        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == color)
            return;
        visual.Normal = color;
        visual.Hover = color.Lerp(Colors.White, 0.16f);
    }
}
