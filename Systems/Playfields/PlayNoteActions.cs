using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Settings;
using Xanadu.Systems.Characters;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;
using NVector2 = System.Numerics.Vector2;

namespace Xanadu.Systems.Playfields;

/// <summary>What a hit note does to the run: character and weapon swaps, channels, construction, contact.</summary>
public sealed class PlayNoteActions(PlayNoteContext context)
{
    public bool Hit(
        ref ScoreState score,
        Entity noteEntity,
        long offsetMs,
        Entity playfield,
        KeybindAction action)
    {
        Note note = noteEntity.GetComponent<Note>();

        if (note.Kind == NoteKind.CharacterSwitch)
            ApplyCharacter(playfield, note.Character);
        else if (note.Kind == NoteKind.WeaponSwitch)
            ApplyWeapon(playfield, action);
        else if (note.Kind == NoteKind.Channel)
            StartChannel(playfield, note);
        else if (note.Kind == NoteKind.Construction)
            StartConstruction(playfield, note);
        else if (note.Kind == NoteKind.ConstructionStep)
            BuildConstructionStep(playfield, note);

        PlayScoring.Apply(ref score, offsetMs);

        // Normal and pair hits feed the Contact bank on the side that was
        // pressed. Only Contact accumulates; the bank itself survives a
        // character switch.
        if ((note.Kind == NoteKind.Hit || note.Kind == NoteKind.Pair) &&
            playfield.HasComponent<ContactCounters>() &&
            BanksContact(playfield) &&
            NoteRules.HasPairIndex(action))
        {
            playfield.GetComponent<ContactCounters>().Add(action);
        }

        noteEntity.DeleteEntity();
        return true;
    }

    /// <summary>Whether the active character banks side hits for Contact barriers.</summary>
    private static bool BanksContact(Entity playfield) =>
        !playfield.TryGetComponent(out PlayerCharacter character) ||
        character.Kind == CharacterKind.Contact;

    private static void ApplyWeapon(Entity playfield, KeybindAction action)
    {
        WeaponKind kind = NoteRules.IsLeft(action)
            ? WeaponKind.Spear
            : WeaponKind.Axe;
        if (playfield.HasComponent<ArmedWeapon>())
            playfield.GetComponent<ArmedWeapon>().Kind = kind;
        else
            playfield.AddComponent(new ArmedWeapon(kind));
    }

    /// <summary>
    /// Absorbs one side until its authored end marker. The end marker is chart
    /// data; once consumed it leaves the playfield.
    /// </summary>
    private void StartChannel(Entity playfield, Note start)
    {
        long endMs = start.TimeMs + 4_000;
        Entity endMarker = default;

        foreach (Entity entity in context.Entities.Query<Note>().ToEntityList())
        {
            Note candidate = entity.GetComponent<Note>();
            if (!NoteRules.IsChannelEnd(candidate.Kind) ||
                candidate.ChannelSide != start.ChannelSide ||
                candidate.TimeMs < start.TimeMs)
            {
                continue;
            }

            if (!endMarker.IsNull && candidate.TimeMs >= endMs)
                continue;

            endMs = candidate.TimeMs;
            endMarker = entity;
        }

        if (!endMarker.IsNull)
            endMarker.DeleteEntity();

        if (!playfield.HasComponent<SplitlineState>())
            playfield.AddComponent(new SplitlineState());

        ref SplitlineState state = ref playfield.GetComponent<SplitlineState>();
        if (start.ChannelSide == ChannelSide.Left)
        {
            state.LeftActive = true;
            state.LeftEndMs = endMs;
        }
        else
        {
            state.RightActive = true;
            state.RightEndMs = endMs;
        }
    }

    public static bool CanFormStep(Entity playfield, Note note)
    {
        if (note.Kind != NoteKind.ConstructionStep)
            return true;

        return playfield.TryGetComponent(out ConstructionState state) &&
               state.Active &&
               state.ShapeId == note.ShapeId;
    }

    private void StartConstruction(Entity playfield, Note note)
    {
        int total = 0;
        foreach (Entity entity in context.Entities.Query<Block>().ToEntityList())
        {
            if (entity.GetComponent<Block>().ShapeId == note.ShapeId)
                total++;
        }

        if (!playfield.HasComponent<ConstructionState>())
            playfield.AddComponent(new ConstructionState());

        ref ConstructionState state =
            ref playfield.GetComponent<ConstructionState>();
        state.ShapeId = note.ShapeId;
        state.BuiltPieces = 0;
        state.TotalPieces = total;
        state.Active = total > 0;
    }

    private void BuildConstructionStep(Entity playfield, Note note)
    {
        if (!playfield.TryGetComponent(out ConstructionState state) ||
            !state.Active ||
            state.ShapeId != note.ShapeId)
        {
            return;
        }

        PlayfieldView view = playfield.GetComponent<PlayfieldView>();
        foreach (Entity entity in context.Entities.Query<Block>().ToEntityList())
        {
            Block block = entity.GetComponent<Block>();
            if (block.ShapeId != note.ShapeId ||
                block.ShapePiece != note.ShapePiece)
            {
                continue;
            }

            entity.RemoveTag<ConstructionHidden>();
            context.Entities.CreateEntity(new ParticleBurst(
                view.PlayfieldToScreen(GroupOffset.Shown(entity, block.Center)),
                ParticleKind.Dash,
                PlayfieldRenderer.ColorFor(note)));
            break;
        }

        ref ConstructionState current =
            ref playfield.GetComponent<ConstructionState>();
        current.BuiltPieces++;
        if (current.BuiltPieces >= current.TotalPieces)
            current.Active = false;
    }

    private static void ApplyCharacter(Entity playfield, CharacterKind kind)
    {
        if (!playfield.HasComponent<PlayerCharacter>())
            return;

        playfield.GetComponent<PlayerCharacter>().Kind = kind;

        // The contact bank is a run resource; switching away only stops it
        // growing. The special rules for the other characters arrive with
        // their kits.
        if (kind == CharacterKind.Contact &&
            !playfield.HasComponent<ContactCounters>())
        {
            playfield.AddComponent(new ContactCounters());
        }

        if (kind == CharacterKind.Armed &&
            !playfield.HasComponent<ArmedWeapon>())
        {
            playfield.AddComponent(new ArmedWeapon(WeaponKind.Spear));
        }
    }
}
