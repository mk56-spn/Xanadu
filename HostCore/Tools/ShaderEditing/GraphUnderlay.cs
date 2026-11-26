using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The render-graph board's wires, drawn by a stack (<see cref="Shader"/>) as the board's underlay, between its backing
/// and its boxes: each wire, as the board routes it, a line fading from its writer's colour to its reader's that glows
/// and carries dots from writer to reader. It hands the stack the wires where the board's pan and zoom put them, as its
/// <c>[Host]</c> buffer, again only when the view or the wires changed (the dots move in the shader, by time).
/// </summary>
internal sealed class GraphUnderlay
{
    private const string Shader = "Assets/Shaders/Lab/graph_board";

    /// <summary>The stack's buffer size (WIRES in its board.slang).</summary>
    private const int MaxWires = 512;

    private readonly CanvasMaterial _material = new(Shader);
    private readonly Board _board;
    private readonly Wire[] _wires = new Wire[MaxWires];

    /// <summary>The board's wires in board space, worked out once per board shown.</summary>
    private List<List<Vector2>?>? _paths;

    public GraphUnderlay(Board shown) => _board = shown;

    /// <summary>The board was made again: the wires are routed afresh.</summary>
    public void Rebuilt() => _paths = null;

    /// <summary>The view the wires were last handed over for.</summary>
    private (Rect2 Area, Vector2 Pan, float Zoom, UiThemeRoles? Roles) _placed;

    /// <summary>Hands the stack the wires where the view puts them this frame.</summary>
    public void Update(BoardState state, UiThemeRoles roles)
    {
        state.Underlay = _material;
        if (state.Area.Size.X <= 0)
            return;
        if (_paths is not null && _placed == (state.Area, state.Pan, state.Zoom, roles))
            return;
        _placed = (state.Area, state.Pan, state.Zoom, roles);
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
            Vector4 from = Tone(roles, _board.Links[at].From), to = Tone(roles, _board.Links[at].To);
            for (int i = 1; i < path.Count && wires < MaxWires; i++)
            {
                Vector2 a = Place(state, path[i - 1]), b = Place(state, path[i]);
                _wires[wires++] = new Wire(a, b, along, total, both, 0, from, to);
                along += (b - a).Length();
            }
        }

        _material.Lists.Set<Wire>("wires", _wires.AsSpan(0, wires));
        _material.Set("wireCount", wires);
        _material.Set("area", state.Area.Size);
        _material.Set("zoom", state.Zoom);
    }

    /// <summary>A box's colour, as the stack reads it.</summary>
    private Vector4 Tone(UiThemeRoles roles, int box) =>
        _board.Find(box) is { } found && roles.Category(found.Category) is var c ? new Vector4(c.R, c.G, c.B, 1) : Vector4.One;

    /// <summary>A board point in pixels from the area's top-left corner.</summary>
    private static Vector2 Place(BoardState state, Vector2 point) => state.ToScreen(point) - state.Area.Position;

    /// <summary>The stack's <c>Wire</c>, laid out as it reads it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Wire(Vector2 A, Vector2 B, float Along, float Total, float Both, float Spare, Vector4 From, Vector4 To);
}
