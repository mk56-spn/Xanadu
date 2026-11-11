using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Samples the player's real hitbox position during an editor preview. A sample
/// is taken when it has moved far enough or enough time has passed, so a long
/// song stays cheap to keep and to draw while the time along the path stays true.
/// </summary>
public sealed class PlayerPathRecordSystem
    : QuerySystem<PlayerPathRecorder, Position, PlayfieldView, Playhead, PlaySession>
{
    /// <summary>Field units moved before the next sample.</summary>
    public const float MinMove = 2f;

    /// <summary>Song time between samples of a player that is not moving.</summary>
    public const long MaxGapMs = 100;

    /// <summary>Screen pixels of movement beyond what its speed explains that count as a jump.</summary>
    private const float JumpSlack = 4f;

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        Query.ForEachEntity((ref recorder, ref position, ref view, ref playhead, ref session, entity) =>
        {
            if (!session.Started || session.Finished ||
                entity.Tags.Has<PlayPaused>() ||
                !entity.TryGetComponent(out PhysicsBody body))
            {
                return;
            }

            // A hit always gets a sample, however little the player has moved.
            int notesHit = entity.TryGetComponent(out ScoreState score) ? score.NotesHit : 0;
            bool hit = notesHit > recorder.NotesHit;
            recorder.NotesHit = notesHit;

            List<PlayerPathSample> samples = recorder.Samples;
            Vector2 at = view.ScreenToPlayfield(position.Value);
            if (samples.Count == 0)
            {
                samples.Add(new PlayerPathSample(at, playhead.TimeMs, false, hit));
                return;
            }

            PlayerPathSample last = samples[^1];
            float moved = (at - last.Position).Length();
            if (!hit && moved < MinMove && playhead.TimeMs - last.TimeMs < MaxGapMs)
                return;

            float speed = body.Body.LinearVelocity.Length() * Box2DPhysicsWorld.PixelsPerMeter;
            float elapsed = System.Math.Max(delta, (playhead.TimeMs - last.TimeMs) / 1000f);
            bool jump = moved * view.Scale > speed * elapsed * 2f + JumpSlack;
            samples.Add(new PlayerPathSample(at, playhead.TimeMs, jump, hit));
        });
    }
}
