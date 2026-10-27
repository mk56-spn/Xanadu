using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Editor.Commands;
using Writer.Levels;

namespace Writer.Systems.Editor;

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

        if (JustPressed(Piece(EditorGroupPart.AddColor)) && group.Effects.Count < EditorGroupLayout.MaxRows)
        {
            Edit(() => group.Effects.Add(GroupEffect.Colour(now, now + 0.5f, DefaultTint)));
            Select(state.Open, group.Effects.Count - 1, ref state);
        }

        int open = state.Open;
        if (JustPressed(Piece(EditorGroupPart.AddSelected)))
            Edit(() => SetMembership(editor, open, add: true));
        if (JustPressed(Piece(EditorGroupPart.RemoveSelected)))
            Edit(() => SetMembership(editor, open, add: false));
        if (JustPressed(Piece(EditorGroupPart.SelectMembers)))
            SelectMembers(editor, open);

        if (state.PaletteOpen)
            HandlePalette(ref state, group);

        bool selected = TrySelected(state, group, out int row, out GroupEffect effect);
        float step = SnapStep(playhead);

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
            Nudge(group, row, effect, -step);
        if (JustPressed(Piece(EditorGroupPart.SelectionNudgeRight)) && selected)
            Nudge(group, row, effect, step);

        if (JustPressed(Piece(EditorGroupPart.SelectionPick)))
            state.PaletteOpen = selected && effect.Kind == GroupEffectKind.Color && !state.PaletteOpen;

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
                state.Preview = true;
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
            {
                Edit(() => group.Effects[index] = group.Effects[index] with
                {
                    Ease = GroupEaseCurves.Next(group.Effects[index].Ease),
                });
            }
        }
    }

    /// <summary>Applies a palette swatch or alpha step to the selected colour effect.</summary>
    private void HandlePalette(ref EditorGroupState state, GroupDefinition group)
    {
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

    private void Nudge(GroupDefinition group, int row, GroupEffect effect, float delta)
    {
        float start = Math.Max(0f, effect.Start + delta);
        float end = Math.Max(start, effect.End + delta);
        Edit(() => group.Effects[row] = effect with { Start = start, End = end });
    }

    private static float SnapStep(Playhead playhead) =>
        Math.Max(0.001f, (float)(TimingGrid.TickMs(playhead.Bpm, playhead.SnapDivision) / 1000.0));

}
