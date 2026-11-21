using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Scoring;
using Xanadu.Logic.Calibration;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Screens.Play;
using Xanadu.Screens.Shared;
using Position = Xanadu.Core.Position;

namespace Xanadu.Screens.Calibration;

/// <summary>
/// The global offset calibration screen: a click track to tap along to, each tap
/// plotted on the judgement bands as it would score in play, and the offset that
/// centres them. Everything shown is redrawn from <see cref="OffsetWizardLogic"/>.
/// </summary>
public sealed class OffsetWizardLayout : IScreenLayout<OffsetWizardLogic>
{
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

    private static float PlotWidth => CalibrationPlot.HalfRangeMs * 2f * CalibrationPlot.PixelsPerMs + MarkWidth;

    private UiNode _offset, _result, _status, _apply;
    private Entity[] _marks = [];
    private Entity _resultMark;

    /// <summary>What was last drawn, so a frame with nothing new writes nothing.</summary>
    private (int Taps, long Offset, OffsetWizardPhase Phase, bool Listening)? _shown;

    public string Name => "CLASSIC";

    public void Build(UiNode layer, OffsetWizardLogic logic)
    {
        UiStage stage = UiStage.Full;
        layer.Rect(new Color(0.01f, 0.02f, 0.04f, 0.82f)).Cover().Below().Input();

        UiNode panel = layer.Column()
            .Size(PanelWidth, PanelHeight)
            .At(stage.Center)
            .Fill(new Color(0.035f, 0.05f, 0.09f, 0.98f))
            .Pad(UiMetrics.Space.Xl)
            .Gap(UiMetrics.Space.Md)
            .Main(StackMainAlignment.Center)
            .Cross(StackCrossAlignment.Center);

        panel.Text(LocalizedText.Get(OffsetWizardStrings.Title), UiType.Heading, SceneUi.AccentColor, TextAlign.Center)
            .Bold();
        panel.Text(
                $"Tap SPACE with the clicks, by ear, for {OffsetCalibration.TotalBeats} clicks. Look away if it helps.",
                UiType.Body, SceneUi.DimTextColor, TextAlign.Center)
            .Size(TextWidth, LineHeight);

        _result = panel.Text("", UiType.Display, SceneUi.TextColor, TextAlign.Center).Size(TextWidth, ResultHeight).Bold();
        _status = panel.Text("", UiType.Body, SceneUi.DimTextColor, TextAlign.Center).Size(TextWidth, LineHeight);

        UiNode plot = Bands(panel);
        UiNode sides = panel.Row().Size(PlotWidth, LineHeight);
        sides.Text("EARLY", UiType.Caption, SceneUi.DimTextColor, TextAlign.Left).Size(PlotWidth / 2f, LineHeight);
        sides.Text("LATE", UiType.Caption, SceneUi.DimTextColor, TextAlign.Right).Size(PlotWidth / 2f, LineHeight);

        UiNode actions = panel.Row().Gap(UiMetrics.Space.Xl);
        actions.Button("START", UiType.Label, SceneUi.ButtonColor).Size(ActionWidth, ActionHeight)
            .With(UiCommand.Of(OffsetWizardEntrance.Start));
        _apply = actions.Button("APPLY", UiType.Label, SceneUi.ButtonColor).Size(ActionWidth, ActionHeight)
            .With(UiCommand.Of(OffsetWizardEntrance.Apply));
        actions.Button("BACK", UiType.Label, SceneUi.MutedButtonColor).Size(ActionWidth, ActionHeight)
            .With(UiCommand.Of(OffsetWizardEntrance.Back));

        UiNode manual = panel.Row().Size(TextWidth, ActionHeight).Gap(UiMetrics.Space.Md).Main(StackMainAlignment.Center);
        _offset = manual.Text("", UiType.Section, SceneUi.TextColor, TextAlign.Center).Size(420, ActionHeight);
        manual.Button("-", UiType.Label, SceneUi.MutedButtonColor).Size(ActionHeight, ActionHeight)
            .With(UiCommand.Of(OffsetWizardEntrance.Nudge, -1));
        manual.Button("+", UiType.Label, SceneUi.MutedButtonColor).Size(ActionHeight, ActionHeight)
            .With(UiCommand.Of(OffsetWizardEntrance.Nudge, 1));
        manual.Button("RESET", UiType.Label, SceneUi.MutedButtonColor).Size(ActionWidth, ActionHeight)
            .With(UiCommand.Of(OffsetWizardEntrance.Reset));

        // Marks hang off the bands, placed relative to their centre, outside the row's stack.
        _marks = new Entity[OffsetCalibration.MeasuredBeats];
        for (int i = 0; i < _marks.Length; i++)
            _marks[i] = plot.Rect(new Color(1, 1, 1, 0.75f)).Size(MarkWidth, MarkHeight).IgnoreLayout().Hidden().Entity;

        _resultMark = plot.Rect(SceneUi.AccentColor).Size(ResultMarkWidth, MarkHeight + 16).IgnoreLayout().Hidden().Entity;
    }

    public void Refresh(OffsetWizardLogic logic)
    {
        List<double> taps = logic.Taps.ToList();
        var now = (taps.Count, logic.OffsetMs, logic.Phase, logic.Listening);
        if (_shown == now)
            return;
        _shown = now;

        _offset.SetText($"GLOBAL OFFSET   {logic.OffsetMs:+0;-0;0} ms");
        _status.SetText(Status(logic, taps.Count));
        _apply.Disabled(!logic.CanApply);

        int mark = 0;
        foreach (double tap in taps)
            Place(_marks[mark++], tap, logic.OffsetMs);
        for (; mark < _marks.Length; mark++)
            UiLayout.SetHidden(_marks[mark], true);

        if (logic.Measurement is { } result)
        {
            Place(_resultMark, result.OffsetMs, logic.OffsetMs);
            _result.SetText($"{result.OffsetMs:+0;-0;0} ms");
        }
        else
        {
            UiLayout.SetHidden(_resultMark, true);
            _result.SetText("");
        }
    }

    /// <summary>The run's phase in words.</summary>
    private static string Status(OffsetWizardLogic logic, int taps)
    {
        switch (logic.Phase)
        {
            case OffsetWizardPhase.MissingTrack:
                return "Click track is missing.";
            case OffsetWizardPhase.CountIn:
                return "Count in... find the beat.";
            case OffsetWizardPhase.Tapping:
                return $"{taps} / {OffsetCalibration.MeasuredBeats} taps";
            case OffsetWizardPhase.Applied:
                return "Applied. Run again: your taps should now sit on the white line.";
            case OffsetWizardPhase.TooFewTaps:
                return $"Only {logic.Measurement?.Used ?? 0} steady taps, {OffsetCalibration.MinimumTaps} needed. Press SPACE to try again.";
            case OffsetWizardPhase.Measured when logic.Measurement is { } result:
                string steadiness = result.SpreadMs > OffsetWizardLogic.ShakySpreadMs ? "Unsteady, so try again if you can" : "Steady";
                string strays = result.Rejected > 0 ? $", {result.Rejected} stray left out" : "";
                return $"{steadiness}: {result.Used} taps{strays}, good to about {result.UncertaintyMs:0.0} ms. APPLY it, or run again to check.";
            default:
                return "Press SPACE or START when you are ready.";
        }
    }

    private static void Place(Entity mark, double offsetMs, long savedMs)
    {
        float error = Math.Clamp((float)(offsetMs - savedMs), -CalibrationPlot.HalfRangeMs, CalibrationPlot.HalfRangeMs);
        mark.GetComponent<Position>().Value = new Vector2(error * CalibrationPlot.PixelsPerMs, 0f);
        mark.AddTag<Moved>();
        UiLayout.SetHidden(mark, false);
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
            windows.Add((judgement, (float)(outer - inner) * CalibrationPlot.PixelsPerMs));
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
