using System.Collections.Generic;
using System.Text;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Theme;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Fills the FLOW tab: one row per clip the edited clip could fade into, with
/// whether it may, its fade, and whether the game asks for it; then the
/// selected transition's readouts and which of its buttons are lit.
/// </summary>
public sealed class AnimatorFlowSystem(IVisualRidFactory visuals) : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Color Allowed = new(Palette.Lime, 0.55f);
    private static readonly Color Missing = new(Palette.Warning, 0.6f);
    private static readonly Color Lit = Palette.Selected;

    /// <summary>What the rows were last built from, so they are rebuilt only when it changes.</summary>
    private string _built = "";

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            if (ui.FlowList.IsNull)
                continue;

            XafPackage package = session.GetComponent<AnimatorDocument>().Package;
            string from = AnimatorFlowCommands.From(session);
            List<(string From, string To)> game = AnimatorFlowCommands.GameTransitions(package);
            string signature = Signature(package, from);
            if (signature != _built)
            {
                _built = signature;
                Rebuild(ui.FlowList, package, from, game);
            }

            SyncRows(ui.FlowList, package, from, game, session.GetComponent<AnimatorFlow>().Target ?? "");

            SyncReadouts(session, ui, package, from, game);
        }
    }

    private void Rebuild(
        Entity list,
        XafPackage package,
        string from,
        List<(string From, string To)> game)
    {
        var children = new List<Entity>();
        foreach (Entity child in list.ChildEntities)
            children.Add(child);
        foreach (Entity child in children)
            UiLayout.DeleteSubtree(child);
        list.AddTag<LayoutDirty>();

        foreach (XafClip clip in package.Clips)
        {
            if (clip.Id == from)
                continue;

            UiBuilder ui = UiBuilder.Begin(Query.Store, visuals).Use(list);
            ui.Container(Vector2.Zero, new Vector2(AnimatorLayout.FlowListWidth, AnimatorLayout.FlowRowHeight),
                spacing: 4, color: new Color(0, 0, 0, 0), main: StackMainAlignment.Start);

            Entity toggle = ui.Button("",
                new Vector2(AnimatorLayout.FlowToggleWidth, AnimatorLayout.FlowRowHeight),
                fontSize: UiTheme.SmallFontSize);
            toggle.AddComponent(new ToolAction(ToolActionKind.AllowTransition, clip.Id));
            AnimatorTooltips.Attach(toggle, ToolActionKind.AllowTransition, clip.Id);

            Entity entry = ui.Button(clip.Id.ToUpperInvariant(),
                new Vector2(AnimatorLayout.FlowListWidth - AnimatorLayout.FlowToggleWidth - 4,
                    AnimatorLayout.FlowRowHeight),
                fontSize: UiTheme.SmallFontSize);
            entry.AddComponent(new ToolAction(ToolActionKind.SelectTransition, clip.Id));
            AnimatorTooltips.Attach(entry, ToolActionKind.SelectTransition, clip.Id);
            ui.End();
        }
        SyncRows(list, package, from, game, "");
    }

    /// <summary>
    /// Brings each row up to date in place: rules change while their own
    /// buttons are being pressed, so the rows are only rebuilt for new clips.
    /// </summary>
    private static void SyncRows(
        Entity list,
        XafPackage package,
        string from,
        List<(string From, string To)> game,
        string target)
    {
        bool open = XafTransitions.IsOpen(package);
        foreach (Entity row in list.ChildEntities)
        {
            foreach (Entity button in row.ChildEntities)
            {
                if (!button.TryGetComponent(out ToolAction action))
                    continue;

                string to = action.Argument;
                XafTransition? rule = XafTransitions.Find(package, from, to);
                bool usedByGame = game.Contains((from, to));
                if (action.Kind == ToolActionKind.AllowTransition)
                {
                    SetText(button, rule is not null ? "ON" : open ? "ANY" : "OFF");
                    Paint(button, rule is not null ? Allowed : usedByGame && !open ? Missing : UiTheme.Button);
                }
                else if (action.Kind == ToolActionKind.SelectTransition)
                {
                    SetText(button, Label(to, rule, usedByGame));
                    Paint(button, to == target ? Lit : UiTheme.Button);
                }
            }
        }
    }

    /// <summary>A row's text: the clip, its fade and curve when allowed, and a mark when the game asks for it.</summary>
    public static string Label(string clip, XafTransition? rule, bool usedByGame)
    {
        var label = new StringBuilder(clip.ToUpperInvariant());
        if (rule is not null)
            label.Append($"  {rule.Fade:0.00}s {Short(rule.Curve)}");
        if (usedByGame)
            label.Append("  • GAME");
        return label.ToString();
    }

    private static string Short(XafFadeCurve curve) => curve switch
    {
        XafFadeCurve.Smooth => "SMO",
        XafFadeCurve.EaseIn => "IN",
        XafFadeCurve.EaseOut => "OUT",
        _ => "LIN",
    };

    private void SyncReadouts(
        Entity session,
        AnimatorUi ui,
        XafPackage package,
        string from,
        List<(string From, string To)> game)
    {
        AnimatorFlow flow = session.GetComponent<AnimatorFlow>();
        int missing = game.FindAll(pair => pair.From == from && !XafTransitions.Allows(package, pair.From, pair.To)).Count;
        string title = $"FROM {from.ToUpperInvariant()}";
        if (missing > 0)
            title += $"  •  {missing} GAME MISSING";
        SetText(ui.FlowTitle, title);

        XafClip? target = AnimatorFlowCommands.Target(session);
        XafTransition? rule = AnimatorFlowCommands.Rule(session);
        if (target is null)
        {
            SetText(ui.FlowRule, XafTransitions.IsOpen(package)
                ? "NO RULES: ANY CLIP CAN FOLLOW ANY. PICK ONE"
                : "PICK A CLIP TO EDIT ITS FADE");
            SetText(ui.FlowFade, "");
            SetText(ui.FlowGap, "");
        }
        else
        {
            string pair = AnimatorFlowCommands.Pair(from, target.Id);
            SetText(ui.FlowRule, rule is not null
                ? pair
                : XafTransitions.IsOpen(package) ? $"{pair} (OPEN TABLE)" : $"{pair} NOT ALLOWED");
            (float fade, XafFadeCurve curve) = XafTransitions.Blend(package, from, target.Id);
            SetText(ui.FlowFade, rule is not null
                ? $"{fade:0.00} s  {AnimatorFlowCommands.CurveName(curve)}"
                : $"{fade:0.00} s DEFAULT");
            SyncGap(session, ui.FlowGap, fade, curve);
        }

        string curveName = rule?.Curve.ToString() ?? "";
        foreach (Entity button in Query.Store.Query<ToolAction>().ToEntityList())
        {
            // The preview bar paints its own buttons in its own colours.
            if (button.Tags.Has<AnimatorPreviewBarButton>())
                continue;

            ToolAction action = button.GetComponent<ToolAction>();
            switch (action.Kind)
            {
                case ToolActionKind.TransitionCurve:
                    Paint(button, action.Argument == curveName ? Lit : UiTheme.Button);
                    break;
                case ToolActionKind.ToggleFlowLoop:
                    Paint(button, flow.Loop ? Lit : UiTheme.Button);
                    break;
                case ToolActionKind.ToggleSlow:
                    Paint(button, flow.Slow ? Lit : UiTheme.Button);
                    break;
            }
        }
    }

    /// <summary>
    /// Rates the joint jump at the hand-over against the fade that has to
    /// hide it: small jumps read as smooth whatever the fade, big ones pop
    /// unless the fade is long enough to carry them.
    /// </summary>
    private static void SyncGap(Entity session, Entity label, float fade, XafFadeCurve curve)
    {
        if (AnimatorFlowCommands.Gap(session) is not { } gap)
        {
            SetText(label, "");
            return;
        }

        (string rating, Color color) = Rate(gap.Distance, fade, curve);
        string bone = gap.BoneId.Length > 0 ? $" AT {gap.BoneId.ToUpperInvariant()}" : "";
        SetText(label, $"GAP {gap.Distance:0} PX{bone}: {rating}");
        if (label.HasComponent<TextStyle>() && label.GetComponent<TextStyle>().Color != color)
        {
            label.GetComponent<TextStyle>().Color = color;
            label.AddTag<Dirty>();
        }
    }

    /// <summary>
    /// A jump the fade spreads over its length: what moves in one 60 Hz frame
    /// at the curve's steepest is what the eye reads as a pop.
    /// </summary>
    public static (string Rating, Color Color) Rate(float distance, float fade, XafFadeCurve curve)
    {
        if (distance < AnimatorFlowCommands.SmallGap)
            return ("SMOOTH", Palette.Lime);

        float perFrame = distance * PeakSlope(curve) / Mathf.Max(1f, fade * 60f);
        if (perFrame < AnimatorFlowCommands.SmallGap)
            return ("SMOOTH", Palette.Lime);
        return perFrame < AnimatorFlowCommands.LargeGap
            ? ("VISIBLE", Palette.Warning)
            : ("POPS", Palette.Rose);
    }

    /// <summary>The steepest the curve's weight climbs, as a multiple of a linear fade's.</summary>
    private static float PeakSlope(XafFadeCurve curve) => curve switch
    {
        XafFadeCurve.Smooth => 1.5f,
        XafFadeCurve.EaseIn or XafFadeCurve.EaseOut => 2f,
        _ => 1f,
    };

    private static string Signature(XafPackage package, string from)
    {
        var text = new StringBuilder(from).Append('|');
        foreach (XafClip clip in package.Clips)
            text.Append(clip.Id).Append(',');
        return text.ToString();
    }

    private static void Paint(Entity button, Color color)
    {
        if (!button.HasComponent<ButtonVisual>())
            return;

        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == color)
            return;

        visual.Normal = color;
        visual.Hover = color.Lerp(Colors.White, 0.16f);
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
