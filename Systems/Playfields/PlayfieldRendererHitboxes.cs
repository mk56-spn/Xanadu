using System;
using Friflo.Engine.ECS;
using Godot;
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
            DrawHitbox(canvas, rid, screen, DebugPalette.BlockHitbox);
        }

        foreach (Entity entity in entities.Query<Spike>().ToEntityList())
        {
            Spike spike = entity.GetComponent<Spike>();
            if (spike.Size.X > 0 && spike.Size.Y > 0)
                DrawSpikeHitbox(canvas, rid, SpikeRules.ScreenHitbox(spike, view));
        }

        foreach (Entity entity in entities.Query<Player, Position>().ToEntityList())
        {
            Player player = entity.GetComponent<Player>();
            Vector2 center = entity.GetComponent<Position>().Value;
            var screen = new Rect2(center - player.Size / 2, player.Size);
            DrawHitbox(canvas, rid, screen, DebugPalette.PlayerHitbox);
            canvas.DrawCircleOutline(
                rid,
                center,
                NoteReach.OnScreen(view.Scale),
                DebugPalette.NoteReach,
                1.5f);

            if (entity.TryGetComponent(out PlayerCharacter character) &&
                character.Kind == CharacterKind.Armed &&
                entity.TryGetComponent(out ArmedWeapon weapon))
            {
                bool facesLeft =
                    entity.TryGetComponent(out CharacterFacing facing) &&
                    facing.Left;
                DrawWeaponHitbox(
                    canvas,
                    rid,
                    weapon.Kind,
                    center,
                    facesLeft,
                    view.Scale);
            }
        }
    }

    private static void DrawSpikeHitbox(
        IRectCanvas canvas,
        Rid rid,
        SpikeTriangle hitbox)
    {
        Span<Vector2> points = [hitbox.Tip, hitbox.BaseLeft, hitbox.BaseRight];
        canvas.DrawPolygon(rid, points, DebugPalette.HitboxFill);
        canvas.DrawLine(rid, hitbox.Tip, hitbox.BaseLeft, DebugPalette.SpikeHitbox, 1.5f);
        canvas.DrawLine(rid, hitbox.BaseLeft, hitbox.BaseRight, DebugPalette.SpikeHitbox, 1.5f);
        canvas.DrawLine(rid, hitbox.BaseRight, hitbox.Tip, DebugPalette.SpikeHitbox, 1.5f);
    }

    private static void DrawWeaponHitbox(
        IRectCanvas canvas,
        Rid rid,
        WeaponKind weapon,
        Vector2 center,
        bool facesLeft,
        float scale)
    {
        if (weapon == WeaponKind.Spear)
        {
            float length = WeaponHitRules.SpearLength * scale;
            float thickness = WeaponHitRules.SpearThickness * scale;
            var bounds = new Rect2(
                new Vector2(facesLeft ? center.X - length : center.X,
                    center.Y - thickness / 2f),
                new Vector2(length, thickness));
            DrawHitbox(canvas, rid, bounds, DebugPalette.WeaponHitbox);
            return;
        }

        float inner = WeaponHitRules.AxeInner * scale;
        float outer = WeaponHitRules.AxeOuter * scale;
        float start = facesLeft ? Mathf.Pi / 2f : -Mathf.Pi / 2f;
        float end = start + Mathf.Pi;
        const int segments = 24;

        DrawArc(canvas, rid, center, inner, start, end, segments, DebugPalette.WeaponHitbox);
        DrawArc(canvas, rid, center, outer, start, end, segments, DebugPalette.WeaponHitbox);

        Vector2 innerStart = center + new Vector2(
            Mathf.Cos(start) * inner,
            Mathf.Sin(start) * inner);
        Vector2 innerEnd = center + new Vector2(
            Mathf.Cos(end) * inner,
            Mathf.Sin(end) * inner);
        Vector2 outerStart = center + new Vector2(
            Mathf.Cos(start) * outer,
            Mathf.Sin(start) * outer);
        Vector2 outerEnd = center + new Vector2(
            Mathf.Cos(end) * outer,
            Mathf.Sin(end) * outer);
        canvas.DrawLine(rid, innerStart, outerStart, DebugPalette.WeaponHitbox, 1.5f);
        canvas.DrawLine(rid, innerEnd, outerEnd, DebugPalette.WeaponHitbox, 1.5f);
    }

    private static void DrawArc(
        IRectCanvas canvas,
        Rid rid,
        Vector2 center,
        float radius,
        float start,
        float end,
        int segments,
        Color color)
    {
        var points = new Vector2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float angle = start + (end - start) * i / segments;
            points[i] = center + new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius);
        }

        canvas.DrawPolyline(rid, points, color, 1.5f);
    }

    private static void DrawHitbox(
        IRectCanvas canvas,
        Rid rid,
        Rect2 bounds,
        Color color)
    {
        canvas.DrawRect(rid, bounds, DebugPalette.HitboxFill);
        DrawBorder(canvas, rid, bounds, color, 1.5f);
    }
}
