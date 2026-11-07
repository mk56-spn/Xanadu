using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The selected effect's cycle row: repeat it ×N, a new cycle every so many
/// beats on the song's tempo map (so repeats stay on the music through BPM
/// changes), each cycle playing as RESTART, RETURN or STACK. Every change is
/// one undo step.
/// </summary>
public sealed partial class EditorGroupSystem
{
    /// <summary>The cycle spacing steps: halved or doubled between these, in beats.</summary>
    private const float MinEveryBeats = 0.25f;
    private const float MaxEveryBeats = 64f;

    private static readonly EditorGroupPart[] CycleParts =
    {
        EditorGroupPart.CycleTitle, EditorGroupPart.CycleCount, EditorGroupPart.CycleCountDown,
        EditorGroupPart.CycleCountUp, EditorGroupPart.CycleEveryText, EditorGroupPart.CycleEveryDown,
        EditorGroupPart.CycleEveryUp, EditorGroupPart.CycleMode,
    };

    private static readonly GroupCycle[] CycleModes = { GroupCycle.Restart, GroupCycle.Return, GroupCycle.Stack };

    /// <summary>Count − / +, spacing − / +, and the mode list, on the selected effect.</summary>
    private void HandleCycle(ref EditorGroupState state, GroupDefinition group, Playhead playhead)
    {
        bool fewer = JustPressed(Piece(EditorGroupPart.CycleCountDown));
        bool more = JustPressed(Piece(EditorGroupPart.CycleCountUp));
        bool tighter = JustPressed(Piece(EditorGroupPart.CycleEveryDown));
        bool looser = JustPressed(Piece(EditorGroupPart.CycleEveryUp));
        Entity modeChoice = Piece(EditorGroupPart.CycleMode);
        int? chosen = null;
        if (!modeChoice.IsNull && modeChoice.TryGetComponent(out DropdownChanged changed))
        {
            chosen = changed.Value;
            CommandBuffer.RemoveComponent<DropdownChanged>(modeChoice.Id);
        }

        if (!TrySelected(state, group, out int row, out GroupEffect effect))
            return;

        if (fewer || more)
            SetCycles(group, row, effect, effect.Cycles + (more ? 1 : -1), playhead);
        if (tighter || looser)
        {
            float every = Math.Clamp(effect.EveryBeats * (looser ? 2f : 0.5f), MinEveryBeats, MaxEveryBeats);
            if (Math.Abs(every - effect.EveryBeats) > 1e-6f)
                Edit(() => group.Effects[row] = group.Effects[row] with { EveryBeats = every });
        }

        if (chosen is { } index)
        {
            GroupCycle mode = CycleModes[Math.Clamp(index, 0, CycleModes.Length - 1)];
            if (mode != effect.Cycle)
                Edit(() => group.Effects[row] = group.Effects[row] with { Cycle = mode });
        }
    }

    /// <summary>
    /// Sets how many times an effect plays. Going from once to repeating picks
    /// a spacing that fits the effect: the smallest of ¼, ½, 1, 2, 4… beats it
    /// fits inside.
    /// </summary>
    private void SetCycles(GroupDefinition group, int row, GroupEffect effect, int cycles, Playhead playhead)
    {
        cycles = Math.Clamp(cycles, 1, GroupEffect.MaxCycles);
        if (cycles == effect.Cycles)
            return;

        float every = effect.EveryBeats;
        if (effect.Cycles <= 1 && cycles > 1)
        {
            TempoMap tempo = playhead.Tempo;
            double length = tempo.BeatAt(effect.End * 1000.0) - tempo.BeatAt(effect.Start * 1000.0);
            every = MinEveryBeats;
            while (every < length - 1e-4 && every < MaxEveryBeats)
                every *= 2f;
        }

        Edit(() => group.Effects[row] = group.Effects[row] with { Cycles = cycles, EveryBeats = every });
    }

    /// <summary>Typing a count sets it once the field loses focus; read before the row refreshes it.</summary>
    private void CommitCycleFields(GroupDefinition group, EditorGroupState state, Playhead playhead)
    {
        CommitIfBlurred(Piece(EditorGroupPart.CycleCount), text =>
        {
            if (!TrySelected(state, group, out int row, out GroupEffect effect) ||
                !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cycles))
            {
                return;
            }

            SetCycles(group, row, effect, cycles, playhead);
        });
    }

    private void ShowCycle(
        EditorGroupState state,
        GroupDefinition group,
        Vector2 dialog,
        Vector2 origin,
        CommandBuffer buffer)
    {
        Entity mode = Piece(EditorGroupPart.CycleMode);
        if (state.PaletteOpen)
        {
            foreach (EditorGroupPart part in CycleParts)
                Hide(Piece(part), buffer);
            UiLayout.SetDisabled(mode, buffer, true);
            return;
        }

        foreach (EditorGroupPart part in CycleParts)
            Place(Piece(part), EditorGroupLayout.Place(part, 0, dialog), origin, buffer);

        bool selected = TrySelected(state, group, out _, out GroupEffect effect);
        Entity title = Piece(EditorGroupPart.CycleTitle);
        UiText.Set(title, !selected ? "CYCLE" : effect.Cycling ? "CYCLE ×" + effect.Cycles : "CYCLE  ONCE", buffer);
        UiText.SetColor(title, selected && effect.Cycling ? Theme.Palette.Accent : Theme.Palette.TextDim, buffer);

        Entity count = Piece(EditorGroupPart.CycleCount);
        if (!count.IsNull && !count.Tags.Has<TextFieldFocused>())
            SetFieldText(count, selected ? effect.Cycles.ToString(CultureInfo.InvariantCulture) : "", buffer);
        UiText.Set(Piece(EditorGroupPart.CycleEveryText), selected ? "EVERY " + BeatsLabel(effect.EveryBeats) : "EVERY –", buffer);

        bool cycling = selected && effect.Cycling;
        Tint(Piece(EditorGroupPart.CycleCountDown), cycling ? ActionIdle : Unavailable, buffer);
        Tint(Piece(EditorGroupPart.CycleCountUp), selected ? ActionIdle : Unavailable, buffer);
        Tint(Piece(EditorGroupPart.CycleEveryDown), cycling && effect.EveryBeats > MinEveryBeats ? ActionIdle : Unavailable, buffer);
        Tint(Piece(EditorGroupPart.CycleEveryUp), cycling && effect.EveryBeats < MaxEveryBeats ? ActionIdle : Unavailable, buffer);
        UiLayout.SetDisabled(count, buffer, !selected);

        UiText.Set(mode, (selected ? effect.Cycle.ToString().ToUpperInvariant() : "RESTART") + " ▾", buffer);
        Tint(mode, cycling ? ActionIdle : Unavailable, buffer);
        UiLayout.SetDisabled(mode, buffer, !cycling);
        if (selected && mode.HasComponent<Dropdown>() && !mode.HasComponent<DropdownChanged>())
            mode.GetComponent<Dropdown>().Selected = Array.IndexOf(CycleModes, effect.Cycle);
    }

    /// <summary>A spacing as it reads: 1/4 BEAT, 1/2 BEAT, 1 BEAT, 2 BEATS…</summary>
    public static string BeatsLabel(float beats)
    {
        if (beats < 1f)
            return $"1/{Math.Round(1f / beats)} BEAT";
        string whole = beats.ToString("0.##", CultureInfo.InvariantCulture);
        return beats == 1f ? "1 BEAT" : whole + " BEATS";
    }
}
