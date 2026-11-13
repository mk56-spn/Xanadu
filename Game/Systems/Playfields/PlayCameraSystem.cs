using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Keeps the player in view on a playfield bigger than the screen: the camera
/// eases towards centring the player, stops at the field's edges, jumps
/// straight there when the player does (a wrap, a restart, a seek), and shakes
/// briefly when a note is hit. While the player drops into a run, it holds on
/// the first note instead.
/// </summary>
public sealed class PlayCameraSystem : QuerySystem<PlayCamera, Player, Position, PlayfieldView, Playfield>
{
    /// <summary>
    /// How quickly the view catches up with the player, per second, while the
    /// player is near the middle of the screen: barely drifting.
    /// </summary>
    public const float FollowNear = 0.5f;

    /// <summary>
    /// How quickly the view catches up once the player reaches the screen's
    /// edge, per second, so they never run out of view.
    /// </summary>
    public const float FollowFar = 9f;

    /// <summary>
    /// The follow rate for a player <paramref name="away"/> from the centring
    /// scroll: rises with the square of the distance, measured against half
    /// the viewport on each axis, so small offsets drift and big ones snap.
    /// </summary>
    public static float FollowRate(Vector2 away, Vector2 viewportSize)
    {
        Vector2 half = viewportSize / 2;
        float reach = Math.Min(new Vector2(away.X / Math.Max(half.X, 1f), away.Y / Math.Max(half.Y, 1f)).Length(), 1f);
        return FollowNear + (FollowFar - FollowNear) * reach * reach;
    }

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
            if (!camera.DroppedIn && DropFocus(ref camera, entity, view, out Vector2 focus))
            {
                camera.Offset = Target(camera, focus, view, playfield);
                camera.Placed = true;
                return;
            }

            Vector2 target = Target(camera, PlayerSmoothing.DrawnPosition(entity), view, playfield);
            bool jumped = (target - camera.Offset).Length() > camera.Viewport.Size.Length() / 3;
            camera.Offset = !camera.Placed || jumped
                ? target
                : camera.Offset.Lerp(target, 1f - MathF.Exp(-FollowRate(target - camera.Offset, camera.Viewport.Size) * delta));
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

    /// <summary>
    /// Where the view looks while the player drops in: the run's first note, so
    /// it is on screen through the countdown and she falls into frame onto it.
    /// The hold ends, for good, once that note is hit or its time comes; false
    /// then, and for a run with no note ahead.
    /// </summary>
    private static bool DropFocus(ref PlayCamera camera, Entity entity, PlayfieldView view, out Vector2 focus)
    {
        focus = default;
        if (!entity.TryGetComponent(out PlaySession session) || !entity.TryGetComponent(out Playhead playhead))
        {
            camera.DroppedIn = true;
            return false;
        }

        if (camera.DropNote.IsNull && !camera.Placed)
            camera.DropNote = FirstNote(entity.Store, session.StartTimeMs);

        if (camera.DropNote.IsNull || !camera.DropNote.HasComponent<Note>())
        {
            camera.DroppedIn = true;
            return false;
        }

        Note note = camera.DropNote.GetComponent<Note>();
        if (session.Started && playhead.TimeMs >= note.TimeMs)
        {
            camera.DroppedIn = true;
            return false;
        }

        focus = view.PlayfieldToScreen(GroupOffset.Shown(camera.DropNote, note.Position));
        return true;
    }

    /// <summary>The earliest note at or after <paramref name="fromMs"/>, or a null entity when there is none.</summary>
    private static Entity FirstNote(EntityStore store, long fromMs)
    {
        Entity first = default;
        long firstMs = long.MaxValue;
        foreach (Entity note in store.Query<Note>().ToEntityList())
        {
            long timeMs = note.GetComponent<Note>().TimeMs;
            if (timeMs >= fromMs && timeMs < firstMs)
            {
                first = note;
                firstMs = timeMs;
            }
        }

        return first;
    }
}
