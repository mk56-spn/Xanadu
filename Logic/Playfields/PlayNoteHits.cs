using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;
using Position = Xanadu.Core.Position;
using Xanadu.Model.Playfields;

namespace Xanadu.Logic.Playfields;

/// <summary>Applies a judged action-note hit: the trail, the judgement and the effects.</summary>
public sealed class PlayNoteHits(PlayNoteContext context, PlayNoteActions actions)
{
    public bool Note(
        KeybindAction action,
        Entity noteEntity,
        Note note,
        long offsetMs,
        Entity playfield,
        ref ScoreState score,
        PlayfieldView view,
        Vector2 playerScreen,
        long playheadTimeMs)
    {
        Vector2 hitPosition = view.PlayfieldToScreen(note.Position);
        Color hitColor = PlayfieldColors.ColorFor(note);
        float hitRadius = PlayfieldMetrics.NoteSize * view.Scale / 2f;
        score.JudgementPosition = hitPosition;
        score.JudgementTimeMs = playheadTimeMs;

        context.Entities.CreateEntity(
            new HitTrail(
                hitPosition,
                PlayfieldColors.ColorFor(note),
                KeybindActions.KeyLabel(context.Keybinds.Get(action))));

        // Read before the hit takes the note away.
        int chartIndex = ChartIndex.Of(noteEntity);
        if (!actions.Hit(ref score, noteEntity, offsetMs, playfield, action))
            return false;

        HitEffects.Spawn(context.Entities, playfield, noteEntity, note, hitPosition, hitColor, hitRadius, chartIndex);
        return true;
    }
}
