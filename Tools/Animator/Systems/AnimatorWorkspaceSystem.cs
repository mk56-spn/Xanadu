using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
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
            if (session.GetComponent<AnimatorUi>().WorkspaceButtons is not { } buttons)
                continue;

            AnimatorWorkspace current = AnimatorWorkspaceCommands.Current(session);
            for (int i = 0; i < buttons.Length; i++)
                Light(buttons[i], i == (int)current ? Lit((AnimatorWorkspace)i) : UiTheme.Button);
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
