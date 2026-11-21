using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Scoring;
using Xanadu.Model.Settings;
using Position = Xanadu.Core.Position;

namespace Xanadu.Logic.Playfields;

/// <summary>What a note hit does to the run: the score, then the note goes.</summary>
public sealed class PlayNoteActions
{
    public bool Hit(
        ref ScoreState score,
        Entity noteEntity,
        long offsetMs,
        Entity playfield,
        KeybindAction action)
    {
        Judgement judgement = PlayScoring.Apply(ref score, offsetMs);
        PlayChart.Judge(noteEntity, judgement, offsetMs);

        noteEntity.DeleteEntity();
        return true;
    }
}
