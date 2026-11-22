using System;
using System.Collections.Generic;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;

namespace Xanadu.Tools.DevTools.Clock;

/// <summary>
/// Clock: how good the song clock is that judging goes by. Plays the test
/// track through the game's audio and plots, for the last seconds, how far the
/// raw cursor (a staircase, one step per chunk the device takes) and the
/// filled-in position judging reads sit from a steady line, against the top
/// judgement window. Below, the numbers for everything since START.
/// </summary>
internal sealed class ClockStage(DevToolsContext context, Handle item, Rect2 area) : CanvasToolStage(context, item, area)
{
    private const float Margin = 40;
    private const double GraphSeconds = 1.0;
    private const double GraphRangeMs = 15;

    /// <summary>The top judgement window, ±ms (Flawless+): a clock's jitter should sit well inside it.</summary>
    private const double TopWindowMs = 8.3;

    private static readonly Color RawColour = Palette.TextDim;
    private static readonly Color FilledColour = Palette.Accent;

    private readonly ClockProbe _probe = new();
    private float _volume = 0.3f;
    private bool _wasDown;
    private ClockReport _report;
    private double _reportAge = double.MaxValue;

    protected override void Tick(float delta, float time)
    {
        bool click = LeftDown && !_wasDown;
        _wasDown = LeftDown;

        Canvas.Clear(Item);
        Canvas.DrawRect(Item, new Rect2(0, 0, Width, Height), Palette.Background);
        Label(new Rect2(Margin, 34, 900, 44), "SONG CLOCK", Palette.Accent, 34);
        Label(new Rect2(Margin, 88, 1500, 30),
            "the clock judging reads: raw = miniaudio's cursor (moves once per device chunk) · filled = cursor + wall time since its chunk, on a steady line through the chunk arrivals",
            Palette.TextDim, 18);

        bool running = _probe.Running;
        if (Button(new Rect2(Margin, 136, 180, 48), running ? "STOP" : "START", running, click))
        {
            if (running)
                _probe.Stop();
            else
                _probe.Start(_volume);
            _reportAge = double.MaxValue;
        }

        if (Button(new Rect2(Margin + 196, 136, 180, 48), _volume > 0 ? "MUTE" : "UNMUTE", _volume == 0, click))
        {
            _volume = _volume > 0 ? 0f : 0.3f;
            _probe.SetVolume(_volume);
        }

        string state = _probe.Error ?? (running ? $"playing {ClockProbe.Track}" : "stopped: START plays the test track and measures");
        Label(new Rect2(Margin + 400, 146, 1200, 30), state, _probe.Error is null ? Palette.TextMuted : Palette.Danger, 20);

        DrawGraph(new Rect2(Margin, 210, Width - Margin * 2, 420));

        _reportAge += delta;
        if (_reportAge > 0.25)
        {
            _reportAge = 0;
            _report = _probe.Report(_probe.Recent(0));
        }

        DrawReport(Margin, 670);
    }

    private void DrawGraph(Rect2 box)
    {
        Canvas.DrawRect(Item, box, Palette.SurfaceDeep);
        float mid = box.GetCenter().Y;
        float Y(double ms) => mid - (float)(Math.Clamp(ms, -GraphRangeMs, GraphRangeMs) / GraphRangeMs) * box.Size.Y / 2f;

        Canvas.DrawRect(Item, new Rect2(box.Position.X, Y(TopWindowMs), box.Size.X, Y(-TopWindowMs) - Y(TopWindowMs)), Palette.Success with { A = 0.08f });
        foreach (double ms in new[] { -TopWindowMs, 0, TopWindowMs })
            Canvas.DrawRect(Item, new Rect2(box.Position.X, Y(ms), box.Size.X, 1), ms == 0 ? Palette.GridLine : Palette.Success with { A = 0.5f });
        Label(new Rect2(box.End.X - 260, Y(TopWindowMs) - 26, 250, 22), $"top window ±{TopWindowMs} ms", Palette.Success, 16, right: true);
        Label(new Rect2(box.Position.X + 10, box.Position.Y + 8, 400, 22), $"+{GraphRangeMs} ms", Palette.TextDim, 16);
        Label(new Rect2(box.Position.X + 10, box.End.Y - 30, 400, 22), $"-{GraphRangeMs} ms   (last {GraphSeconds:0.#} s, off a fitted line)", Palette.TextDim, 16);

        List<ClockSample> samples = _probe.Recent(GraphSeconds);
        if (samples.Count < 3)
            return;

        double from = samples[0].WallMs, span = Math.Max(1, samples[^1].WallMs - from);
        Plot(box, samples, ClockProbe.Residuals(samples, static s => s.RawMs).Residuals, from, span, RawColour, Y);
        Plot(box, samples, ClockProbe.Residuals(samples, static s => s.FilledMs).Residuals, from, span, FilledColour, Y);

        Label(new Rect2(box.Position.X + 10, box.Position.Y + 34, 200, 22), "— raw", RawColour, 18);
        Label(new Rect2(box.Position.X + 110, box.Position.Y + 34, 200, 22), "— filled", FilledColour, 18);
    }

    private void Plot(Rect2 box, List<ClockSample> samples, List<double> residuals, double from, double span, Color colour, Func<double, float> y)
    {
        var points = new Vector2[samples.Count];
        for (int i = 0; i < samples.Count; i++)
            points[i] = new Vector2(box.Position.X + (float)((samples[i].WallMs - from) / span) * box.Size.X, y(residuals[i]));
        Canvas.DrawPolyline(Item, points, colour, 2f);
    }

    private void DrawReport(float x, float y)
    {
        ClockReport r = _report;
        if (r.RawJitter.Count == 0)
        {
            Label(new Rect2(x, y, 1200, 30), "no measurement yet", Palette.TextDim, 20);
            return;
        }

        Label(new Rect2(x, y, 1800, 30),
            $"{r.Seconds:0.0} s measured · device chunk {r.ChunkMs:0.00} ms · clock drift vs wall {r.DriftPpm:+0;-0} ppm",
            Palette.TextMuted, 20);
        string[] heads = ["", "± (p1–p99)", "stdev", "min", "median", "max", "n"];
        float[] columns = [x, x + 360, x + 560, x + 740, x + 920, x + 1100, x + 1280];
        for (int i = 0; i < heads.Length; i++)
            Label(new Rect2(columns[i], y + 50, 170, 26), heads[i], Palette.TextDim, 18);

        Row(columns, y + 84, "raw: off a steady line", r.RawJitter, RawColour, verdict: true);
        Row(columns, y + 118, "filled: off a steady line", r.FilledJitter, FilledColour, verdict: true);
        Row(columns, y + 152, "raw cursor step size", r.RawSteps, Palette.TextMuted, verdict: false);
        Row(columns, y + 186, "wall time between chunks", r.ChunkGaps, Palette.TextMuted, verdict: false);

        Color back = r.Backwards == 0 ? Palette.Success : Palette.Danger;
        Label(new Rect2(x, y + 236, 1800, 26), $"filled clock went backwards {r.Backwards} times", back, 18);
        Label(new Rect2(x, y + 268, 1800, 26),
            "± is how far a perfectly timed press is judged off, from the clock alone (frame sampling of presses comes on top)",
            Palette.TextDim, 16);
    }

    private void Row(float[] columns, float y, string name, Spread s, Color colour, bool verdict)
    {
        Color plus = !verdict ? colour : s.Plus < TopWindowMs / 4 ? Palette.Success : s.Plus < TopWindowMs ? Palette.Warning : Palette.Danger;
        Label(new Rect2(columns[0], y, 340, 26), name, colour, 18);
        Label(new Rect2(columns[1], y, 170, 26), $"±{s.Plus:0.00}", plus, 18);
        Label(new Rect2(columns[2], y, 170, 26), $"{s.Stdev:0.00}", colour, 18);
        Label(new Rect2(columns[3], y, 170, 26), $"{s.Min:0.00}", colour, 18);
        Label(new Rect2(columns[4], y, 170, 26), $"{s.P50:0.00}", colour, 18);
        Label(new Rect2(columns[5], y, 170, 26), $"{s.Max:0.00}", colour, 18);
        Label(new Rect2(columns[6], y, 170, 26), $"{s.Count}", Palette.TextDim, 18);
    }

    private bool Button(Rect2 box, string text, bool on, bool click)
    {
        bool hover = box.HasPoint(Mouse);
        Canvas.DrawRect(Item, box, on ? Palette.PrimarySoft : hover ? Palette.PrimaryMuted with { A = 1f } : Palette.SurfaceRaised);
        Canvas.DrawTextStyled(Item, box, text, Palette.TextBright, 20);
        return hover && click;
    }

    private void Label(Rect2 box, string text, Color colour, int size, bool right = false)
    {
        if (right)
            Canvas.DrawTextStyledRight(Item, box, text, colour, size);
        else
            Canvas.DrawTextStyledLeft(Item, box, text, colour, size);
    }

    public override void Hidden() => _probe.Stop();

    public override void Dispose()
    {
        _probe.Dispose();
        base.Dispose();
    }
}
