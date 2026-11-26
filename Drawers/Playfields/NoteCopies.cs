using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// One counted draw of note looks (key notes, dash bars, chord lines, landing dots) through the
/// <see cref="Shader"/> stack: each look is added as a quad in the item's own units, and
/// <see cref="Draw"/> hands the whole list to the stack as its <c>[Host]</c> buffer and draws it at
/// that point of the item, in the order added. Each use (a target and what it draws) keeps its own,
/// as a material's buffer is the material's.
/// </summary>
public sealed class NoteCopies(string use)
{
    public const string Shader = "Assets/Shaders/Game/notes";

    /// <summary>The stack's list size (LOOKS in notes.slang); looks past it are left out.</summary>
    private const int Most = 2048;

    private const uint Key = 0, Dash = 1, Line = 2, Dot = 3;

    private static readonly Dictionary<(Handle, string), NoteCopies> Uses = [];

    private readonly CanvasMaterial _material = new(Shader, instance: use);
    private readonly List<Shown> _shown = [];

    /// <summary>The copies <paramref name="use"/> keeps on <paramref name="target"/>.</summary>
    public static NoteCopies For(Handle target, string use)
    {
        if (!Uses.TryGetValue((target, use), out NoteCopies? copies))
            Uses[(target, use)] = copies = new NoteCopies(use);
        return copies;
    }

    /// <summary>A key note's rectangle round <paramref name="center"/>, <paramref name="scale"/> item units a playfield unit.</summary>
    public void AddKey(Vector2 center, float scale, Color color)
    {
        Vector2 size = Chords.NoteSize * scale;
        Add(new Vector2(size.X, 0f), new Vector2(0f, size.Y), center - size / 2f, Key, scale, color, default);
    }

    /// <summary>A dash's bar across the chord with its diamond, round <paramref name="center"/>.</summary>
    public void AddDash(Vector2 center, float scale, Color color)
    {
        Vector2 half = new Vector2(Chords.DashSize.X / 2f + 2f, MathF.Max(Chords.DashSize.Y / 2f, NoteLooks.DiamondReach + 2f)) * scale;
        Add(new Vector2(half.X * 2f, 0f), new Vector2(0f, half.Y * 2f), center - half, Dash, scale, color,
            new Vector4(Chords.DashSize.X, Chords.DashSize.Y, NoteLooks.DiamondReach, 0f));
    }

    /// <summary>A line <paramref name="width"/> wide, clear at <paramref name="from"/> and full colour <paramref name="fade"/> of the way along.</summary>
    public void AddLine(Vector2 from, Vector2 to, Color color, float width, float fade)
    {
        Vector2 along = to - from;
        if (along.LengthSquared() < 1e-4f)
            return;
        Vector2 side = Vector2.Normalize(new Vector2(-along.Y, along.X)) * width;
        Add(along, side, from - side / 2f, Line, 1f, color, new Vector4(fade, 0f, 0f, 0f));
    }

    /// <summary>A round dot <paramref name="radius"/> across either way of <paramref name="center"/>.</summary>
    public void AddDot(Vector2 center, float radius, Color color) =>
        Add(new Vector2(radius * 2f, 0f), new Vector2(0f, radius * 2f), center - new Vector2(radius), Dot, 1f, color, default);

    /// <summary>
    /// Draws every look added since the last draw, in one run over <paramref name="area"/> (in the
    /// item's units, the looks' too), and starts the list again.
    /// </summary>
    public void Draw(IRectCanvas canvas, Handle target, Rect2 area)
    {
        int count = Math.Min(_shown.Count, Most);
        Span<Shown> shown = CollectionsMarshal.AsSpan(_shown)[..count];
        for (int i = 0; i < shown.Length; i++)
            shown[i] = shown[i] with { Corner = shown[i].Corner - area.Position };
        _material.Lists.Set<Shown>("looks", shown);
        _material.Set("lookCount", count);
        _material.Set("area", area.Size);
        _shown.Clear();
        if (count == 0 || area.Size.X <= 0 || area.Size.Y <= 0)
            return;

        canvas.SelectMaterial(_material);
        canvas.DrawRect(target, area, Colors.White);
        canvas.SelectMaterial(null);
    }

    private void Add(Vector2 xAxis, Vector2 yAxis, Vector2 corner, uint kind, float scale, Color color, Vector4 shape)
    {
        if (color.A <= 0f)
            return;
        _shown.Add(new Shown(xAxis, yAxis, corner, kind, scale, new Vector4(color.R, color.G, color.B, color.A), shape));
    }

    /// <summary>The stack's <c>Shown</c>, laid out as it reads it; corners are from the run's rect.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Shown(Vector2 XAxis, Vector2 YAxis, Vector2 Corner, uint Kind, float Scale, Vector4 Tint, Vector4 Shape);
}
