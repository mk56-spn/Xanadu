using System.Globalization;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Debug;

/// <summary>
/// The F3 frame breakdown: every part of the frame the host times, a row each,
/// with its milliseconds per frame over the live and average windows the system
/// tree uses. Parts nest by indent; a part includes the ones under it.
/// </summary>
public sealed partial class DebugPressedOverlaySystem
{
    private const float PhaseNameWidth = 300;
    private const float PhaseCellWidth = 80;
    private const float PhaseIndent = 18;

    /// <summary>A part at or above this share of the frame is coloured as a warning.</summary>
    private const float HeavyShare = 0.25f;

    private sealed class PhaseWindow
    {
        public double[] LiveStart = [];
        public double[] AverageStart = [];
        public long LiveFrames;
        public long AverageFrames;
    }

    private readonly PhaseWindow _phaseWindow = new();
    private Entity[] _phaseLive = [];
    private Entity[] _phaseAverage = [];
    private Entity[] _phaseNames = [];
    private float _phaseLiveClock;
    private float _phaseAverageClock;

    private void BuildPhases(UiNode panel)
    {
        if (_phases is null)
            return;

        int count = _phases.Phases.Count;
        panel.Text("WHERE THE FRAME GOES", UiType.Caption, DebugPalette.Header)
            .Size(PhaseNameWidth + PhaseCellWidth * 2, CaptionHeight);
        UiNode header = panel.Row().Main(StackMainAlignment.Start);
        header.Text("part", FontSize, DebugPalette.Header).Size(PhaseNameWidth, DebugTable.RowHeight);
        header.Text("live ms", FontSize, DebugPalette.Header, TextAlign.Right).Size(PhaseCellWidth, DebugTable.RowHeight);
        header.Text("avg ms", FontSize, DebugPalette.Header, TextAlign.Right).Size(PhaseCellWidth, DebugTable.RowHeight);

        _phaseNames = new Entity[count];
        _phaseLive = new Entity[count];
        _phaseAverage = new Entity[count];
        for (int i = 0; i < count; i++)
        {
            FramePhase phase = _phases.Phases[i];
            UiNode row = panel.Row().Main(StackMainAlignment.Start);
            float indent = phase.Depth * PhaseIndent;
            if (indent > 0)
                row.Text("", FontSize, DebugPalette.Text).Size(indent, DebugTable.RowHeight);
            _phaseNames[i] = row.Text(phase.Name, FontSize, DebugPalette.Text)
                .Size(PhaseNameWidth - indent, DebugTable.RowHeight).Entity;
            _phaseLive[i] = row.Text("-", FontSize, DebugPalette.Text, TextAlign.Right)
                .Size(PhaseCellWidth, DebugTable.RowHeight).Entity;
            _phaseAverage[i] = row.Text("-", FontSize, DebugPalette.Text, TextAlign.Right)
                .Size(PhaseCellWidth, DebugTable.RowHeight).Entity;
        }

        _phaseWindow.LiveStart = Snapshot();
        _phaseWindow.AverageStart = Snapshot();
        _phaseWindow.LiveFrames = _phaseWindow.AverageFrames = _phases.Frames;
        _phaseLiveClock = _phaseAverageClock = 0;
    }

    private double[] Snapshot()
    {
        var totals = new double[_phases!.Phases.Count];
        for (int i = 0; i < totals.Length; i++)
            totals[i] = _phases.TotalMs(i);
        return totals;
    }

    private void RefreshPhases(float delta)
    {
        if (_phases is null || _phaseLive.Length == 0)
            return;

        _phaseLiveClock += delta;
        _phaseAverageClock += delta;
        if (_phaseLiveClock >= LiveWindow)
        {
            _phaseLiveClock = 0;
            Close(_phaseLive, ref _phaseWindow.LiveStart, ref _phaseWindow.LiveFrames, colour: false);
        }

        if (_phaseAverageClock >= AverageWindow)
        {
            _phaseAverageClock = 0;
            Close(_phaseAverage, ref _phaseWindow.AverageStart, ref _phaseWindow.AverageFrames, colour: true);
        }
    }

    /// <summary>Writes each part's mean per frame since the window opened, then opens the next one.</summary>
    private void Close(Entity[] cells, ref double[] start, ref long startFrames, bool colour)
    {
        double[] now = Snapshot();
        long frames = _phases!.Frames - startFrames;
        double frameMs = 0;
        for (int i = 0; i < now.Length; i++)
        {
            if (_phases.Phases[i].Depth == 0)
                frameMs += now[i] - start[i];
        }

        for (int i = 0; i < now.Length; i++)
        {
            double ms = frames > 0 ? (now[i] - start[i]) / frames : double.NaN;
            SetLine(cells[i], double.IsNaN(ms) ? "-" : ms.ToString("0.00", CultureInfo.InvariantCulture));
            if (!colour)
                continue;

            // Heavy parts stand out against the whole frame.
            bool heavy = frameMs > 0 && (now[i] - start[i]) / frameMs >= HeavyShare && _phases.Phases[i].Depth > 0;
            Color ink = heavy ? DebugPalette.Warning : _phases.Phases[i].Depth == 0 ? DebugPalette.Header : DebugPalette.Text;
            UiText.SetColor(_phaseNames[i], ink);
            UiText.SetColor(_phaseLive[i], ink);
            UiText.SetColor(_phaseAverage[i], ink);
        }

        start = now;
        startFrames = _phases.Frames;
    }
}
