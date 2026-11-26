using System.Runtime.InteropServices;
using Xanadu.Core.Theme;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Color = Xanadu.Core.Maths.Color;
using Rect2 = Xanadu.Core.Maths.Rect2;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The motion the open stack's probe nodes measured (<see cref="ProbeNodes"/>), under the clips: a row for each copy of a
/// draw that moves over the window, unlike the rows above it. The rows are a graph of their own
/// (<see cref="Shader"/>) reading the probes on the GPU and drawing their curves (x cyan, y pink, turn gold, scale lime),
/// then a row for each shade whose colours change (<see cref="ColourProbes"/>), drawn as strips of what it painted;
/// the CPU draws the header and the labels, from the rows the graph reports back a few frames late.
/// </summary>
internal sealed class ProbeLanes(IRectCanvas canvas, Handle labels, Handle curves) : IDisposable
{
    private const string Shader = "Assets/Shaders/Editor/probe_lanes";

    private const float HeaderHeight = 22, RowHeight = 18, LabelWidth = 120, Pad = 6;
    private const int MostRows = 12;

    /// <summary>The channels, by their bit in a row (x 1, y 2, turn 4, scale 8), and their curves' colours; colour 16 is a colour row.</summary>
    private static readonly string[] Channels = ["x", "y", "turn", "scale"];
    private static readonly Color[] ChannelColours = [Palette.Cyan, Palette.Pink, Palette.Gold, Palette.Lime];

    private readonly CanvasMaterial _material = new(Shader);
    private const uint ColourRow = 16;

    private string[] _lanes = [], _colourLanes = [];
    private Rect2 _area, _placed;

    /// <summary>The height it wants: the header, and a row for each moving lane the GPU reported (one at least, up to <see cref="MostRows"/>).</summary>
    public float Height => HeaderHeight + Math.Clamp(Rows.Length, 1, MostRows) * RowHeight + Pad;

    public Rect2 Area { get => _area; set => _area = value; }

    /// <summary>What each motion and colour lane stands for in the code as it is now (<see cref="ProbeNodes.Labels"/>, <see cref="ColourProbes.Labels"/>).</summary>
    public void Use(string[] lanes, string[] colourLanes) => (_lanes, _colourLanes) = (lanes, colourLanes);

    private bool Probing => _lanes.Length + _colourLanes.Length > 0;

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
        if (rows.IsEmpty)
            canvas.DrawTextStyledLeft(labels, Under(header), "Nothing moves or changes colour over this window.", Palette.TextMuted, 13);
        for (int r = 0; r < Math.Min(rows.Length, MostRows); r++)
        {
            uint lane = rows[r] & 0xFF;
            string[] named = (rows[r] >> 8 & ColourRow) != 0 ? _colourLanes : _lanes;
            var label = new Rect2(_area.Position.X + 8, _area.Position.Y + HeaderHeight + r * RowHeight, LabelWidth - 12, RowHeight);
            canvas.DrawTextMono(labels, label, lane < named.Length ? named[lane] : $"lane {lane}", Palette.Text, 12, TextAlign.Left);
        }

        Place(new Rect2(_area.Position.X + LabelWidth, _area.Position.Y + HeaderHeight, _area.Size.X - LabelWidth - 4,
            Math.Clamp(rows.Length, 1, MostRows) * RowHeight));
        _material.Set("rowHeight", RowHeight);
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
    }

    public void Dispose()
    {
        CanvasServer.Current.Free(curves);
        CanvasServer.Current.Free(labels);
    }
}
