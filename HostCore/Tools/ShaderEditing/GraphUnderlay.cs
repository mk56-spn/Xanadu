using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The render-graph board's wires, drawn by a stack (<see cref="Shader"/>) as the board's underlay, between its backing
/// and its boxes: each wire, as the board routes it, a line that glows and carries pulses from writer to reader, and a
/// glow under each box marked hot. Each frame it hands the stack the wires and boxes where the board's pan and zoom put
/// them, as its <c>[Host]</c> buffers; the same view twice uploads nothing.
/// </summary>
internal sealed class GraphUnderlay
{
    private const string Shader = "Assets/Shaders/Lab/graph_board";

    /// <summary>The stack's buffer sizes (WIRES and HOT in its build.slang).</summary>
    private const int MaxWires = 512, MaxHot = 64;

    private readonly CanvasMaterial _material = new(Shader);
    private readonly Board _board;
    private readonly Wire[] _wires = new Wire[MaxWires];
    private readonly Hot[] _hot = new Hot[MaxHot];
    private readonly List<(int Box, Color Tone)> _hotBoxes = [];

    /// <summary>The board's wires in board space, worked out once per board shown.</summary>
    private List<List<Vector2>?>? _paths;

    public GraphUnderlay(Board shown) => _board = shown;

    /// <summary>The board was made again: no box is hot until marked, and the wires are routed afresh.</summary>
    public void Rebuilt()
    {
        _hotBoxes.Clear();
        _paths = null;
    }

    /// <summary>Glows under a box; the tone's alpha is how strongly.</summary>
    public void Mark(int box, Color tone) => _hotBoxes.Add((box, tone));

    /// <summary>Hands the stack the wires and hot boxes where the view puts them this frame.</summary>
    public void Update(BoardState state, UiThemeRoles roles)
    {
        state.Underlay = _material;
        if (state.Area.Size.X <= 0)
            return;
        _paths ??= _board.Paths();

        int wires = 0;
        for (int at = 0; at < _paths.Count && wires < MaxWires; at++)
        {
            if (_paths[at] is not { } path)
                continue;
            float total = 0;
            for (int i = 1; i < path.Count; i++)
                total += (Place(state, path[i]) - Place(state, path[i - 1])).Length();
            float along = 0, both = _board.TwoWay(_board.Links[at]) ? 1 : 0;
            for (int i = 1; i < path.Count && wires < MaxWires; i++)
            {
                Vector2 a = Place(state, path[i - 1]), b = Place(state, path[i]);
                _wires[wires++] = new Wire(a, b, along, total, both, 0);
                along += (b - a).Length();
            }
        }

        int hot = 0;
        foreach ((int id, Color tone) in _hotBoxes)
        {
            if (hot == MaxHot || _board.Find(id) is not { } box)
                continue;
            _hot[hot++] = new Hot(Place(state, box.Rect.Position), Place(state, box.Rect.End), new Vector4(tone.R, tone.G, tone.B, tone.A));
        }

        _material.SetBuffer<Wire>("wires", _wires.AsSpan(0, wires));
        _material.SetBuffer<Hot>("hot", _hot.AsSpan(0, hot));
        _material.Set("wireCount", wires);
        _material.Set("hotCount", hot);
        _material.Set("area", state.Area.Size);
        _material.Set("zoom", state.Zoom);
        _material.Set("wireTone", roles.TextDim);
        _material.Set("pulse", roles.Accent);
    }

    /// <summary>A board point in pixels from the area's top-left corner.</summary>
    private static Vector2 Place(BoardState state, Vector2 point) => state.ToScreen(point) - state.Area.Position;

    /// <summary>The stack's <c>Wire</c>, laid out as it reads it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Wire(Vector2 A, Vector2 B, float Along, float Total, float Both, float Spare);

    /// <summary>The stack's <c>Hot</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Hot(Vector2 Low, Vector2 High, Vector4 Tone);
}
