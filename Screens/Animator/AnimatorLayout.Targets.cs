using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Animator;

/// <summary>Marks a line of the POSE tab's target readout.</summary>
public struct AnimatorTargetCaption : ITag;

/// <summary>The POSE tab's OVERRIDES section: pins and ellipses, each driving a limb for a span of the clip.</summary>
public static partial class AnimatorLayout
{
    private static void BuildTargetSection(UiBuilder menu)
    {
        menu.Collapsible("OVERRIDES", ButtonWidth);
        for (int line = 0; line < 2; line++)
        {
            Entity caption = menu.Text("", new Vector2(ButtonWidth, CaptionHeight), UiTheme.SmallFontSize,
                line == 0 ? UiTheme.Accent : UiTheme.TextDim, TextAlign.Left);
            caption.AddComponent(new ListIndex(line));
            caption.AddTag<AnimatorTargetCaption>();
        }
        Subhead(menu, "ADD");
        TargetRow(menu, ("PIN", AnimatorTargetVerb.Pin), ("+ ELLIPSE", AnimatorTargetVerb.Orbit));
        Subhead(menu, "SELECTED");
        TargetRow(menu, ("ANCHOR", AnimatorTargetVerb.Anchor), ("DETACH", AnimatorTargetVerb.Detach));
        Subhead(menu, "TIMING · OR DRAG ITS BAR");
        TargetRow(menu, ("FROM HERE", AnimatorTargetVerb.From), ("TO HERE", AnimatorTargetVerb.To));
        TargetRow(menu, ("EASE", AnimatorTargetVerb.Ease));
        Subhead(menu, "ELLIPSE SHAPE");
        SliderRow(menu, "WIDE", AnimatorSliderValue.TargetRadiusX, Xanadu.Model.Animation.RigLimbTarget.MinRadius, 120f,
            AnimatorTargetCommands.DefaultRadiusX, "0", "The ellipse's size along its long side; or drag its handle");
        SliderRow(menu, "TALL", AnimatorSliderValue.TargetRadiusY, Xanadu.Model.Animation.RigLimbTarget.MinRadius, 120f,
            AnimatorTargetCommands.DefaultRadiusY, "0", "The ellipse's size across; or drag its handle");
        SliderRow(menu, "TILT \u00b0", AnimatorSliderValue.TargetTilt, -180f, 180f, 0f, "0", "Turn the ellipse");
        Subhead(menu, "ELLIPSE PATH");
        SliderRow(menu, "START \u00b0", AnimatorSliderValue.TargetStart, -180f, 180f, 0f, "0", "Where on the ellipse the span starts");
        SliderRow(menu, "ARC \u00b0", AnimatorSliderValue.TargetSweep, -720f, 720f, 360f, "0", "How far round it goes; whole turns loop cleanly");
        TargetRow(menu, ("REVERSE", AnimatorTargetVerb.Flip));
        menu.EndCollapsible();
    }

    private static void TargetRow(UiBuilder ui, params (string Label, AnimatorTargetVerb Verb)[] buttons)
    {
        ui.Container(Vector2.Zero, new Vector2(ButtonWidth, RowHeight), spacing: 6, color: Clear);
        Vector2 size = RowButton(buttons.Length);
        foreach ((string label, AnimatorTargetVerb verb) in buttons)
            TargetButton(ui, label, verb, size, UiTheme.SmallFontSize);
        ui.End();
    }

    private static void TargetButton(UiBuilder ui, string label, AnimatorTargetVerb verb, Vector2 size, int fontSize)
    {
        Entity button = ui.Button(label, size, fontSize: fontSize);
        IIntent intent = TargetIntent(verb);
        button.AddComponent(new Intent(intent));
        AnimatorTooltips.Attach(button, intent);
    }

    /// <summary>What a limb target button sends.</summary>
    public static IIntent TargetIntent(AnimatorTargetVerb verb) => verb switch
    {
        AnimatorTargetVerb.Pin => new PlantBone(),
        AnimatorTargetVerb.Orbit => new AddOrbit(),
        AnimatorTargetVerb.Anchor => new StepTargetAnchor(),
        AnimatorTargetVerb.Detach => new DetachTarget(),
        AnimatorTargetVerb.From => new SetTargetFrom(),
        AnimatorTargetVerb.To => new SetTargetTo(),
        AnimatorTargetVerb.Ease => new StepTargetEase(),
        _ => new FlipTarget(),
    };

    public static string TargetTooltip(AnimatorTargetVerb verb) => verb switch
    {
        AnimatorTargetVerb.Pin => "Pin the selected foot or hand to its spot from the playhead on, saved with the clip; again to end it at the playhead (Y)",
        AnimatorTargetVerb.Orbit => "Send the selected hand, foot or root round an ellipse, starting where it is",
        AnimatorTargetVerb.Detach => "Stop the override driving this limb and keep its keys as ordinary keys",
        AnimatorTargetVerb.Flip => "Go round the other way",
        AnimatorTargetVerb.Ease => "How time maps to the way round: linear, ease, ease in, ease out",
        AnimatorTargetVerb.Anchor => "Which bone it travels with, or fixed in the rig",
        AnimatorTargetVerb.From => "Start the override at the playhead; or drag its bar on the timeline",
        AnimatorTargetVerb.To => "End the override at the playhead; or drag its bar on the timeline",
        _ => "",
    };
}
