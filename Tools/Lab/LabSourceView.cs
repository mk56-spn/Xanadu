using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Lab;

/// <summary>
/// A read-only, scrollable listing of a source file: one text row per line
/// with a dim line number. Nothing in it takes text input, so it cannot be edited.
/// </summary>
public static class LabSourceView
{
    private const int FontSize = UiType.Caption;
    private const float LineGap = UiMetrics.Space.Xxs;
    private const string TabSpaces = "    ";

    /// <summary>Height of the file label above the listing.</summary>
    public static float HeaderHeight => UiNode.Measure("A", UiType.Small).Y;

    /// <summary>Adds the file label and a <paramref name="size"/> listing of <paramref name="path"/> to <paramref name="parent"/>.</summary>
    public static void Build(UiNode parent, string path, Vector2 size)
    {
        parent.Text(path.Replace("res://", ""), UiType.Small, Palette.TextDim);

        string[] lines = Read(path);
        float lineHeight = UiNode.MeasureMono("A", FontSize).Y;
        float contentHeight = lines.Length * (lineHeight + LineGap) + UiMetrics.Space.Sm * 2;
        float numberWidth = UiNode.MeasureMono(lines.Length.ToString(), FontSize).X;

        UiNode viewport = parent.Scroll(size, Mathf.Max(contentHeight, size.Y), out UiNode content)
            .Fill(Palette.SurfaceInset)
            .Border(Palette.BorderStrong);
        content.Gap(LineGap).Pad(UiMetrics.Space.Sm)
            .Main(StackMainAlignment.Start).Cross(StackCrossAlignment.Start);

        for (int i = 0; i < lines.Length; i++)
        {
            UiNode row = content.Row().Gap(UiMetrics.Space.Md);
            row.Text((i + 1).ToString(), FontSize, Palette.TextDim, TextAlign.Right).Mono().Width(numberWidth);
            row.Text(lines[i], FontSize, Palette.Text).Mono();
        }
    }

    private static string[] Read(string path)
    {
        if (!FileAccess.FileExists(path))
            return [$"missing: {path}"];

        string text = FileAccess.GetFileAsString(path).Replace("\r", "").TrimEnd('\n');
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Replace("\t", TabSpaces);
            // An empty label measures zero tall and would collapse the row.
            lines[i] = line.Length == 0 ? " " : line;
        }
        return lines;
    }
}
