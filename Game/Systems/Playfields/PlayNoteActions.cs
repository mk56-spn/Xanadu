using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Settings;
using Xanadu.Systems.Characters;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>What an action-note hit does to the run: the score, and Contact's bars.</summary>
public sealed class PlayNoteActions
{
    public bool Hit(
        ref ScoreState score,
        Entity noteEntity,
        long offsetMs,
        Entity playfield,
        KeybindAction action)
    {
        Note note = noteEntity.GetComponent<Note>();
        Judgement judgement = PlayScoring.Apply(ref score, offsetMs);
        PlayChart.Judge(noteEntity, judgement, offsetMs);

        // A neutral note loads the bar of the action key it was hit with.
        if (note.Kind == NoteKind.Neutral &&
            playfield.HasComponent<ContactCounters>() &&
            IsContact(playfield))
        {
            playfield.GetComponent<ContactCounters>().Add(action);
        }

        noteEntity.DeleteEntity();
        return true;
    }

    private static bool IsContact(Entity playfield) =>
        !playfield.TryGetComponent(out PlayerCharacter character) ||
        character.Kind == CharacterKind.Contact;
}
