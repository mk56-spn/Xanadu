using System;
using Xanadu.Ecs;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Timeline;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The song on the timeline: its bass, middle and highs overlaid around a centre line.
/// One quad over the ruler whose shader (<see cref="Shader"/>) works out every
/// column; the bands' running sums go up to the GPU once per song.
/// </summary>
public sealed class EditorWaveformDrawer(EditorDrawSurface surface)
{
    public const string Shader = "res://Assets/Shaders/Editor/waveform.slang";

    private const float ColumnWidth = 2f;
    private const float Margin = 4f;

    /// <summary>Bass is drawn full height; the higher bands shorter, so all three read at once.</summary>
    private const float MidScale = 0.62f;
    private const float HighScale = 0.34f;

    /// <summary>Texels a row of the sums texture, inside any GPU's smallest texture limit.</summary>
    private const int RowTexels = 4096;

    private WaveformData? _uploaded;
    private Handle _sums;

    public void Draw(Rect2 ruler, WaveformPreview preview, long windowStart, long windowEnd)
    {
        if (preview.Data is not { DurationMs: > 0, Steps: > 0 } data || windowEnd <= windowStart)
            return;

        Handle sums = Sums(data);
        if (sums.Id == 0)
            return;

        // Columns sit on fixed multiples of time, so the shape holds still as the window scrolls past it.
        double msPerColumn = (windowEnd - windowStart) * ColumnWidth / (double)ruler.Size.X;
        IRectCanvas canvas = surface.Canvas;
        canvas.SelectCommonShader(Shader);
        canvas.NewRun();
        // Drawn ahead of its clock, so a hit shows where a chart would place it.
        canvas.SetShaderValue("wave_window", new Vector4(windowStart, windowEnd, (float)msPerColumn, (float)ChartTiming.AudioLagMs), 4);
        canvas.SetShaderValue("wave_data", new Vector4((float)data.Steps / data.DurationMs, data.Steps, RowTexels, 0), 4);
        canvas.SetShaderValue("wave_box", new Vector4(ruler.Size.Y, ruler.Size.Y / 2f - Margin, MidScale, HighScale), 4);
        canvas.SetShaderValue("wave_low", AsVector(Palette.WaveLow), 4);
        canvas.SetShaderValue("wave_mid", AsVector(Palette.WaveMid), 4);
        canvas.SetShaderValue("wave_high", AsVector(Palette.WaveHigh), 4);
        canvas.DrawTexture(surface.Target, ruler, sums);
        canvas.SelectCommonShader("");
    }

    /// <summary>The song's running sums on the GPU, made when the song changes.</summary>
    private Handle Sums(WaveformData data)
    {
        if (ReferenceEquals(data, _uploaded))
            return _sums;

        if (_sums.Id != 0)
            TextureServer.Current.Free(_sums);

        double[] low = data.RunningSums(WaveformBand.Low);
        double[] mid = data.RunningSums(WaveformBand.Mid);
        double[] high = data.RunningSums(WaveformBand.High);
        int rows = (low.Length + RowTexels - 1) / RowTexels;
        var values = new float[rows * RowTexels * 4];
        for (int i = 0; i < low.Length; i++)
        {
            values[i * 4] = (float)low[i];
            values[i * 4 + 1] = (float)mid[i];
            values[i * 4 + 2] = (float)high[i];
        }

        _sums = TextureServer.Current.CreateFloat(RowTexels, rows, values);
        _uploaded = data;
        return _sums;
    }

    private static Vector4 AsVector(Color color) => new(color.R, color.G, color.B, color.A);
}
