using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Scoring;
using Xanadu.Model.Settings;
using Xanadu.Systems.Input;
using Xanadu.Ui.Ecs;
using Position = Xanadu.Core.Position;
using Xanadu.Model.Playfields;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Picks what an action key press should judge: the nearest matching note in
/// reach and in the window.
/// </summary>
public sealed class PlayNoteJudge(PlayNoteContext context, PlayNoteHits hits)
{
    private readonly ArchetypeQuery<Note> _open =
        context.Entities.Query<Note>().WithoutAnyTags(Tags.Get<NoteMissed, NoteSteering, NoteHolding>());

    public void Note(KeybindAction action, Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        ref PlayfieldView view = ref playfield.GetComponent<PlayfieldView>();
        Vector2 playerScreen = playfield.GetComponent<Position>().Value;

        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
            return;

        long judgedTime = playhead.TimeMs - PlayOffset.TotalMs(playfield, context.Offsets);
        long missWindow = (long)ScoreRules.JudgementDeviation(Judgement.Miss);

        NearestNote nearest = _open.EachEntity(
            new NearestNote(context.Entities, view, action, playerScreen, judgedTime, missWindow));
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
            playhead.TimeMs);
    }

    /// <summary>The open action note matching the key that is nearest in time, in reach and visible.</summary>
    private struct NearestNote(
        EntityStore store,
        PlayfieldView view,
        KeybindAction action,
        Vector2 playerScreen,
        long judgedTime,
        long missWindow) : IEachEntity<Note>
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

            Vector2 noteScreen = view.PlayfieldToScreen(note.Position);
            if (!NoteReach.Contains(playerScreen, view, noteScreen))
                return;

            _bestError = error;
            Offset = offset;
            Id = id;
        }
    }
}
