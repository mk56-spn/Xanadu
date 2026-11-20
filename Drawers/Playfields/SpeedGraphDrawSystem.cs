using System;
using System.Collections.Generic;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// The F6 speed graph: the play body's x and y speed over the last
/// <see cref="SpeedGraph.WindowMs"/> of song time, in a panel at the bottom left
/// of the screen, with guides at zero and at the three fixed speeds either way.
/// Playfield y points down, so a positive y speed is falling.
/// </summary>
public sealed class SpeedGraphDrawSystem(IRectCanvas canvas, Handle target) : QuerySystem<SpeedGraph, PlayfieldView>
{
    private const float Margin = 24f;
    private const float PanelWidth = 560f;
    private const float PanelHeight = 260f;
    private const float Pad = 12f;
    private const float LabelWidth = 52f;
    private const float TitleHeight = 22f;

    private static readonly Color XColor = DebugPalette.Header;
    private static readonly Color YColor = DebugPalette.Title;

    private static readonly float[] FixedSpeeds =
    {
        NoteRoute.SpeedOf(DashStrength.Single),
        NoteRoute.SpeedOf(DashStrength.Double),
        NoteRoute.SpeedOf(DashStrength.Triple),
    };

    private readonly List<Vector2> _xLine = new();
    private readonly List<Vector2> _yLine = new();

    protected override void OnUpdate()
    {
        canvas.Clear(target);

        // One play entity at most.
        foreach (Entity entity in Query.Entities)
            Draw(entity.GetComponent<SpeedGraph>().Samples, entity.GetComponent<PlayfieldView>().ScreenSize);
    }

    private void Draw(List<SpeedSample> samples, Vector2 screen)
    {
        var panel = new Rect2(Margin, screen.Y - Margin - PanelHeight, PanelWidth, PanelHeight);
        canvas.DrawRect(target, panel, DebugPalette.Panel);
        canvas.DrawRectOutlines(target, [panel], DebugPalette.PanelBorder);

        SpeedSample? last = samples.Count > 0 ? samples[^1] : null;
        Vector2 now = last?.Velocity ?? Vector2.Zero;
        canvas.DrawTextLeft(target, new Rect2(panel.Position.X + Pad, panel.Position.Y + 4, 160, 20), "F6 SPEED", DebugPalette.Title);
        canvas.DrawTextLeft(target, new Rect2(panel.Position.X + 170, panel.Position.Y + 4, 160, 20), "x " + Number(now.X), XColor);
        canvas.DrawTextLeft(target, new Rect2(panel.Position.X + 330, panel.Position.Y + 4, 200, 20), "y " + Number(now.Y) + " (down +)", YColor);

        var plot = new Rect2(
            panel.Position.X + Pad + LabelWidth,
            panel.Position.Y + TitleHeight + Pad,
            PanelWidth - Pad * 2 - LabelWidth,
            PanelHeight - TitleHeight - Pad * 2);

        // Symmetric about zero, wide enough for the fastest fixed speed and anything faster seen.
        float range = FixedSpeeds[^1] * 1.15f;
        foreach (SpeedSample sample in samples)
            range = MathF.Max(range, MathF.Max(MathF.Abs(sample.Velocity.X), MathF.Abs(sample.Velocity.Y)) * 1.1f);

        float Y(float speed) => plot.Position.Y + plot.Size.Y * (0.5f - speed / (2f * range));

        Guide(0f, DebugPalette.TextDim, "0");
        foreach (float speed in FixedSpeeds)
        {
            Color dim = DebugPalette.TextDim with { A = 0.35f };
            Guide(speed, dim, Number(speed));
            Guide(-speed, dim, Number(-speed));
        }

        if (last is not SpeedSample end)
            return;

        double from = end.TimeMs - SpeedGraph.WindowMs;
        _xLine.Clear();
        _yLine.Clear();
        foreach (SpeedSample sample in samples)
        {
            float x = plot.Position.X + plot.Size.X * (float)((sample.TimeMs - from) / SpeedGraph.WindowMs);
            _xLine.Add(new Vector2(x, Y(sample.Velocity.X)));
            _yLine.Add(new Vector2(x, Y(sample.Velocity.Y)));
        }

        if (_xLine.Count >= 2)
        {
            canvas.DrawPolyline(target, _xLine.ToArray(), XColor, 2);
            canvas.DrawPolyline(target, _yLine.ToArray(), YColor, 2);
        }

        void Guide(float speed, Color color, string label)
        {
            float y = Y(speed);
            canvas.DrawLine(target, new Vector2(plot.Position.X, y), new Vector2(plot.Position.X + plot.Size.X, y), color, 1);
            canvas.DrawTextRight(target, new Rect2(panel.Position.X + Pad - 8, y - 10, LabelWidth, 20), label, DebugPalette.TextDim);
        }
    }

    private static string Number(float value) => MathF.Round(value).ToString("0", CultureInfo.InvariantCulture);
}
