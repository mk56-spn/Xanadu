using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

public static partial class PlayfieldRenderer
{
    /// <summary>The player's box while hitboxes are shown (F10).</summary>
    public static readonly Color PlayerHitboxColor = new("#22c55e");

    /// <summary>A block's box while hitboxes are shown (F10).</summary>
    public static readonly Color BlockHitboxColor = new("#fb7185");

    private static readonly Color HitboxFill = new(1f, 1f, 1f, 0.1f);

    /// <summary>
    /// Outlines the boxes collision actually uses: the player's box, which is
    /// already in screen pixels, and every block's box scaled by the view. The
    /// character art is drawn over the playfield, so this needs its own canvas
    /// above the characters to stay visible.
    /// </summary>
    public static void DrawHitboxes(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view)
    {
        foreach (Entity entity in entities.Query<Block>().ToEntityList())
        {
            Block block = entity.GetComponent<Block>();
            if (block.Size.X <= 0 || block.Size.Y <= 0)
                continue;

            // A static body is built from the block's centre and scaled size,
            // which is the same rect as its top-left corner and size.
            var screen = new Rect2(
                view.PlayfieldToScreen(block.Position),
                block.Size * view.Scale);
            DrawHitbox(canvas, rid, screen, BlockHitboxColor);
        }

        foreach (Entity entity in entities.Query<Player, Position>().ToEntityList())
        {
            Player player = entity.GetComponent<Player>();
            Vector2 center = entity.GetComponent<Position>().Value;
            var screen = new Rect2(center - player.Size / 2, player.Size);
            DrawHitbox(canvas, rid, screen, PlayerHitboxColor);
        }
    }

    private static void DrawHitbox(
        IRectCanvas canvas,
        Rid rid,
        Rect2 bounds,
        Color color)
    {
        canvas.DrawRect(rid, bounds, HitboxFill);
        DrawBorder(canvas, rid, bounds, color, 1.5f);
    }
}
