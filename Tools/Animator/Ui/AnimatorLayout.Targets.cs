using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator;

/// <summary>Marks a line of the POSE tab's target readout.</summary>
public struct AnimatorTargetCaption(int line) : IComponent
{
    public int Line = line;
}

/// <summary>The POSE tab's OVERRIDES section: pins and ellipses, each driving a limb for a span of the clip.</summary>
public static partial class AnimatorLayout
{
    private static void BuildTargetSection(UiBuilder menu)
    {
        menu.Collapsible("OVERRIDES", ButtonWidth);
        for (int line = 0; line < 2; line++)
        {
            menu.Text("", new Vector2(ButtonWidth, CaptionHeight), UiTheme.SmallFontSize,
                line == 0 ? UiTheme.Accent : UiTheme.TextDim, TextAlign.Left)
                .AddComponent(new AnimatorTargetCaption(line));
        }
        Subhead(menu, "ADD");
        TargetRow(menu, ("PIN", "pin"), ("+ ELLIPSE", "orbit"));
        Subhead(menu, "SELECTED");
        TargetRow(menu, ("ANCHOR", "anchor"), ("DETACH", "detach"));
        Subhead(menu, "TIMING · OR DRAG ITS BAR");
        TargetRow(menu, ("FROM HERE", "from"), ("TO HERE", "to"));
        TargetRow(menu, ("EASE", "ease"));
        Subhead(menu, "ELLIPSE SHAPE");
        SliderRow(menu, "WIDE", AnimatorSliderValue.TargetRadiusX, Animation.XafLimbTarget.MinRadius, 120f,
            Commands.AnimatorTargetCommands.DefaultRadiusX, "0", TargetTooltip("rx")!);
        SliderRow(menu, "TALL", AnimatorSliderValue.TargetRadiusY, Animation.XafLimbTarget.MinRadius, 120f,
            Commands.AnimatorTargetCommands.DefaultRadiusY, "0", TargetTooltip("ry")!);
        SliderRow(menu, "TILT \u00b0", AnimatorSliderValue.TargetTilt, -180f, 180f, 0f, "0", TargetTooltip("tilt")!);
        Subhead(menu, "ELLIPSE PATH");
        SliderRow(menu, "START \u00b0", AnimatorSliderValue.TargetStart, -180f, 180f, 0f, "0", TargetTooltip("start")!);
        SliderRow(menu, "ARC \u00b0", AnimatorSliderValue.TargetSweep, -720f, 720f, 360f, "0", TargetTooltip("sweep")!);
        TargetRow(menu, ("REVERSE", "flip"));
        menu.EndCollapsible();
    }

    private static void TargetRow(UiBuilder ui, params (string Label, string Verb)[] buttons)
    {
        ui.Container(Vector2.Zero, new Vector2(ButtonWidth, RowHeight), spacing: 6, color: Clear);
        Vector2 size = RowButton(buttons.Length);
        foreach ((string label, string verb) in buttons)
            TargetButton(ui, label, verb, size, UiTheme.SmallFontSize);
        ui.End();
    }

    private static void TargetButton(UiBuilder ui, string label, string verb, Vector2 size, int fontSize)
    {
        Entity button = ui.Button(label, size, fontSize: fontSize);
        button.AddComponent(ToolAction.Of(ToolActionKind.LimbTarget, verb));
        AnimatorTooltips.Attach(button, ToolActionKind.LimbTarget, verb);
    }

    public static string? TargetTooltip(string argument) => argument.Split(':')[0] switch
    {
        "pin" => "Pin the selected foot or hand to its spot from the playhead on, saved with the clip; again to end it at the playhead (Y)",
        "orbit" => "Send the selected hand, foot or root round an ellipse, starting where it is",
        "detach" => "Stop the override driving this limb and keep its keys as ordinary keys",
        "rx" => "The ellipse's size along its long side; or drag its handle",
        "ry" => "The ellipse's size across; or drag its handle",
        "tilt" => "Turn the ellipse",
        "start" => "Where on the ellipse the span starts",
        "sweep" => "How far round it goes; whole turns loop cleanly",
        "flip" => "Go round the other way",
        "ease" => "How time maps to the way round: linear, ease, ease in, ease out",
        "anchor" => "Which bone it travels with, or fixed in the rig",
        "from" => "Start the override at the playhead; or drag its bar on the timeline",
        "to" => "End the override at the playhead; or drag its bar on the timeline",
        _ => null,
    };
}
