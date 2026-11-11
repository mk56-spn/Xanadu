using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

public static partial class PlayfieldRenderer
{
    /// <summary>
    /// Outlines what collision and input actually use: the player's box, every
    /// block's box, every spike's triangle, and the sphere notes are checked against. The box stays
    /// what damage is measured with; notes are reach-checked against the
    /// sphere. The character art is drawn over the playfield, so this needs its
    /// own canvas above the characters to stay visible.
    /// </summary>
    public static void DrawHitboxes(
        IRectCanvas canvas,
        Handle rid,
        EntityStore entities,
        PlayfieldView view)
    {
        foreach (Entity entity in entities.Cached<Block>().ToEntityList())
        {
            Block block = entity.GetComponent<Block>();
            if (block.Size.X <= 0 || block.Size.Y <= 0)
                continue;

            // A static body is built from the block's centre and scaled size,
            // which is the same rect as its top-left corner and size.
            var screen = new Rect2(
                view.PlayfieldToScreen(GroupOffset.Shown(entity, block.Position)),
                block.Size * view.Scale);
            DrawHitbox(canvas, rid, screen, DebugPalette.BlockHitbox);
            if (block.OneWay != BlockOneWay.Off)
                canvas.DrawRect(rid, OneWayEdge(screen, block.OneWay), DebugPalette.BlockHitbox);
        }

        foreach (Entity entity in entities.Cached<Spike>().ToEntityList())
        {
            Spike spike = entity.GetComponent<Spike>();
            if (spike.Size.X > 0 && spike.Size.Y > 0)
                DrawSpikeHitbox(canvas, rid, SpikeRules.ScreenHitbox(spike, GroupOffset.View(entity, view)));
        }

        foreach (Entity entity in entities.Cached<Player, Position>().ToEntityList())
        {
            Vector2 center = entity.GetComponent<Position>().Value;
            Rect2 screen = PlayerTuck.Box(entity, center);
            DrawHitbox(canvas, rid, screen, DebugPalette.PlayerHitbox);
            canvas.DrawCircleOutline(
                rid,
                center,
                NoteReach.OnScreen(view.Scale),
                DebugPalette.NoteReach,
                1.5f);

        }
    }

    private static void DrawSpikeHitbox(
        IRectCanvas canvas,
        Handle rid,
        SpikeTriangle hitbox)
    {
        Span<Vector2> points = [hitbox.Tip, hitbox.BaseLeft, hitbox.BaseRight];
        canvas.DrawPolygon(rid, points, DebugPalette.HitboxFill);
        canvas.DrawLine(rid, hitbox.Tip, hitbox.BaseLeft, DebugPalette.SpikeHitbox, 1.5f);
        canvas.DrawLine(rid, hitbox.BaseLeft, hitbox.BaseRight, DebugPalette.SpikeHitbox, 1.5f);
        canvas.DrawLine(rid, hitbox.BaseRight, hitbox.Tip, DebugPalette.SpikeHitbox, 1.5f);
    }

    private static void DrawHitbox(
        IRectCanvas canvas,
        Handle rid,
        Rect2 bounds,
        Color color)
    {
        canvas.DrawRect(rid, bounds, DebugPalette.HitboxFill);
        canvas.DrawBorder(rid, bounds, color, 1.5f);
    }
}
