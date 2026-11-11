using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Strings;
using Xanadu.Systems.Playfields;
using Xanadu.Systems.Scenes;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>
/// The global offset calibration screen: a click track to tap along to, each tap
/// plotted on the judgement bands as it would score in play, and the offset that
/// centres them. The scene builds the handles; the system drives the run.
/// </summary>
public static class OffsetWizardScene
{
    /// <summary>Pixels per millisecond of the plot, so the Flawless window is wide enough to read.</summary>
    public const float PlotPixelsPerMs = 3f;

    private const float PanelWidth = 1100;
    private const float PanelHeight = 760;
    private const float TextWidth = 1000;
    private const float LineHeight = 30;
    private const float ResultHeight = 92;
    private const float BandHeight = 44;
    private const float MarkWidth = 2;
    private const float ResultMarkWidth = 4;
    private const float MarkHeight = 64;
    private const float ActionWidth = 180;
    private const float ActionHeight = 46;

    private static float PlotWidth => PlotHalfRangeMs * 2f * PlotPixelsPerMs + MarkWidth;

    public static float PlotHalfRangeMs => (float)ScoreRules.JudgementDeviation(Judgement.Miss);

    public static void Build(
        EntityStore entities,
        Entity root,
        IVisualRidFactory? visuals = null)
    {
        UiStage stage = UiStage.Full;
        UiNode ui = UiNode.Layer(entities, root, UiLayers.Menu, visuals, stage);

        ui.Rect(new Color(0.01f, 0.02f, 0.04f, 0.82f)).Cover().Below().Input();

        UiNode panel = ui.Column()
            .Size(PanelWidth, PanelHeight)
            .At(stage.Center)
            .Fill(new Color(0.035f, 0.05f, 0.09f, 0.98f))
            .Pad(UiMetrics.Space.Xl)
            .Gap(UiMetrics.Space.Md)
            .Main(StackMainAlignment.Center)
            .Cross(StackCrossAlignment.Center);

        panel.Text(
                LocalizedText.Get(OffsetWizardStrings.Title),
                UiType.Heading,
                SceneUi.AccentColor,
                TextAlign.Center)
            .Bold();
        panel.Text(
                $"Tap SPACE with the clicks, by ear, for {OffsetCalibration.TotalBeats} clicks. Look away if it helps.",
                UiType.Body,
                SceneUi.DimTextColor,
                TextAlign.Center)
            .Size(TextWidth, LineHeight);

        UiNode result = panel.Text("", UiType.Display, SceneUi.TextColor, TextAlign.Center)
            .Size(TextWidth, ResultHeight)
            .Bold();
        UiNode status = panel.Text("", UiType.Body, SceneUi.DimTextColor, TextAlign.Center)
            .Size(TextWidth, LineHeight);

        UiNode plot = Bands(panel);
        UiNode sides = panel.Row().Size(PlotWidth, LineHeight);
        sides.Text("EARLY", UiType.Caption, SceneUi.DimTextColor, TextAlign.Left).Size(PlotWidth / 2f, LineHeight);
        sides.Text("LATE", UiType.Caption, SceneUi.DimTextColor, TextAlign.Right).Size(PlotWidth / 2f, LineHeight);

        UiNode actions = panel.Row().Gap(UiMetrics.Space.Xl);
        UiNode start = actions.Button("START", UiType.Label, SceneUi.ButtonColor)
            .Size(ActionWidth, ActionHeight);
        UiNode apply = actions.Button("APPLY", UiType.Label, SceneUi.ButtonColor)
            .Size(ActionWidth, ActionHeight);
        actions.Button("BACK", UiType.Label, SceneUi.MutedButtonColor)
            .Size(ActionWidth, ActionHeight)
            .On(SceneActionId.Back);

        UiNode manual = panel.Row()
            .Size(TextWidth, ActionHeight)
            .Gap(UiMetrics.Space.Md)
            .Main(StackMainAlignment.Center);
        UiNode offset = manual.Text("", UiType.Section, SceneUi.TextColor, TextAlign.Center)
            .Size(420, ActionHeight);
        UiNode down = manual.Button("-", UiType.Label, SceneUi.MutedButtonColor)
            .Size(ActionHeight, ActionHeight);
        UiNode up = manual.Button("+", UiType.Label, SceneUi.MutedButtonColor)
            .Size(ActionHeight, ActionHeight);
        UiNode reset = manual.Button("RESET", UiType.Label, SceneUi.MutedButtonColor)
            .Size(ActionWidth, ActionHeight);

        // Marks hang off the bands, placed relative to their centre, outside the row's stack.
        var marks = new Entity[OffsetCalibration.MeasuredBeats];
        for (int i = 0; i < marks.Length; i++)
        {
            marks[i] = plot.Rect(new Color(1, 1, 1, 0.75f))
                .Size(MarkWidth, MarkHeight)
                .IgnoreLayout()
                .Hidden()
                .Entity;
        }

        Entity resultMark = plot.Rect(SceneUi.AccentColor)
            .Size(ResultMarkWidth, MarkHeight + 16)
            .IgnoreLayout()
            .Hidden()
            .Entity;

        panel.With(new OffsetWizard(
            offset, result, status, plot, marks, resultMark, start, apply, reset, down, up));
    }

    /// <summary>The judgement windows as they are in play: Flawless+ in the middle, Miss at the ends.</summary>
    private static UiNode Bands(UiNode panel)
    {
        var windows = new List<(Judgement Judgement, float Width)>();
        double inner = 0;
        foreach (Judgement judgement in new[]
                 {
                     Judgement.FlawlessP, Judgement.Flawless, Judgement.Clean, Judgement.Fair,
                     Judgement.Deficient, Judgement.Terrible, Judgement.Miss,
                 })
        {
            double outer = ScoreRules.JudgementDeviation(judgement);
            windows.Add((judgement, (float)(outer - inner) * PlotPixelsPerMs));
            inner = outer;
        }

        UiNode row = panel.Row().Size(PlotWidth, BandHeight);
        for (int i = windows.Count - 1; i >= 0; i--)
            Band(row, windows[i]);
        row.Rect(Colors.White).Size(MarkWidth, BandHeight);
        for (int i = 0; i < windows.Count; i++)
            Band(row, windows[i]);
        return row;
    }

    private static void Band(UiNode row, (Judgement Judgement, float Width) window)
    {
        Color color = PlayScoreSystem.ColorFor(window.Judgement);
        color.A = 0.82f;
        row.Rect(color).Size(window.Width, BandHeight);
    }
}
