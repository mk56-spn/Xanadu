using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Keeps the player in view on a playfield bigger than the screen: the camera
/// eases towards centring the player, stops at the field's edges, jumps
/// straight there when the player does (a wrap, a restart, a seek), and shakes
/// briefly when a note is hit.
/// </summary>
public sealed class PlayCameraSystem : QuerySystem<PlayCamera, Player, Position, PlayfieldView, Playfield>
{
    /// <summary>How quickly the view catches up with the player, per second.</summary>
    public const float Follow = 7f;

    /// <summary>How far a full kick knocks the view, in pixels.</summary>
    public const float KickPixels = 7f;

    /// <summary>How quickly a kick dies away, per second.</summary>
    public const float KickDecay = 16f;

    private readonly Random _random = new(7);

    /// <summary>Gives the camera on a play entity a kick, for a hit; does nothing without one.</summary>
    public static void Kick(Entity playfield, float strength = 1f)
    {
        if (playfield.IsNull || !playfield.HasComponent<PlayCamera>())
            return;

        ref PlayCamera camera = ref playfield.GetComponent<PlayCamera>();
        camera.Kick = Math.Max(camera.Kick, strength);
    }

    /// <summary>
    /// The scroll that puts <paramref name="player"/> in the middle of the
    /// viewport, held so the view never runs past the field; along an axis where
    /// the field is no bigger than the viewport, the field is centred instead.
    /// </summary>
    public static Vector2 Target(PlayCamera camera, Vector2 player, PlayfieldView view, Playfield playfield)
    {
        Rect2 field = PlayfieldRenderer.FieldRect(view, playfield);
        return new Vector2(
            Axis(player.X, field.Position.X, field.Size.X, camera.Viewport.Position.X, camera.Viewport.Size.X),
            Axis(player.Y, field.Position.Y, field.Size.Y, camera.Viewport.Position.Y, camera.Viewport.Size.Y));
    }

    private static float Axis(float player, float fieldStart, float fieldSize, float viewStart, float viewSize)
    {
        if (fieldSize <= viewSize)
            return fieldStart + fieldSize / 2 - (viewStart + viewSize / 2);

        float centred = player - (viewStart + viewSize / 2);
        return Math.Clamp(centred, fieldStart - viewStart, fieldStart + fieldSize - viewStart - viewSize);
    }

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        Query.ForEachEntity((ref camera, ref player, ref position, ref view, ref playfield, entity) =>
        {
            Vector2 target = Target(camera, position.Value, view, playfield);
            bool jumped = (target - camera.Offset).Length() > camera.Viewport.Size.Length() / 3;
            camera.Offset = !camera.Placed || jumped
                ? target
                : camera.Offset.Lerp(target, 1f - MathF.Exp(-Follow * delta));
            camera.Placed = true;

            camera.Kick *= MathF.Exp(-KickDecay * delta);
            if (camera.Kick < 0.02f)
                camera.Kick = 0f;
            float angle = (float)(_random.NextDouble() * Math.Tau);
            camera.KickOffset = camera.Kick > 0f
                ? new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * KickPixels * camera.Kick
                : Vector2.Zero;
        });
    }
}
