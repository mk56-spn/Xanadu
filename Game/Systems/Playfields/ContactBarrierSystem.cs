using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Rendering;
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
    private readonly ArchetypeQuery<Note> _open;
    private readonly List<int> _reached = [];

    public ContactBarrierSystem(
        EntityStore entities,
        OffsetSettings? offsets = null)
    {
        _entities = entities;
        _offsets = offsets ?? new OffsetSettings(persist: false);
        _open = entities.Query<Note>().WithoutAnyTags(Tags.Get<NoteMissed>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        long window = (long)ScoreRules.JudgementDeviation(Judgement.Miss);

        Query.ForEachEntity((ref player, ref position, ref view, ref playhead, entity) =>
        {
            if (entity.Tags.Has<PlayPaused>() || IsWaiting(entity))
            {
                return;
            }

            bool contact =
                entity.TryGetComponent(out PlayerCharacter character) &&
                character.Kind == CharacterKind.Contact &&
                entity.HasComponent<ContactCounters>();
            Vector2 playerScreen = position.Value;
            long judgedTime = playhead.TimeMs - PlayOffset.TotalMs(entity, _offsets);

            _reached.Clear();
            _open.EachEntity(new ReachedBarriers(_entities, view, buffer, _reached, playerScreen, contact, judgedTime, window));

            foreach (int id in _reached)
            {
                Entity noteEntity = _entities.GetEntityById(id);
                Note note = noteEntity.GetComponent<Note>();
                Vector2 noteScreen = view.PlayfieldToScreen(GroupOffset.Shown(noteEntity, note.Position));
                ref ContactCounters counters = ref entity.GetComponent<ContactCounters>();
                bool paid = counters.Consume(note.Costs);
                if (paid)
                    ShaderPlay.Mark(ShaderEvent.BarrierPaid);

                if (!paid && entity.HasComponent<ScoreState>())
                {
                    ref ScoreState score = ref entity.GetComponent<ScoreState>();
                    PlayScoring.FailContact(ref score, noteScreen, playhead.TimeMs);
                }

                _entities.CreateEntity(
                    new HitTrail(
                        noteScreen,
                        paid ? PlayfieldRenderer.ColorFor(note) : Palette.DangerBright,
                        paid ? "PASS" : "BREAK"),
                    EffectLayer.From(noteEntity));
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

    /// <summary>
    /// Open barriers in the window that the Contact player's reach touches.
    /// Barriers whose window has passed are deleted on the way: they are
    /// checks, not scored notes, so one the player never reached simply leaves.
    /// </summary>
    private readonly struct ReachedBarriers(
        EntityStore store,
        PlayfieldView view,
        CommandBuffer buffer,
        List<int> reached,
        Vector2 playerScreen,
        bool contact,
        long judgedTime,
        long window) : IEachEntity<Note>
    {
        public void Execute(ref Note note, int id)
        {
            if (note.Kind != NoteKind.Contact)
                return;

            long delta = note.TimeMs - judgedTime;
            if (delta < -window)
            {
                buffer.DeleteEntity(id);
                return;
            }

            if (!contact || Math.Abs(delta) > window || NoteFade.Alpha(note, judgedTime) <= 0)
                return;

            Vector2 noteScreen = view.PlayfieldToScreen(GroupOffset.Shown(store.GetEntityById(id), note.Position));
            if (NoteReach.Contains(playerScreen, view, noteScreen, CharacterKind.Contact))
                reached.Add(id);
        }
    }
}
