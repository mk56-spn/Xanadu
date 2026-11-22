using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Scoring;
using Xanadu.Model.Settings;
using Xanadu.Ui.Ecs;
using Position = Xanadu.Core.Position;
using Xanadu.Model.Playfields;

namespace Xanadu.Logic.Playfields;

/// <summary>
/// Picks what an action key press should judge: the nearest matching note in
/// the window. Where the player is doesn't matter: their beam drops into the
/// judgement line and hits it.
/// </summary>
public sealed class PlayNoteJudge(PlayNoteContext context, PlayNoteHits hits)
{
    private readonly ArchetypeQuery<Note> _open =
        context.Entities.Query<Note>().WithoutAnyTags(Tags.Get<NoteMissed>());

    /// <param name="pressMs">The song time the key went down (<see cref="Playhead.KeyMs"/>).</param>
    public void Note(KeybindAction action, Entity playfield, long pressMs)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        ref PlayfieldView view = ref playfield.GetComponent<PlayfieldView>();
        Vector2 playerScreen = playfield.GetComponent<Position>().Value;

        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
            return;

        long judgedTime = pressMs - PlayOffset.TotalMs(playfield, context.Offsets.OffsetMs);
        long missWindow = (long)ScoreRules.JudgementDeviation(Judgement.Miss);

        CurveBody body = playfield.TryGetComponent(out CurveBody found) ? found : default;
        NearestNote nearest = _open.EachEntity(
            new NearestNote(context.Entities, view, action, playerScreen, judgedTime, missWindow,
                body.Position, playfield.GetComponent<Playfield>(), body.Placed));
        if (nearest.Id == 0)
            return;

        Entity hit = context.Entities.GetEntityById(nearest.Id);
        Note hitNote = hit.GetComponent<Note>();
        long bestOffset = nearest.Offset;


        hits.Note(
            action,
            hit,
            hitNote,
            bestOffset,
            playfield,
            ref score,
            view,
            playerScreen,
            playhead.ShownMs);
    }

    /// <summary>The open action note matching the key that is nearest in time, in reach and visible.</summary>
    private struct NearestNote(
        EntityStore store,
        PlayfieldView view,
        KeybindAction action,
        Vector2 playerScreen,
        long judgedTime,
        long missWindow,
        Vector2 body,
        Playfield field,
        bool placed) : IEachEntity<Note>
    {
        public int Id;
        public long Offset;
        private long _bestError = long.MaxValue;

        public void Execute(ref Note note, int id)
        {
            if (!NoteRules.Matches(note, action))
                return;

            long offset = note.TimeMs - judgedTime;
            long error = Math.Abs(offset);
            if (error > missWindow || error >= _bestError)
                return;

            if (NoteFade.Alpha(note, judgedTime) <= 0)
                return;

            // An orb is taken by a dash that reaches its head, or with the body already on the head.
            if (note.IsOrb && (!placed || !Orbs.Catches(note, judgedTime, body, PlayRules.DashDirection(action), field)))
                return;

            _bestError = error;
            Offset = offset;
            Id = id;
        }
    }
}
