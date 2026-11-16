using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Draws every <see cref="UiBoard"/> into a canvas item of its own under its node,
/// clipped to the node's box: a dot grid, frames behind the boxes they hold
/// (titled top-left), the lines between boxes with an arrowhead (over frames, under
/// boxes, bent round the boxes in their way), boxes with their text wrapped and
/// centred, the selection, the link dot and resize corner under the
/// pointer, a line being dragged, and the caret while a box is typed into. Colours
/// are the theme's roles and categories. Redrawn whole every frame.
/// </summary>
public sealed class BoardDrawSystem(IRectCanvas canvas) : QuerySystem<UiBoard>
{
    private const float GridStep = 50, MinGridGap = 24, Pad = 14, LineWidth = 3, LineGap = 1.3f, MinFont = 7;

    private readonly List<string> _lines = new();
    private readonly List<BoardBox> _boxes = new();

    protected override void OnUpdate()
    {
        foreach (Entity entity in Query.ToEntityList())
        {
            ref UiBoard ui = ref entity.GetComponent<UiBoard>();
            Draw(entity, ui.Board, ui.State);
        }
    }

    private void Draw(Entity entity, Board board, BoardState state)
    {
        if (!state.HasItem)
        {
            state.Item = CanvasServer.Current.CreateItem();
            if (entity.TryGetComponent(out VisualRid visual))
                CanvasServer.Current.SetParent(state.Item, visual.Value);
            CanvasServer.Current.SetClip(state.Item, true);
            state.HasItem = true;
        }

        Handle item = state.Item;
        Rect2 area = new(state.Local(state.Area.Position), state.Area.Size);
        CanvasServer.Current.SetCustomRect(item, true, area);
        canvas.Clear(item);

        UiThemeRoles roles = UiThemes.Of(entity);
        canvas.DrawRect(item, area, roles.Inset);
        Grid(item, state, area, roles);

        _boxes.Clear();
        foreach (BoardBox box in board.PaintOrder())
        {
            if (board.IsFrame(box))
                Box(item, state, box, true, roles);
            else
                _boxes.Add(box);
        }

        List<List<Vector2>?> paths = board.Paths();
        for (int i = 0; i < paths.Count; i++)
        {
            if (paths[i] is { } path)
                Link(item, state, board.Links[i], path, roles);
        }

        foreach (BoardBox box in _boxes)
            Box(item, state, box, false, roles);
        Handles(item, board, state, roles);
    }

    private void Grid(Handle item, BoardState state, Rect2 area, UiThemeRoles roles)
    {
        float step = GridStep;
        while (step * state.Zoom < MinGridGap)
            step *= 2;

        Color dot = roles.TextDim with { A = 0.18f };
        Vector2 first = new(MathF.Ceiling(state.Pan.X / step) * step, MathF.Ceiling(state.Pan.Y / step) * step);
        Vector2 last = state.ToBoard(state.Area.End);
        for (float y = first.Y; y <= last.Y; y += step)
        {
            for (float x = first.X; x <= last.X; x += step)
            {
                Vector2 at = state.Local(state.ToScreen(new Vector2(x, y)));
                canvas.DrawRect(item, new Rect2(at - Vector2.One, new Vector2(2, 2)), dot);
            }
        }
    }

    private void Box(Handle item, BoardState state, BoardBox box, bool frame, UiThemeRoles roles)
    {
        Rect2 screen = state.ToScreen(box.Rect);
        Rect2 local = new(state.Local(screen.Position), screen.Size);
        Color colour = roles.Category(box.Category);
        bool selected = state.Selected.Contains(box.Id);

        canvas.DrawRect(item, local, frame ? roles.Inset.Lerp(colour, 0.08f) : roles.CategoryTone(box.Category).Fill);
        Outline(item, local, selected ? roles.Accent : frame ? colour with { A = 0.6f } : colour, selected ? 2 : 1);

        int size = (int)MathF.Round((frame ? UiType.Small : UiType.Body) * state.Zoom);
        bool editing = state.Editing == box.Id;
        if (size < MinFont)
            return;

        float pad = Pad * state.Zoom;
        Wrap(box.Text, size, local.Size.X - pad * 2);
        float lineHeight = size * LineGap;
        float top = frame ? local.Position.Y + pad : local.Position.Y + (local.Size.Y - _lines.Count * lineHeight) / 2;
        for (int i = 0; i < _lines.Count; i++)
        {
            var row = new Rect2(local.Position.X + pad, top + i * lineHeight, local.Size.X - pad * 2, lineHeight);
            if (frame)
                canvas.DrawTextStyledLeft(item, row, _lines[i], roles.TextDim, size);
            else
                canvas.DrawTextStyled(item, row, _lines[i], roles.Text, size);
        }

        if (editing && (state.SinceCaretMove < 0.5f || (int)(state.Clock * 2) % 2 == 0))
            Caret(item, local, frame, top, lineHeight, size, pad, roles);
    }

    private void Caret(Handle item, Rect2 local, bool frame, float top, float lineHeight, int size, float pad, UiThemeRoles roles)
    {
        string last = _lines.Count > 0 ? _lines[^1] : "";
        float width = UiTextMetrics.Width(last, size);
        float x = frame ? local.Position.X + pad + width : local.Position.X + local.Size.X / 2 + width / 2;
        float y = top + Math.Max(_lines.Count - 1, 0) * lineHeight;
        if (_lines.Count == 0)
            y = top - lineHeight / 2;
        canvas.DrawRect(item, new Rect2(x + 1, y + (lineHeight - size) / 2, 2, size), roles.Accent);
    }

    /// <summary>Breaks text at spaces and newlines into <see cref="_lines"/>, each fitting <paramref name="width"/>.</summary>
    private void Wrap(string text, int size, float width)
    {
        _lines.Clear();
        foreach (string paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' '))
            {
                string tried = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && UiTextMetrics.Width(tried, size) > width)
                {
                    _lines.Add(line);
                    line = word;
                }
                else
                    line = tried;
            }

            _lines.Add(line);
        }

        if (_lines.Count == 1 && _lines[0].Length == 0)
            _lines.Clear();
    }

    private void Link(Handle item, BoardState state, BoardLink link, List<Vector2> path, UiThemeRoles roles)
    {
        Color colour = state.SelectedLink == link ? roles.Accent : roles.TextDim;
        float width = state.SelectedLink == link ? LineWidth + 1 : LineWidth;
        for (int i = 1; i < path.Count - 1; i++)
            canvas.DrawLine(item, state.Local(state.ToScreen(path[i - 1])), state.Local(state.ToScreen(path[i])), colour, width);
        Arrow(item, state.Local(state.ToScreen(path[^2])), state.Local(state.ToScreen(path[^1])), colour, width);
    }

    private void Arrow(Handle item, Vector2 from, Vector2 to, Color colour, float width)
    {
        canvas.DrawLine(item, from, to, colour, width);
        Vector2 along = to - from;
        if (along.LengthSquared() < 1)
            return;

        along = Vector2.Normalize(along);
        Vector2 side = new(-along.Y, along.X);
        const float Length = 14, Half = 7;
        canvas.DrawPolygon(item, [to, to - along * Length + side * Half, to - along * Length - side * Half], colour);
    }

    private void Handles(Handle item, Board board, BoardState state, UiThemeRoles roles)
    {
        if (state.Gesture == BoardGestureKind.Link && board.Find(state.Target) is { } from)
        {
            Vector2 start = Board.Edge(from.Rect, state.Pointer);
            Arrow(item, state.Local(state.ToScreen(start)), state.Local(state.ToScreen(state.Pointer)), roles.Accent, LineWidth);
            return;
        }

        if (state.Gesture != BoardGestureKind.None || state.Editing != 0)
            return;

        if (board.Find(state.Hover) is not { } hover)
            return;

        Vector2 dot = state.Local(state.ToScreen(BoardInputSystem.HandleOf(hover.Rect)));
        canvas.DrawCircle(item, dot, state.HoverLinkHandle ? 6 : 4, state.HoverLinkHandle ? roles.Accent : roles.TextDim);

        if (state.HoverResize)
        {
            Rect2 screen = state.ToScreen(hover.Rect);
            Vector2 end = state.Local(screen.End);
            float size = BoardInputSystem.CornerSize;
            canvas.DrawPolygon(item, [end, end - new Vector2(size, 0), end - new Vector2(0, size)], roles.Accent);
        }
    }

    private void Outline(Handle item, Rect2 rect, Color colour, float width)
    {
        Vector2 p = rect.Position, s = rect.Size;
        canvas.DrawRect(item, new Rect2(p, new Vector2(s.X, width)), colour);
        canvas.DrawRect(item, new Rect2(p.X, p.Y + s.Y - width, s.X, width), colour);
        canvas.DrawRect(item, new Rect2(p, new Vector2(width, s.Y)), colour);
        canvas.DrawRect(item, new Rect2(p.X + s.X - width, p.Y, width, s.Y), colour);
    }
}
