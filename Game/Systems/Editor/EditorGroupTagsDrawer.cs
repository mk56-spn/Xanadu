using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Playfields;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The group tags the view bar's TAGS button turns on: beside each item in a
/// group, a see-through box holding one dark pill per group it's in, "G1",
/// "G2" and so on, in group order. Drawn over everything, at the item's shown
/// position, and only for layers the editor shows.
/// </summary>
public sealed class EditorGroupTagsDrawer(EditorDrawSurface surface)
{
    public const float PillWidth = 58f;
    public const float PillHeight = 20f;
    public const float Pad = 6f;
    public const float Gap = 4f;
    public const float Radius = 6f;
    public const int FontSize = 16;

    /// <summary>The gap between the item's box and its tags.</summary>
    private const float Offset = 6f;

    private static readonly Color Backing = new(0, 0, 0, 0.3f);
    private static readonly Color Pill = new(0, 0, 0, 1f);
    private static readonly Color Ink = new(1, 1, 1, 1f);

    /// <summary>A light rim on each pill, so the black reads against the dark field.</summary>
    private static readonly Color Outline = new(1, 1, 1, 0.7f);
    private const float OutlineWidth = 1.5f;

    private readonly List<Vector2> _points = new();

    public void Draw(Entity editor, PlayfieldView view)
    {
        if (!GroupTagsSwitch.IsOn(surface.Entities))
            return;

        EditorLayers layers = EditorLayers.Of(editor);
        foreach (var (groups, chunk) in surface.Entities.Cached<ItemGroups>().Chunks)
        {
            Span<ItemGroups> span = groups.Span;
            for (int i = 0; i < span.Length; i++)
            {
                uint mask = span[i].Mask;
                Entity item = chunk.EntityAt(i);
                if (mask == 0 || !layers.Shows(ItemLayer.Of(item).Layer) ||
                    !SelectionRotation.TryGetBox(item, out Rect2 box))
                {
                    continue;
                }

                // A point item (a note, mine or start point) is tagged beside the circle it draws.
                if (box.Size == Vector2.Zero)
                    box = box.Grow(PlayfieldRenderer.NoteSize / 2f);

                Vector2 shift = GroupOffset.Of(item);
                Vector2 corner = view.PlayfieldToScreen(new Vector2(box.End.X, box.Position.Y) + shift);
                DrawStack(corner + new Vector2(Offset, 0), mask);
            }
        }
    }

    /// <summary>The backing box and one pill per group, top-left at <paramref name="origin"/>.</summary>
    private void DrawStack(Vector2 origin, uint mask)
    {
        int count = ItemGroups.Count(mask);
        var backing = new Rect2(
            origin,
            new Vector2(PillWidth + Pad * 2, Pad * 2 + count * PillHeight + (count - 1) * Gap));
        FillRounded(backing, Backing);

        float y = origin.Y + Pad;
        for (int group = 0; group < GroupLimits.Count; group++)
        {
            if ((mask & ItemGroups.Bit(group)) == 0)
                continue;

            var pill = new Rect2(origin.X + Pad, y, PillWidth, PillHeight);
            FillRounded(pill, Pill);
            OutlineRounded(pill, Outline);
            surface.Canvas.DrawTextWeighted(
                surface.Target,
                new Rect2(pill.Position + new Vector2(6, 0), pill.Size - new Vector2(6, 0)),
                "G" + (group + 1),
                Ink,
                FontSize,
                TextAlign.Left,
                bold: true);
            y += PillHeight + Gap;
        }
    }

    private void FillRounded(Rect2 rect, Color color)
    {
        RoundedRect(rect, Radius, _points);
        surface.Canvas.DrawPolygon(surface.Target, _points.ToArray(), color);
    }

    private void OutlineRounded(Rect2 rect, Color color)
    {
        // Inset by half the width so the rim stays inside the pill's edge.
        RoundedRect(rect.Grow(-OutlineWidth / 2f), Radius - OutlineWidth / 2f, _points);
        _points.Add(_points[0]);
        surface.Canvas.DrawPolyline(surface.Target, _points.ToArray(), color, OutlineWidth);
    }

    /// <summary>The outline of a rectangle with rounded corners, clockwise.</summary>
    public static void RoundedRect(Rect2 rect, float radius, List<Vector2> into)
    {
        const int steps = 4;
        into.Clear();
        float r = Mathf.Min(radius, Mathf.Min(rect.Size.X, rect.Size.Y) / 2f);
        Vector2[] centres =
        [
            new(rect.End.X - r, rect.Position.Y + r),
            new(rect.End.X - r, rect.End.Y - r),
            new(rect.Position.X + r, rect.End.Y - r),
            new(rect.Position.X + r, rect.Position.Y + r),
        ];
        for (int corner = 0; corner < 4; corner++)
        {
            float start = -Mathf.Pi / 2f + corner * Mathf.Pi / 2f;
            for (int step = 0; step <= steps; step++)
            {
                float angle = start + step * (Mathf.Pi / 2f) / steps;
                into.Add(centres[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
            }
        }
    }
}
