using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Levels;
using Xanadu.Timeline;

namespace Xanadu.Systems.Editor;

// Reads the pointer outside UI (BannedSymbols.txt): still to port to a UI concept.
#pragma warning disable RS0030

/// <summary>The overlay's buttons: adding, selecting, duplicating, muting and nudging effects.</summary>
public sealed partial class EditorGroupSystem
{
    private void HandleButtons(Entity editor, ref EditorGroupState state, GroupDefinition group, Playhead playhead)
    {
        if (JustPressed(Piece(EditorGroupPart.Close)))
        {
            state.Open = -1;
            state.TargetEdit = false;
            state.PaletteOpen = false;
            state.DragRow = -1;
            return;
        }

        if (JustPressed(Piece(EditorGroupPart.SnapToggle)))
            state.Snap = !state.Snap;

        if (JustPressed(Piece(EditorGroupPart.SoloToggle)))
            state.SoloGroup = state.SoloGroup == state.Open ? -1 : state.Open;

        if (JustPressed(Piece(EditorGroupPart.PlayToggle)))
            _commands.Enqueue(EditorCommand.TogglePlayback());

        if (JustPressed(Piece(EditorGroupPart.ScrollLeft)))
            state.ViewStart = Math.Max(0f, state.ViewStart - state.ViewSpan / 2f);
        if (JustPressed(Piece(EditorGroupPart.ScrollRight)))
            state.ViewStart += state.ViewSpan / 2f;
        if (JustPressed(Piece(EditorGroupPart.ZoomOut)))
            state.ViewSpan = Math.Min(240f, state.ViewSpan * 2f);
        if (JustPressed(Piece(EditorGroupPart.ZoomIn)))
            state.ViewSpan = Math.Max(0.25f, state.ViewSpan / 2f);

        float now = SnapIf(state, playhead.TimeMs / 1000f, playhead);
        if (JustPressed(Piece(EditorGroupPart.AddMove)) && group.Effects.Count < EditorGroupLayout.MaxRows)
        {
            Edit(() => group.Effects.Add(GroupEffect.Move(now, now + 0.5f, new Vector2(0, -80))));
            Select(state.Open, group.Effects.Count - 1, ref state);
        }

        if (JustPressed(Piece(EditorGroupPart.BufferToggle)))
        {
            if (group.Buffered)
            {
                Edit(() => group.Buffered = false);
            }
            else if (editor.TryGetComponent(out LevelGroups all))
            {
                string? problem = GroupBufferRules.Problem(editor.Store, all.Groups, state.Open);
                if (problem is null)
                    Edit(() => group.Buffered = true);
                else
                    CommandBuffer.AddComponent(editor.Id, new WarningRequest(problem));
            }
        }

        if (JustPressed(Piece(EditorGroupPart.AddColor)) && group.Effects.Count < EditorGroupLayout.MaxRows)
        {
            Edit(() => group.Effects.Add(GroupEffect.Colour(now, now + 0.5f, DefaultTint)));
            Select(state.Open, group.Effects.Count - 1, ref state);
        }

        int open = state.Open;
        if (JustPressed(Piece(EditorGroupPart.AddSelected)))
            Membership(editor, open, add: true);
        if (JustPressed(Piece(EditorGroupPart.RemoveSelected)))
            Membership(editor, open, add: false);
        if (JustPressed(Piece(EditorGroupPart.SelectMembers)))
            SelectMembers(editor, open);
        if (JustPressed(Piece(EditorGroupPart.InteriorLink)))
            Edit(() => ToggleColorLink(editor, open, outline: false));
        if (JustPressed(Piece(EditorGroupPart.OutlineLink)))
            Edit(() => ToggleColorLink(editor, open, outline: true));

        HandleShaderTrack(editor, ref state, group, playhead);
        HandleCycle(ref state, group, playhead);

        if (state.PaletteOpen)
            HandlePalette(ref state, group);

        bool selected = TrySelected(state, group, out int row, out GroupEffect effect);
        float step = Math.Max(0.001f, EditorGroupLayout.StepSeconds(effect.End, playhead, 1) - effect.End);

        if (JustPressed(Piece(EditorGroupPart.SelectionDuplicate)) && selected &&
            group.Effects.Count < EditorGroupLayout.MaxRows)
        {
            float length = Math.Max(effect.End - effect.Start, step);
            GroupEffect copy = effect with
            {
                Start = effect.End,
                End = effect.End + length,
                Muted = false,
            };
            Edit(() => group.Effects.Insert(row + 1, copy));
            Select(state.Open, row + 1, ref state);
        }

        if (JustPressed(Piece(EditorGroupPart.SelectionMute)) && selected)
            Edit(() => group.Effects[row] = effect with { Muted = !effect.Muted });

        if (JustPressed(Piece(EditorGroupPart.SelectionNudgeLeft)) && selected)
            Nudge(group, row, effect, playhead, -1);
        if (JustPressed(Piece(EditorGroupPart.SelectionNudgeRight)) && selected)
            Nudge(group, row, effect, playhead, 1);

        if (JustPressed(Piece(EditorGroupPart.SelectionPick)))
        {
            state.PaletteOpen = selected && effect.Kind == GroupEffectKind.Color &&
                                !(state.PaletteOpen && !state.PaletteForBuffer);
            state.PaletteForBuffer = false;
        }

        if (JustPressed(Piece(EditorGroupPart.SelectionTarget)))
        {
            if (!selected)
            {
                int firstMove = FirstMove(group);
                if (firstMove >= 0)
                {
                    Select(state.Open, firstMove, ref state);
                    selected = true;
                    effect = group.Effects[firstMove];
                }
            }

            if (selected && effect.Kind == GroupEffectKind.Move)
            {
                state.TargetEdit = true;
                state.Open = -1;
                state.PaletteOpen = false;
                state.DragRow = -1;
                _pointerHeld = _pointer.Read().Pressed;
                return;
            }
        }

        for (int i = 0; i < group.Effects.Count && i < EditorGroupLayout.MaxRows; i++)
        {
            int index = i;
            if (JustPressed(Piece(EditorGroupPart.RowDelete, i)))
            {
                Edit(() => group.Effects.RemoveAt(index));
                if (state.SelectedEffect == index)
                    state.SelectedEffect = -1;
                else if (state.SelectedEffect > index)
                    state.SelectedEffect--;
                return;
            }

            bool kindPressed = JustPressed(Piece(EditorGroupPart.RowKind, i));
            bool easePressed = JustPressed(Piece(EditorGroupPart.RowEase, i));
            if (kindPressed || easePressed)
                Select(state.Open, i, ref state);

            if (kindPressed)
                Edit(() => group.Effects[index] = Toggled(group.Effects[index]));

            if (easePressed)
                ToggleEasePicker(ref state, EditorEaseTarget.Effect, Piece(EditorGroupPart.RowEase, i));
        }
    }

    /// <summary>
    /// Hooks the group's colour up to, or off, the interior or the outline of the
    /// selected blocks in it. They all follow the first one, so a mixed selection
    /// ends up matching.
    /// </summary>
    private static void ToggleColorLink(Entity editor, int group, bool outline)
    {
        uint bit = ItemGroups.Bit(group);
        bool? linked = null;
        foreach (Entity item in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (!IsBlockIn(item, group))
                continue;

            ref Block block = ref item.GetComponent<Block>();
            ref uint links = ref outline ? ref block.OutlineGroups : ref block.FillGroups;
            linked ??= (links & bit) == 0;
            links = linked.Value ? links | bit : links & ~bit;
        }
    }

    /// <summary>The label and tint of a colour link button, from the first selected block in the group.</summary>
    private void ShowColorLink(Entity editor, int group, bool outline, CommandBuffer buffer)
    {
        Entity button = Piece(outline ? EditorGroupPart.OutlineLink : EditorGroupPart.InteriorLink);
        string name = outline ? "OUTLINE" : "INTERIOR";
        foreach (Entity item in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (!IsBlockIn(item, group))
                continue;

            Block block = item.GetComponent<Block>();
            bool on = ((outline ? block.OutlineGroups : block.FillGroups) & ItemGroups.Bit(group)) != 0;
            UiText.Set(button, $"{name} {(on ? "ON" : "OFF")}", buffer);
            Tint(button, on ? ToggleOn : ActionIdle, buffer);
            return;
        }

        UiText.Set(button, name, buffer);
        Tint(button, Unavailable, buffer);
    }

    private static bool IsBlockIn(Entity item, int group) =>
        !item.IsNull &&
        item.HasComponent<Block>() &&
        item.TryGetComponent(out ItemGroups member) &&
        member.Has(group);

    /// <summary>Applies a palette swatch or alpha step to the selected colour effect.</summary>
    private void HandlePalette(ref EditorGroupState state, GroupDefinition group)
    {
        if (state.PaletteForBuffer)
        {
            HandleBufferPalette(ref state, group);
            return;
        }

        if (JustPressed(Piece(EditorGroupPart.PaletteClose)) ||
            !TrySelected(state, group, out int row, out GroupEffect effect) ||
            effect.Kind != GroupEffectKind.Color)
        {
            state.PaletteOpen = false;
            return;
        }

        for (int i = 0; i < EditorGroupPalette.Swatches.Length; i++)
        {
            if (!JustPressed(Piece(EditorGroupPart.PaletteSwatch, i)))
                continue;

            Color swatch = EditorGroupPalette.WithAlpha(
                EditorGroupPalette.Swatches[i],
                effect.Tint.A);
            Edit(() => group.Effects[row] = effect with { Tint = swatch });
            return;
        }

        for (int i = 0; i < EditorGroupPalette.Alphas.Length; i++)
        {
            if (!JustPressed(Piece(EditorGroupPart.PaletteAlpha, i)))
                continue;

            Color tinted = EditorGroupPalette.WithAlpha(
                effect.Tint,
                EditorGroupPalette.Alphas[i]);
            Edit(() => group.Effects[row] = effect with { Tint = tinted });
            return;
        }
    }

    /// <summary>The palette while it sets the buffer's tint: a swatch is the colour; alpha has its own − / +.</summary>
    private void HandleBufferPalette(ref EditorGroupState state, GroupDefinition group)
    {
        if (JustPressed(Piece(EditorGroupPart.PaletteClose)) || !state.BufferTrack || !group.Buffered)
        {
            state.PaletteOpen = false;
            state.PaletteForBuffer = false;
            return;
        }

        for (int i = 0; i < EditorGroupPalette.Swatches.Length; i++)
        {
            if (!JustPressed(Piece(EditorGroupPart.PaletteSwatch, i)))
                continue;

            Color swatch = EditorGroupPalette.WithAlpha(EditorGroupPalette.Swatches[i], 1f);
            Edit(() => group.BufferTint = swatch);
            state.PaletteOpen = false;
            state.PaletteForBuffer = false;
            return;
        }
    }

    private static bool TrySelected(
        EditorGroupState state,
        GroupDefinition group,
        out int row,
        out GroupEffect effect)
    {
        row = state.SelectedEffect;
        effect = default;
        if (state.SelectedGroup != state.Open ||
            row < 0 ||
            row >= group.Effects.Count)
        {
            return false;
        }

        effect = group.Effects[row];
        return true;
    }

    private static int FirstMove(GroupDefinition group)
    {
        for (int i = 0; i < group.Effects.Count; i++)
        {
            if (group.Effects[i].Kind == GroupEffectKind.Move)
                return i;
        }

        return -1;
    }

    private static void Select(int group, int row, ref EditorGroupState state)
    {
        state.SelectedGroup = group;
        state.SelectedEffect = row;
    }

    /// <summary>Moves an effect one grid step in beat space, so it stays on the beat across tempo changes.</summary>
    private void Nudge(GroupDefinition group, int row, GroupEffect effect, Playhead playhead, int lines)
    {
        float start = EditorGroupLayout.Tidy(EditorGroupLayout.StepSeconds(effect.Start, playhead, lines));
        float end = Math.Max(start, EditorGroupLayout.Tidy(EditorGroupLayout.StepSeconds(effect.End, playhead, lines)));
        Edit(() => group.Effects[row] = effect with { Start = start, End = end });
    }


}
