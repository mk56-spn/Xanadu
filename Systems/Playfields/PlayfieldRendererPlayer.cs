using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Characters;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

public static partial class PlayfieldRenderer
{
    public static void DrawPlayer(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view)
    {
        foreach (Entity entity in entities.Query<Player, Position>().ToEntityList())
        {
            if (entity.Tags.Has<SplitlineAbsent>() ||
                entity.Tags.Has<PlayerShattered>())
            {
                return;
            }

            Player player = entity.GetComponent<Player>();
            Vector2 center = PlayerSmoothing.DrawnPosition(entity);
            if (entity.TryGetComponent(out PlayCamera camera))
                center += camera.Subpixel;
            Vector2 size = player.Size;

            // The character rig's art stands in for the box once it is on screen.
            if (entity.Tags.Has<CharacterRigVisible>())
                return;

            var bounds = new Rect2(
                center - size / 2,
                size);

            canvas.DrawRect(rid, bounds, PlayerColor);
            canvas.DrawBorder(rid, bounds, QueueColor, 2);
            return;
        }
    }

    /// <summary>
    /// The see-through boxes a player without rig art leaves when it hits a
    /// note; the rig draw system draws the rest as copies of the art.
    /// </summary>
    public static void DrawHitGhosts(IRectCanvas canvas, Rid rid, EntityStore entities)
    {
        foreach (Entity entity in entities.Query<HitGhost>().ToEntityList())
        {
            if (entity.Tags.Has<HitGhostRigged>())
                continue;

            HitGhost ghost = entity.GetComponent<HitGhost>();
            float alpha = ghost.Alpha;
            if (alpha <= 0f)
                continue;

            var bounds = new Rect2(ghost.Center - ghost.Size / 2, ghost.Size);
            canvas.DrawRect(rid, bounds, PlayerColor with { A = PlayerColor.A * alpha });
            canvas.DrawBorder(rid, bounds, QueueColor with { A = QueueColor.A * alpha }, 2);
        }
    }


}
