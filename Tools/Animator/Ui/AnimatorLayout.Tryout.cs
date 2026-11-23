using Friflo.Engine.ECS;
using Xanadu.Core.Theme;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Ui;

/// <summary>
/// The ease bar of the quick test after keying, floating at the bottom centre
/// of the preview: what to call the move, an ease button per offered ease,
/// then replay and close. Hidden until a key is set.
/// </summary>
public static partial class AnimatorLayout
{
    private const float TryoutControl = 28f;
    private const float TryoutEaseWidth = 74f;
    private const float TryoutLabelWidth = 110f;
    private const float TryoutBottom = 14f;

    public static Vector2 TryoutBarSize => new(
        TryoutLabelWidth + AnimatorKeyTryoutCommands.Offered.Length * TryoutEaseWidth + 2 * 74f
        + (AnimatorKeyTryoutCommands.Offered.Length + 3) * 6f + 2 * 4f,
        36f);

    private static void BuildTryoutBar(UiBuilder menu, EditorShellRects shell, ref Refs refs)
    {
        Vector2 size = TryoutBarSize;
        UiNode bar = UiNode.Wrap(menu.Current, menu.Visuals).Row()
            .Size(size)
            .Gap(6)
            .Pad(4)
            .Solid()
            .Input()
            .At(new Vector2(
                shell.Viewport.Position.X + shell.Viewport.Size.X / 2f,
                shell.Viewport.Position.Y + shell.Viewport.Size.Y - TryoutBottom - size.Y / 2f));
        refs.Ui.TryoutBar = bar.Entity;
        UiLayout.SetHidden(bar.Entity, true);

        bar.Text("EASE INTO KEY", UiTheme.SmallFontSize, UiTheme.Accent, TextAlign.Center)
            .Size(TryoutLabelWidth, TryoutControl);
        var eases = new Entity[AnimatorKeyTryoutCommands.Offered.Length];
        for (int i = 0; i < eases.Length; i++)
        {
            var preset = AnimatorKeyTryoutCommands.Offered[i];
            Entity button = bar.Button(EaseNames.Short(preset), UiTheme.SmallFontSize)
                .Size(new Vector2(TryoutEaseWidth, TryoutControl)).Entity;
            button.AddComponent(ToolAction.Of(ToolActionKind.TryoutEase, preset.ToString()));
            AnimatorTooltips.Attach(button, ToolActionKind.TryoutEase, preset.ToString());
            eases[i] = button;
        }
        refs.Ui.TryoutEases = eases;
        foreach ((string label, ToolActionKind kind) in new[] { ("REPLAY", ToolActionKind.TryoutReplay), ("CLOSE", ToolActionKind.TryoutClose) })
        {
            Entity button = bar.Button(label, UiTheme.SmallFontSize).Size(new Vector2(74f, TryoutControl)).Entity;
            button.AddComponent(ToolAction.Of(kind));
            AnimatorTooltips.Attach(button, kind);
        }
    }
}
