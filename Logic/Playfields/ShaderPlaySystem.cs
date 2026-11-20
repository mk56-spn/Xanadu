using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Rendering;
using Xanadu.Core.Rendering.Rhythm;
using Xanadu.Model.Ecs.Components;
using Position = Xanadu.Core.Position;

namespace Xanadu.Logic.Playfields;

/// <summary>
/// Hands level shaders the run as plain values (<see cref="ShaderPlay"/>,
/// <c>rhythm.play</c>): where the player is and how it moves, whether it
/// stands, dashes or is dead, the score, the song's progress, beat and next
/// note, and Contact's bars. Read off the play scene each frame; the events
/// (hits, dashes, the countdown…) are marked where they happen instead.
/// </summary>
public sealed class ShaderPlaySystem : QuerySystem<PlayfieldMode, Player, Position, PlayfieldView, Playhead>
{
    protected override void OnUpdate()
    {
        Query.EachEntity(new Publish(Query.Store));
    }

    /// <summary>Seconds until the next note still waiting to be played, or never.</summary>
    private static float UntilNote(EntityStore store, long nowMs)
    {
        long next = store.Query<Note>().Each(new NextNote(nowMs)).Next;
        return next == long.MaxValue ? ShaderKeys.Never : (next - nowMs) / 1000f;
    }

    private readonly record struct Publish(EntityStore Store) : IEachEntity<PlayfieldMode, Player, Position, PlayfieldView, Playhead>
    {
        public void Execute(ref PlayfieldMode mode, ref Player player, ref Position position, ref PlayfieldView view, ref Playhead playhead, int id)
        {
            Entity entity = Store.GetEntityById(id);

            if (mode.Kind != PlayfieldKind.Play)
                return;

            // On the target: the camera moves the playfield layer by its shift.
            Vector2 shift = entity.TryGetComponent(out PlayCamera camera) ? camera.Shift : Vector2.Zero;
            ShaderPlay.Position = position.Value - shift;
            ShaderPlay.Velocity = entity.TryGetComponent(out CurveBody body)
                ? body.Velocity * view.Scale
                : Vector2.Zero;

            if (entity.TryGetComponent(out Playfield playfield))
            {
                ShaderPlay.PlayfieldCorner = view.ScreenOrigin - shift;
                ShaderPlay.PixelsPerUnit = view.Scale;
                ShaderPlay.PlayfieldSize = new Vector2(playfield.Width, playfield.Height) * view.Scale;
            }

            ShaderPlay.Dead = entity.TryGetComponent(out PlaySession session) && session.Died;

            if (entity.TryGetComponent(out ScoreState score))
            {
                ShaderPlay.Accuracy = (float)score.Accuracy;
                ShaderPlay.Combo = score.Combo;
                ShaderPlay.MaxCombo = score.MaxCombo;
                ShaderPlay.Score = score.Score;
                ShaderPlay.LastJudgement = score.HasJudgement ? (float)score.LastJudgement : 0;
            }

            ShaderPlay.SongProgress = playhead.DurationMs > 0
                ? Math.Clamp(playhead.TimeMs / (float)playhead.DurationMs, 0f, 1f)
                : 0f;
            ShaderPlay.SongSeconds = playhead.TimeMs / 1000f;
            ShaderPlay.Beat = (float)playhead.Tempo.BeatAt(playhead.TimeMs);
            ShaderPlay.Bpm = (float)playhead.Tempo.At(playhead.TimeMs).Bpm;
            ShaderPlay.UntilNote = UntilNote(entity.Store, playhead.TimeMs);
        }
    }

    private struct NextNote(long NowMs) : IEach<Note>
    {
        public long Next = long.MaxValue;

        public void Execute(ref Note note)
        {
            if (note.TimeMs >= NowMs && note.TimeMs < Next)
                Next = note.TimeMs;
        }
    }
}
