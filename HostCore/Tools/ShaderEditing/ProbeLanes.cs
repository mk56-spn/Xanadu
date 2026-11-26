using System.Numerics;
using System.Runtime.InteropServices;
using Xanadu.Core.Theme;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Color = Xanadu.Core.Maths.Color;
using Rect2 = Xanadu.Core.Maths.Rect2;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The motion the open stack's probe nodes measured (<see cref="ProbeNodes"/>), under the clips: a row for each draw
/// whose copies move over the window, a band in it for each channel they move, every copy's curve in it. The rows are a
/// graph of their own (<see cref="Shader"/>) reading the probes on the GPU and drawing their curves (x cyan, y pink, turn
/// gold, scale lime), then a row for each shade whose colours change (<see cref="ColourProbes"/>), drawn as strips of what it painted;
/// then a row for each number the stack keeps that changed over the last frames (<see cref="RecordProbes"/>), recorded, so
/// it replays rather than scrubs; the CPU draws the header and the labels, from the rows the graph reports back a few
/// frames late.
/// </summary>
internal sealed class ProbeLanes(IRectCanvas canvas, Handle labels, Handle curves) : IDisposable
{
    private const string Shader = "Assets/Shaders/Editor/probe_lanes";

    /// <summary>A band of a motion row (one a moving channel; a recorded row is one band), a colour row, the tallest the rows get together.</summary>
    private const float HeaderHeight = 22, BandHeight = 14, RowHeight = 24, MostBody = 360, LabelWidth = 150, Pad = 6;

    /// <summary>The channels, by their bit in a row (x 1, y 2, turn 4, scale 8), and their curves' colours; colour 16 is a colour row.</summary>
    private static readonly string[] Channels = ["x", "y", "turn", "scale"];
    private static readonly Color[] ChannelColours = [Palette.Cyan, Palette.Pink, Palette.Gold, Palette.Lime];

    /// <summary>A recorded row's label and line: set apart from the scrubbable rows.</summary>
    private static readonly Color RecordColour = Palette.Warning;

    private readonly CanvasMaterial _material = new(Shader);
    private const uint ColourRow = 16, RecordedRow = 32;

    private string[] _lanes = [], _colourLanes = [], _recordLanes = [];
    private Rect2 _area, _placed;

    /// <summary>The height it wants: the header, and the rows the GPU reported that fit in <see cref="MostBody"/> (one row's room at least).</summary>
    public float Height => HeaderHeight + Body(Rows).Height + Pad;

    public Rect2 Area { get => _area; set => _area = value; }

    /// <summary>
    /// What each motion, colour and recorded lane stands for in the code as it is now (<see cref="ProbeNodes.Labels"/>,
    /// <see cref="ColourProbes.Labels"/>, <see cref="RecordProbes.Labels"/>).
    /// </summary>
    public void Use(string[] lanes, string[] colourLanes, string[] recordLanes)
    {
        (_lanes, _colourLanes, _recordLanes) = (lanes, colourLanes, recordLanes);
        _material.Set("motionStarts", Starts(lanes));
        _material.Set("colourStarts", Starts(colourLanes));
    }

    /// <summary>The draw a lane's label names: the label without its copy's number (<c>shown 3</c> is <c>shown</c>).</summary>
    private static string DrawOf(string label)
    {
        int space = label.LastIndexOf(' ');
        return space > 0 && label.AsSpan(space + 1).ContainsAnyExceptInRange('0', '9') == false ? label[..space] : label;
    }

    /// <summary>Where each draw's lanes start, a bit a lane, 16 to a component: the lanes' graph folds a draw's copies into one row.</summary>
    private static Vector4 Starts(string[] labels)
    {
        Span<float> bits = stackalloc float[4];
        for (int k = 1; k < Math.Min(labels.Length, 64); k++)
            if (DrawOf(labels[k]) != DrawOf(labels[k - 1]))
                bits[k / 16] += 1 << (k % 16);
        return new Vector4(bits[0], bits[1], bits[2], bits[3]);
    }

    /// <summary>A row's height, as the lanes' graph works it out (<c>tall</c>): a band a moving channel, colour rows their own.</summary>
    private static float Tall(uint row)
    {
        uint kind = row >> 8 & 0xFF;
        return (kind & ColourRow) != 0 ? RowHeight : 4 + Math.Max(BitOperations.PopCount(kind & 15), 1) * BandHeight;
    }

    /// <summary>How many of the rows fit in <see cref="MostBody"/> (one at least), and their height (a colour row's when there are none).</summary>
    private static (int Count, float Height) Body(ReadOnlySpan<uint> rows)
    {
        float height = 0;
        int count = 0;
        while (count < rows.Length && (count == 0 || height + Tall(rows[count]) <= MostBody))
            height += Tall(rows[count++]);
        return (count, count == 0 ? RowHeight : height);
    }

    private bool Probing => _lanes.Length + _colourLanes.Length + _recordLanes.Length > 0;

    /// <summary>What the clip probe last reported (<see cref="ClipProbes.Read"/>), as bytes; empty before the first report.</summary>
    public ReadOnlySpan<byte> ClipReport => _material.Reports.TryGetValue(ClipProbes.List, out byte[]? bytes) ? bytes : [];

    /// <summary>The rows reported: a lane number, then its moving channels times 256.</summary>
    private ReadOnlySpan<uint> Rows
    {
        get
        {
            if (!Probing || !_material.Reports.TryGetValue("rows", out byte[]? bytes))
                return [];
            ReadOnlySpan<uint> rows = MemoryMarshal.Cast<byte, uint>(bytes);
            return rows.Length == 0 ? [] : rows.Slice(1, (int)Math.Min(rows[0], (uint)rows.Length - 1));
        }
    }

    /// <summary>Draws the panel over <paramref name="window"/> (start and length, seconds), the playhead at <paramref name="seconds"/>.</summary>
    public void Draw((double Start, double Length) window, double seconds)
    {
        canvas.Clear(labels);
        // The lanes' graph runs whenever there is room, even with no lanes: it reports the clips too.
        bool room = _area.Size.X > LabelWidth, probing = Probing && room;
        CanvasServer.Current.SetVisible(curves, room);
        if (_area.Size.X <= 0)
            return;
        canvas.DrawRect(labels, _area, Palette.SurfaceDeep);
        canvas.DrawRect(labels, new Rect2(_area.Position, new Vector2(_area.Size.X, HeaderHeight)), Palette.Surface);
        var header = new Rect2(_area.Position.X + 8, _area.Position.Y, _area.Size.X - 16, HeaderHeight);
        canvas.DrawTextStyledLeft(labels, header, "MOTION", Palette.TextDim, 12);
        if (!probing)
        {
            canvas.DrawTextStyledLeft(labels, Under(header), "Nothing to probe: no shade or draw the probe can run.", Palette.TextMuted, 13);
            if (room)
                Place(new Rect2(_area.Position.X + LabelWidth, _area.Position.Y + _area.Size.Y - 1, _area.Size.X - LabelWidth - 4, 1));
            return;
        }

        // The key: each channel's name in its curve's colour, then the window.
        float x = LabelWidth - 8;
        for (int k = 0; k < Channels.Length; k++, x += Channels[k - 1].Length * 7 + 14)
            canvas.DrawTextStyledLeft(labels, header with { Position = header.Position + new Vector2(x, 0) }, Channels[k], ChannelColours[k], 12);
        canvas.DrawTextStyledLeft(labels, header with { Position = header.Position + new Vector2(x + 8, 0) },
            $"{window.Start:0.#} to {window.Start + window.Length:0.#} s", Palette.TextMuted, 12);
        ReadOnlySpan<uint> rows = Rows;
        foreach (uint row in rows)
        {
            if ((row >> 8 & RecordedRow) == 0)
                continue;
            canvas.DrawTextStyledLeft(labels, header with { Position = header.Position + new Vector2(x + 110, 0) },
                $"recorded: last {RecordProbes.Frames} frames", RecordColour, 12);
            break;
        }

        if (rows.IsEmpty)
            canvas.DrawTextStyledLeft(labels, Under(header), "Nothing moves or changes colour over this window.", Palette.TextMuted, 13);
        (int shown, float body) = Body(rows);
        float top = _area.Position.Y + HeaderHeight;
        for (int r = 0; r < shown; r++)
        {
            uint lane = rows[r] & 0xFF, kind = rows[r] >> 8 & 0xFF, folded = rows[r] >> 24;
            bool recorded = (kind & RecordedRow) != 0;
            string[] named = recorded ? _recordLanes : (kind & ColourRow) != 0 ? _colourLanes : _lanes;
            string name = lane < named.Length ? named[lane] : $"lane {lane}";
            // A draw's copies folded into one row: the draw, and how many of its copies change.
            if (folded > 0)
                name = recorded ? $"{name} +{folded}" : $"{DrawOf(name)} ×{folded + 1}";
            canvas.DrawTextMono(labels, new Rect2(_area.Position.X + 8, top, LabelWidth - 12, Math.Min(Tall(rows[r]), RowHeight)), name,
                recorded ? RecordColour : Palette.Text, 12, TextAlign.Left);
            top += Tall(rows[r]);
        }

        Place(new Rect2(_area.Position.X + LabelWidth, _area.Position.Y + HeaderHeight, _area.Size.X - LabelWidth - 4, body));
        _material.Set("rowHeight", RowHeight);
        _material.Set("bandHeight", BandHeight);
        _material.Set("now", (float)Math.Clamp((seconds - window.Start) / Math.Max(window.Length, 1e-3), 0, 1));
    }

    /// <summary>The row under the header, for a note in place of the lanes.</summary>
    private static Rect2 Under(Rect2 header) => header with { Position = header.Position + new Vector2(0, HeaderHeight) };

    /// <summary>Puts the curves' rect where the rows go, with the material's colours; only when it moved.</summary>
    private void Place(Rect2 body)
    {
        if (body == _placed)
            return;
        _placed = body;
        CanvasServer.Current.SetMaterial(curves, _material);
        CanvasServer.Current.Clear(curves);
        CanvasServer.Current.AddRect(curves, body, Colors.White);
        _material.Set("backing", Palette.SurfaceDeep);
        _material.Set("stripe", Palette.SurfaceInset);
        _material.Set("playhead", Palette.TextBright);
        string[] names = ["xLine", "yLine", "turnLine", "scaleLine"];
        for (int k = 0; k < names.Length; k++)
            _material.Set(names[k], ChannelColours[k]);
        _material.Set("recordLine", RecordColour);
    }

    public void Dispose()
    {
        CanvasServer.Current.Free(curves);
        CanvasServer.Current.Free(labels);
    }
}
