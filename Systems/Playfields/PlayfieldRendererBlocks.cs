using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Systems.Canvas;

namespace Writer.Systems.Playfields;

/// <summary>Draws collidable blocks in both editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    public static readonly Color BlockFill = new("#1f2937");
    public static readonly Color BlockEdge = new("#475569");
    public static readonly Color BlockSelectedEdge = new("#facc15");

    /// <summary>
    /// Draws every block as a filled slab. The selected one, if any, gets an
    /// accent edge; the editor draws its resize handles on top.
    /// </summary>
    public static void DrawBlocks(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Entity selected = default)
    {
        foreach (Entity entity in entities.Query<Block>().ToEntityList())
        {
            Block block = entity.GetComponent<Block>();
            if (block.Size.X <= 0 || block.Size.Y <= 0)
                continue;

            Rect2 screen = new(
                view.PlayfieldToScreen(block.Position),
                block.Size * view.Scale);
            bool chosen = entity == selected;

            canvas.DrawRect(rid, screen, chosen ? BlockSelectedEdge : BlockEdge);
            float border = Mathf.Min(
                2f,
                Mathf.Min(screen.Size.X, screen.Size.Y) / 2);
            canvas.DrawRect(rid, screen.Grow(-border), BlockFill);
        }
    }
}
