using System.Collections.Generic;
using Friflo.Engine.ECS;
using Position = Xanadu.Core.Position;

namespace Xanadu.Model.Ecs;

/// <summary>
/// One play HUD element the player can lay out: a group of entities moved,
/// scaled, turned, mirrored and shown together. Like osu!'s skin editor it has
/// an anchor (the screen point it hangs from, so it keeps its edge on another
/// screen size) and an origin (its own point that sits there, and that it turns
/// and scales about). Points 0–8 run row by row: top left, top, top right, …
/// </summary>
public sealed class HudItemLayout(
    string id,
    Vector2 anchor,
    Rect2 bounds)
{
    public string Id = id;

    /// <summary>The built-in place the element was made at (its old single anchor point).</summary>
    public Vector2 Home = anchor;

    /// <summary>The element's box as built, before any layout.</summary>
    public Rect2 Bounds = bounds;

    /// <summary>How far it has been moved from where it was built, pixels.</summary>
    public Vector2 Offset;

    public float Scale = 1f;

    /// <summary>Its turn about its origin, radians.</summary>
    public float Rotation;

    public bool FlipX, FlipY;
    public bool Visible = true;

    /// <summary>The screen point it hangs from, 0–8.</summary>
    public int Anchor;

    /// <summary>Its own point that sits at the anchor, and that it turns and scales about, 0–8.</summary>
    public int Origin;

    public bool Initialized;
    public Entity Handle;
    public readonly List<Entity> Members = [];
    public readonly List<Vector2> BasePositions = [];

    /// <summary>Its box where it has been moved to, unturned and unscaled.</summary>
    public Rect2 Moved => new(Bounds.Position + Offset, Bounds.Size);

    /// <summary>Where its origin is on screen.</summary>
    public Vector2 OriginPoint => HudPoints.On(Moved, Origin);

    /// <summary>Its scale with mirroring, as a vector (negative mirrors).</summary>
    public Vector2 ScaleVector => new(FlipX ? -Scale : Scale, FlipY ? -Scale : Scale);

    public bool Posed => Scale != 1f || Rotation != 0f || FlipX || FlipY;

    /// <summary>The corners of its box as drawn, turned and scaled about its origin: top left, top right, bottom right, bottom left.</summary>
    public Vector2[] Corners()
    {
        Rect2 box = Moved;
        Vector2 pivot = OriginPoint;
        Vector2 scale = ScaleVector;
        float cos = MathF.Cos(Rotation), sin = MathF.Sin(Rotation);
        Vector2 Place(Vector2 p)
        {
            Vector2 d = (p - pivot) * scale;
            return pivot + new Vector2(d.X * cos - d.Y * sin, d.X * sin + d.Y * cos);
        }

        return
        [
            Place(box.Position),
            Place(new Vector2(box.End.X, box.Position.Y)),
            Place(box.End),
            Place(new Vector2(box.Position.X, box.End.Y)),
        ];
    }

    /// <summary>The upright box round its corners as drawn.</summary>
    public Rect2 Shown()
    {
        Vector2[] corners = Corners();
        Vector2 min = corners[0], max = corners[0];
        foreach (Vector2 corner in corners)
        {
            min = Vector2.Min(min, corner);
            max = Vector2.Max(max, corner);
        }

        return new Rect2(min, max - min);
    }

    /// <summary>Whether a screen point is on it as drawn.</summary>
    public bool Contains(Vector2 point)
    {
        Vector2[] c = Corners();
        float sign = 0f;
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = c[i], b = c[(i + 1) % 4];
            float cross = (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);
            if (cross == 0f)
                continue;
            if (sign == 0f)
                sign = MathF.Sign(cross);
            else if (MathF.Sign(cross) != sign)
                return false;
        }

        return true;
    }

    /// <summary>Its layout, to undo to or revert to.</summary>
    public HudItemState State => new(Offset, Scale, Rotation, FlipX, FlipY, Visible, Anchor, Origin);

    public void Apply(in HudItemState state)
    {
        Offset = state.Offset;
        Scale = state.Scale;
        Rotation = state.Rotation;
        FlipX = state.FlipX;
        FlipY = state.FlipY;
        Visible = state.Visible;
        Anchor = state.Anchor;
        Origin = state.Origin;
    }
}

/// <summary>One element's layout at a moment, for undo and revert.</summary>
public readonly record struct HudItemState(
    Vector2 Offset,
    float Scale,
    float Rotation,
    bool FlipX,
    bool FlipY,
    bool Visible,
    int Anchor,
    int Origin);

/// <summary>The nine points of a box an element can hang from or turn about, row by row.</summary>
public static class HudPoints
{
    public static readonly string[] Names =
    [
        "Top left", "Top", "Top right",
        "Left", "Centre", "Right",
        "Bottom left", "Bottom", "Bottom right",
    ];

    /// <summary>Point <paramref name="index"/> of <paramref name="box"/>.</summary>
    public static Vector2 On(Rect2 box, int index) =>
        box.Position + box.Size * new Vector2(index % 3 / 2f, index / 3 / 2f);

    /// <summary>The point of the screen in whose third a box's centre lies: what an element hangs from by default.</summary>
    public static int Closest(Rect2 box, Vector2 screen)
    {
        Vector2 centre = box.GetCenter();
        int column = centre.X < screen.X / 3f ? 0 : centre.X > screen.X * 2f / 3f ? 2 : 1;
        int row = centre.Y < screen.Y / 3f ? 0 : centre.Y > screen.Y * 2f / 3f ? 2 : 1;
        return row * 3 + column;
    }
}

/// <summary>Play HUD layout metadata attached to the playfield entity.</summary>
public struct HudLayout : IComponent
{
    public HudLayout() { }

    public List<HudItemLayout> Items = [];
    public bool Editing;

    public HudItemLayout Add(
        string id,
        Vector2 anchor,
        Rect2 bounds,
        params Entity[] members)
    {
        var item = new HudItemLayout(id, anchor, bounds);
        foreach (Entity member in members)
        {
            if (member.IsNull)
                continue;

            item.Members.Add(member);
            item.BasePositions.Add(
                member.HasComponent<Position>()
                    ? member.GetComponent<Position>().Value
                    : anchor);
        }

        if (members.Length > 0)
            item.Handle = members[0];
        Items.Add(item);
        return item;
    }
}
