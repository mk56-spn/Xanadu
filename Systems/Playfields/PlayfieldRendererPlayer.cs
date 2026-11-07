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

            // Far from every note the character is gone and a rhombus keeps the beat.
            CharacterPresenceRules.Look look = CharacterPresenceRules.For(entity);
            if (!look.Shown)
            {
                DrawRestRhombus(canvas, rid, center, look);
                return;
            }

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

    /// <summary>The resting rhombus's side, in pixels: a square stood on its corner.</summary>
    public const float RestRhombusSide = 40f;

    /// <summary>How far out from the rhombus its outline runs, in pixels, edge to edge.</summary>
    public const float RestRhombusRingGap = 20f;

    /// <summary>How much bigger the filled rhombus is on the beat.</summary>
    public const float RestRhombusPulse = 0.1f;

    /// <summary>How far, in degrees, the rhombus turns as it comes in or goes out.</summary>
    public const float RestRhombusTurn = 90f;

    public static readonly Color RestRhombusColor = Palette.Info;

    /// <summary>
    /// A square stood on its corner where the player is, inside a thin white
    /// outline of the same shape. It turns and grows in as <paramref name="look"/>
    /// opens, and the fill swells and brightens on each beat.
    /// </summary>
    private static void DrawRestRhombus(
        IRectCanvas canvas, Rid rid, Vector2 center, CharacterPresenceRules.Look look)
    {
        if (look.Rhombus <= 0f)
            return;

        float angle = Mathf.DegToRad(45f + RestRhombusTurn * (1f - look.Rhombus));
        float fillHalf = RestRhombusSide / 2f * look.Rhombus * (1f + RestRhombusPulse * look.Pulse);
        float ringHalf = (RestRhombusSide / 2f + RestRhombusRingGap) * look.Rhombus;

        Span<Vector2> fill = stackalloc Vector2[4];
        Square(fill, center, fillHalf, angle);
        Color fillColor = RestRhombusColor.Lerp(Palette.Text, look.Pulse) with { A = 0.6f + 0.4f * look.Pulse };
        canvas.DrawPolygon(rid, fill, fillColor);

        Span<Vector2> ring = stackalloc Vector2[5];
        Square(ring, center, ringHalf, angle);
        ring[4] = ring[0];
        canvas.DrawPolyline(rid, ring, Palette.Text with { A = look.Rhombus }, 1.5f);
    }

    /// <summary>The corners of a square <paramref name="half"/> from its centre to each side, turned by <paramref name="angle"/>.</summary>
    private static void Square(Span<Vector2> corners, Vector2 center, float half, float angle)
    {
        var right = new Vector2(half, 0).Rotated(angle);
        var down = new Vector2(0, half).Rotated(angle);
        corners[0] = center - right - down;
        corners[1] = center + right - down;
        corners[2] = center + right + down;
        corners[3] = center - right + down;
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
