using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Settings;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Resolves Contact barriers automatically when the character's melee circle
/// touches one. A barrier drains both side costs as far as it can and applies
/// an accuracy penalty when either side is short; it never kills.
/// </summary>
public sealed class ContactBarrierSystem : QuerySystem<Player, Position, PlayfieldView, Playhead>
{
    private readonly EntityStore _entities;
    private readonly OffsetSettings _offsets;

    public ContactBarrierSystem(
        EntityStore entities,
        OffsetSettings? offsets = null)
    {
        _entities = entities;
        _offsets = offsets ?? new OffsetSettings(persist: false);
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        long window = (long)ScoreRules.JudgementDeviation(Judgement.Miss);

        Query.ForEachEntity((ref player, ref position, ref view, ref playhead, entity) =>
        {
            if (entity.Tags.Has<PlayPaused>() ||
                entity.Tags.Has<SplitlineAbsent>() ||
                IsWaiting(entity))
            {
                return;
            }

            bool contact =
                entity.TryGetComponent(out PlayerCharacter character) &&
                character.Kind == CharacterKind.Contact &&
                entity.HasComponent<ContactCounters>();
            Vector2 playerScreen = position.Value;
            long judgedTime = playhead.TimeMs - _offsets.OffsetMs;

            foreach (Entity noteEntity in _entities.Query<Note>().ToEntityList())
            {
                Note note = noteEntity.GetComponent<Note>();
                if (note.Kind != NoteKind.Contact ||
                    noteEntity.Tags.Has<NoteMissed>())
                {
                    continue;
                }

                long delta = note.TimeMs - judgedTime;
                if (delta < -window)
                {
                    // Barriers are checks, not scored notes; one the player
                    // never reached simply leaves the field.
                    buffer.DeleteEntity(noteEntity.Id);
                    continue;
                }

                if (!contact ||
                    Math.Abs(delta) > window ||
                    NoteFade.Alpha(note, judgedTime) <= 0)
                {
                    continue;
                }

                Vector2 noteScreen = view.PlayfieldToScreen(note.Position);
                if (!NoteReach.Contains(playerScreen, view, noteScreen))
                    continue;

                (int left, int right) = ContactRules.Costs(note);
                ref ContactCounters counters = ref entity.GetComponent<ContactCounters>();
                bool paid = counters.Consume(left, right);

                if (!paid && entity.HasComponent<ScoreState>())
                {
                    ref ScoreState score = ref entity.GetComponent<ScoreState>();
                    PlayScoring.FailContact(ref score, noteScreen, playhead.TimeMs);
                }

                _entities.CreateEntity(new HitTrail(
                    noteScreen,
                    paid ? PlayfieldRenderer.ColorFor(note) : Palette.DangerBright,
                    paid ? "PASS" : "BREAK"));
                buffer.DeleteEntity(noteEntity.Id);
            }
        });
    }

    private static bool IsWaiting(Entity entity)
    {
        if (!entity.TryGetComponent(out PlaySession session))
            return false;

        return !session.Started || session.Finished || session.Died;
    }
}
