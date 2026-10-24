using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;

namespace Writer.Ecs;

/// <summary>One movable/visibility-toggleable group of play HUD entities.</summary>
public sealed class HudItemLayout(
    string id,
    Vector2 anchor,
    Rect2 bounds)
{
    public string Id = id;
    public Vector2 Anchor = anchor;
    public Rect2 Bounds = bounds;
    public Vector2 Offset;
    public bool Visible = true;
    public bool Initialized;
    public readonly List<Entity> Members = new();
    public readonly List<Vector2> BasePositions = new();
}

/// <summary>Play HUD layout metadata attached to the playfield entity.</summary>
public struct HudLayout : IComponent
{
    public HudLayout() { }

    public List<HudItemLayout> Items = new();
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
                member.HasComponent<Writer.Ecs.Position>()
                    ? member.GetComponent<Writer.Ecs.Position>().Value
                    : anchor);
        }

        Items.Add(item);
        return item;
    }
}
