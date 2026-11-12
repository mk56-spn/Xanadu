using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Systems.Physics;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Hands level shaders the run as plain values (<see cref="ShaderPlay"/>,
/// <c>xanadu.play</c>): where the player is and how it moves, whether it
/// stands, dashes or is dead, the score, the song's progress, beat and next
/// note, and Contact's bars. Read off the play scene each frame; the events
/// (hits, dashes, the countdown…) are marked where they happen instead.
/// </summary>
public sealed class ShaderPlaySystem : QuerySystem<PlayfieldMode, Player, Position, PlayfieldView, Playhead>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref mode, ref player, ref position, ref view, ref playhead, entity) =>
        {
            if (mode.Kind != PlayfieldKind.Play)
                return;

            Vector2 at = entity.TryGetComponent(out PlayerSmoothing smoothing) ? smoothing.Drawn : position.Value;
            ShaderPlay.Position = at;
            ShaderPlay.Velocity = entity.TryGetComponent(out PhysicsBody body)
                ? Box2DPhysicsWorld.ToScreen(body.Body.LinearVelocity)
                : Vector2.Zero;

            if (entity.TryGetComponent(out Playfield playfield))
            {
                ShaderPlay.PlayfieldCorner = view.ScreenOrigin;
                ShaderPlay.PlayfieldSize = new Vector2(playfield.Width, playfield.Height) * view.Scale;
                ShaderPlay.Grounded = PlayerGround.TryStanding(entity, view, playfield, position.Value, out _);
            }

            ShaderPlay.Dashing = entity.HasComponent<DashHang>();
            ShaderPlay.Dead = entity.Tags.Has<PlayerShattered>() ||
                              (entity.TryGetComponent(out PlaySession session) && session.Died);

            if (entity.TryGetComponent(out ScoreState score))
            {
                ShaderPlay.Accuracy = (float)score.Accuracy;
                ShaderPlay.Combo = score.Combo;
                ShaderPlay.MaxCombo = score.MaxCombo;
                ShaderPlay.Score = score.Score;
                ShaderPlay.LastJudgement = score.HasJudgement ? (float)score.LastJudgement : 0;
            }

            if (entity.TryGetComponent(out ContactCounters bars))
                ShaderPlay.ContactBars = new Vector3(bars.First, bars.Second, bars.Third);

            ShaderPlay.SongProgress = playhead.DurationMs > 0
                ? Math.Clamp(playhead.TimeMs / (float)playhead.DurationMs, 0f, 1f)
                : 0f;
            ShaderPlay.SongSeconds = playhead.TimeMs / 1000f;
            ShaderPlay.Beat = (float)playhead.Tempo.BeatAt(playhead.TimeMs);
            ShaderPlay.Bpm = (float)playhead.Tempo.At(playhead.TimeMs).Bpm;
            ShaderPlay.UntilNote = UntilNote(entity.Store, playhead.TimeMs);
        });
    }

    /// <summary>Seconds until the next note still waiting to be played, or never.</summary>
    private static float UntilNote(EntityStore store, long nowMs)
    {
        long next = long.MaxValue;
        store.Query<Note>().ForEachEntity((ref Note note, Entity _) =>
        {
            if (note.TimeMs >= nowMs && note.TimeMs < next)
                next = note.TimeMs;
        });
        return next == long.MaxValue ? ShaderKeys.Never : (next - nowMs) / 1000f;
    }
}
