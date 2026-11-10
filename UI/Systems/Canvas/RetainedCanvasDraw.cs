using System.Collections.Generic;

namespace Xanadu.Systems.Canvas;

/// <summary>
/// Draws to a canvas item only when the picture changes. Canvas items keep what was
/// drawn on them, so a view can describe itself every frame between
/// <see cref="Begin"/> and <see cref="End"/> and sends nothing while it looks the same.
/// </summary>
public sealed class RetainedCanvasDraw(IRectCanvas canvas, Handle target)
{
    private enum Kind
    {
        Rect,
        Circle,
        CircleOutline,
        TextLeft,
        TextRight,
    }

    private readonly record struct Command(Kind Kind, Rect2 Bounds, Color Color, string? Text = null, float Width = 0);

    private List<Command> _next = new();
    private List<Command> _drawn = new();
    private bool _isDrawn;

    public void Begin() => _next.Clear();

    public void Rect(Rect2 bounds, Color color) => _next.Add(new Command(Kind.Rect, bounds, color));

    public void Circle(Vector2 center, float radius, Color color) =>
        _next.Add(new Command(Kind.Circle, new Rect2(center, radius, 0), color));

    public void CircleOutline(Vector2 center, float radius, Color color, float width) =>
        _next.Add(new Command(Kind.CircleOutline, new Rect2(center, radius, 0), color, null, width));

    public void TextLeft(Rect2 bounds, string text, Color color) =>
        _next.Add(new Command(Kind.TextLeft, bounds, color, text));

    public void TextRight(Rect2 bounds, string text, Color color) =>
        _next.Add(new Command(Kind.TextRight, bounds, color, text));

    /// <summary>Sends the picture described since <see cref="Begin"/>, if it differs from the last.</summary>
    public void End()
    {
        if (_isDrawn && Matches())
            return;

        canvas.Clear(target);
        foreach (Command command in _next)
            Send(command);

        (_next, _drawn) = (_drawn, _next);
        _isDrawn = true;
    }

    /// <summary>Wipes the canvas item, so the next picture is sent whatever it is.</summary>
    public void Reset()
    {
        canvas.Clear(target);
        _drawn.Clear();
        _isDrawn = false;
    }

    private bool Matches()
    {
        if (_next.Count != _drawn.Count)
            return false;

        for (int i = 0; i < _next.Count; i++)
        {
            if (_next[i] != _drawn[i])
                return false;
        }
        return true;
    }

    private void Send(in Command command)
    {
        switch (command.Kind)
        {
            case Kind.Rect:
                canvas.DrawRect(target, command.Bounds, command.Color);
                break;
            case Kind.Circle:
                canvas.DrawCircle(target, command.Bounds.Position, command.Bounds.Size.X, command.Color);
                break;
            case Kind.CircleOutline:
                canvas.DrawCircleOutline(target, command.Bounds.Position, command.Bounds.Size.X, command.Color, command.Width);
                break;
            case Kind.TextLeft:
                canvas.DrawTextLeft(target, command.Bounds, command.Text!, command.Color);
                break;
            case Kind.TextRight:
                canvas.DrawTextRight(target, command.Bounds, command.Text!, command.Color);
                break;
        }
    }
}
