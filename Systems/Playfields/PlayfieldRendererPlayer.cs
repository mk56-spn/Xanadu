using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Canvas;
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
            // The character rig's art stands in for the box once it is on screen.
            if (entity.Tags.Has<CharacterRigVisible>())
                return;

            Player player = entity.GetComponent<Player>();
            Vector2 center = entity.GetComponent<Position>().Value;
            Vector2 size = player.Size;
            var bounds = new Rect2(
                center - size / 2,
                size);

            canvas.DrawRect(rid, bounds, PlayerColor);
            DrawBorder(canvas, rid, bounds, QueueColor, 2);
            return;
        }
    }


}
