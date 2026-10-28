using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

public static partial class PlayfieldRenderer
{
    /// <summary>The player's box while hitboxes are shown (F10).</summary>
    public static readonly Color PlayerHitboxColor = Palette.Success;

    /// <summary>A block's box while hitboxes are shown (F10).</summary>
    public static readonly Color BlockHitboxColor = Palette.Rose;

    /// <summary>The sphere notes are checked against while hitboxes are shown (F10).</summary>
    public static readonly Color NoteReachColor = Palette.Accent;

    private static readonly Color HitboxFill = new(1f, 1f, 1f, 0.1f);

    /// <summary>
    /// Outlines what collision and input actually use: the player's box, every
    /// block's box, and the sphere notes are checked against. The box stays
    /// what damage is measured with; notes are reach-checked against the
    /// sphere. The character art is drawn over the playfield, so this needs its
    /// own canvas above the characters to stay visible.
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
            canvas.DrawCircleOutline(
                rid,
                center,
                NoteReach.OnScreen(view.Scale),
                NoteReachColor,
                1.5f);
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
