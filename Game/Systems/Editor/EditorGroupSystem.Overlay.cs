using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>Placing the group overlay's pieces: rows, bars and the beat ruler.</summary>
public sealed partial class EditorGroupSystem
{
    // The timeline itself (its beat lines, labels, effect bands, pins and the
    // playhead) is drawn by EditorGroupOverlayDrawSystem with the shared painter.

    // ------------------------------------------------------------------ showing

    private void ShowOverlay(
        Entity editor,
        EditorGroupState state,
        GroupDefinition group,
        Playhead playhead,
        Vector2 dialog,
        Vector2 origin,
        CommandBuffer buffer)
    {
        Vector2 screen = editor.GetComponent<PlayfieldView>().ScreenSize;
        // The scrim keeps presses off the field while the panel is open, but not
        // off the sidebar: the groups grid and the layer inspector stay usable,
        // so the selection can join groups and change layer mid-edit.
        Place(
            Piece(EditorGroupPart.Scrim),
            new Rect2(Vector2.Zero, new Vector2(screen.X - EditorShellMetrics.RightPanelWidth, screen.Y)),
            Vector2.Zero,
            buffer);
        foreach (EditorGroupPart part in Fixed)
        {
            Entity entity = Piece(part);
            Place(entity, EditorGroupLayout.Place(part, 0, dialog), origin, buffer);
        }

        string title = group.Name.Length > 0 ? $"GROUP {state.Open + 1}" : $"GROUP {state.Open + 1}";
        UiText.Set(Piece(EditorGroupPart.Title), title, buffer);

        int members = CountMembers(state.Open);
        int mine = 0;
        foreach (Entity item in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (!item.IsNull && item.TryGetComponent(out ItemGroups mask) && mask.Has(state.Open))
                mine++;
        }

        UiText.Set(Piece(EditorGroupPart.MemberText), mine > 0 ? $"{members} ITEMS · {mine} SEL" : $"{members} ITEMS", buffer);
        ShowColorLink(editor, state.Open, outline: false, buffer);
        ShowColorLink(editor, state.Open, outline: true, buffer);
        UiText.Set(
            Piece(EditorGroupPart.BufferToggle),
            group.Buffered ? "BUF ON" : "BUF OFF",
            buffer);
        Tint(Piece(EditorGroupPart.BufferToggle), group.Buffered ? ToggleOn : ActionIdle, buffer);
        UiText.Set(Piece(EditorGroupPart.SnapToggle), state.Snap ? "SNAP ON" : "SNAP OFF", buffer);
        Tint(Piece(EditorGroupPart.SnapToggle), state.Snap ? ToggleOn : Unavailable, buffer);
        UiText.Set(Piece(EditorGroupPart.PlayToggle), playhead.Playing ? "PAUSE" : "PLAY", buffer);
        Tint(Piece(EditorGroupPart.PlayToggle), playhead.Playing ? ToggleOn : ActionIdle, buffer);
        UiLayout.SetDisabled(Piece(EditorGroupPart.ShaderSlot), buffer, state.PaletteOpen);
        ShowShaderTrack(editor, state, group, playhead, buffer);
        ShowBufferSettings(state, group, dialog, origin, buffer);

        Entity name = Piece(EditorGroupPart.NameField);
        if (!name.IsNull && !name.Tags.Has<TextFieldFocused>())
            SetFieldText(name, group.Name, buffer);

        ShowRows(state, group, dialog, origin, buffer);
        ShowKeyLane(editor, state, group, dialog, origin, buffer);
        ShowSelection(state, group, buffer);
        ShowCycle(state, group, dialog, origin, buffer);
        ShowPalette(state, group, dialog, origin, buffer);
    }

    private static readonly EditorGroupPart[] Fixed =
    {
        EditorGroupPart.Dialog, EditorGroupPart.Title, EditorGroupPart.NameField, EditorGroupPart.Close,
        EditorGroupPart.MemberText, EditorGroupPart.AddSelected, EditorGroupPart.RemoveSelected,
        EditorGroupPart.SelectMembers, EditorGroupPart.InteriorLink, EditorGroupPart.OutlineLink,
        EditorGroupPart.SnapToggle, EditorGroupPart.AddMove,
        EditorGroupPart.AddColor, EditorGroupPart.BufferToggle, EditorGroupPart.SoloToggle, EditorGroupPart.PlayToggle,
        EditorGroupPart.ScrollLeft, EditorGroupPart.ScrollRight, EditorGroupPart.ZoomOut,
        EditorGroupPart.ZoomIn, EditorGroupPart.Hint,
        EditorGroupPart.SelectionText, EditorGroupPart.SelectionTarget,
        EditorGroupPart.SelectionDuplicate, EditorGroupPart.SelectionMute,
        EditorGroupPart.SelectionNudgeLeft, EditorGroupPart.SelectionNudgeRight,
        EditorGroupPart.SelectionPick,
        EditorGroupPart.ShaderText, EditorGroupPart.ShaderSlot, EditorGroupPart.ShaderParam,
        EditorGroupPart.ShaderChannel, EditorGroupPart.ShaderDown, EditorGroupPart.ShaderValue,
        EditorGroupPart.ShaderUp, EditorGroupPart.ShaderClear,
        EditorGroupPart.TrackItem, EditorGroupPart.TrackBuffer,
    };

    /// <summary>The buffer's own settings: shown only on the shader row's BUFFER tab.</summary>
    private static readonly EditorGroupPart[] BufferSettings =
    {
        EditorGroupPart.BufferTint, EditorGroupPart.BufferAlphaText, EditorGroupPart.BufferAlphaDown,
        EditorGroupPart.BufferAlphaUp, EditorGroupPart.BufferBlend,
    };

    private static readonly EditorGroupPart[] RowParts =
    {
        EditorGroupPart.RowIndex, EditorGroupPart.RowKind, EditorGroupPart.RowStart, EditorGroupPart.RowEnd,
        EditorGroupPart.RowA, EditorGroupPart.RowB, EditorGroupPart.RowEase, EditorGroupPart.RowDelete,
    };

    private void ShowRows(
        EditorGroupState state,
        GroupDefinition group,
        Vector2 dialog,
        Vector2 origin,
        CommandBuffer buffer)
    {
        for (int row = 0; row < EditorGroupLayout.MaxRows; row++)
        {
            if (state.PaletteOpen)
            {
                foreach (EditorGroupPart part in RowParts)
                    Hide(Piece(part, row), buffer);
                continue;
            }

            if (row >= group.Effects.Count)
            {
                foreach (EditorGroupPart part in RowParts)
                    Hide(Piece(part, row), buffer);
                continue;
            }

            GroupEffect effect = group.Effects[row];
            bool isMove = effect.Kind == GroupEffectKind.Move;
            foreach (EditorGroupPart part in RowParts)
            {
                if (part == EditorGroupPart.RowB && !isMove)
                {
                    Hide(Piece(part, row), buffer);
                    continue;
                }

                Rect2 rect = EditorGroupLayout.Place(part, row, dialog);
                if (part == EditorGroupPart.RowA && !isMove)
                    rect = new Rect2(rect.Position, new Vector2(EditorGroupLayout.Place(EditorGroupPart.RowB, row, dialog).End.X - rect.Position.X, rect.Size.Y));
                Place(Piece(part, row), rect, origin, buffer);
            }

            UiText.Set(Piece(EditorGroupPart.RowIndex, row), (row + 1) + (effect.Muted ? "*" : ""), buffer);
            UiText.Set(Piece(EditorGroupPart.RowKind, row), isMove ? "MOVE" : "COLOR", buffer);
            Tint(Piece(EditorGroupPart.RowKind, row), isMove ? MoveColor.Darkened(0.55f) : OpaqueTint(effect).Darkened(0.45f), buffer);
            Entity ease = Piece(EditorGroupPart.RowEase, row);
            UiText.Set(ease, EaseLabel(effect.Ease), buffer);
            Tint(ease, isMove ? MoveColor.Darkened(0.7f) : OpaqueTint(effect).Darkened(0.65f), buffer);

            SetNumber(Piece(EditorGroupPart.RowStart, row), effect.Start, buffer);
            SetNumber(Piece(EditorGroupPart.RowEnd, row), effect.End, buffer);

            Entity a = Piece(EditorGroupPart.RowA, row);
            Entity b = Piece(EditorGroupPart.RowB, row);
            SetNumericMode(a, isMove);
            if (isMove)
            {
                SetNumber(a, effect.Offset.X, buffer);
                SetNumber(b, effect.Offset.Y, buffer);
            }
            else if (!a.IsNull && !a.Tags.Has<TextFieldFocused>())
            {
                SetFieldText(a, "#" + effect.Tint.ToHtml(), buffer);
            }

        }
    }

    private static Color OpaqueTint(GroupEffect effect) => new(effect.Tint.R, effect.Tint.G, effect.Tint.B);

    /// <summary>The effect toolbar's tools that act on the selected effect.</summary>
    private void ShowSelection(
        EditorGroupState state,
        GroupDefinition group,
        CommandBuffer buffer)
    {
        bool selected = TrySelected(state, group, out int row, out GroupEffect effect);
        bool move = selected && effect.Kind == GroupEffectKind.Move;
        bool colour = selected && effect.Kind == GroupEffectKind.Color;

        Entity label = Piece(EditorGroupPart.SelectionText);
        if (selected)
        {
            string kind = move ? "MOVE" : "COLOR";
            string text = $"{kind} {row + 1}  {effect.Start:0.##}–{effect.End:0.##}";
            if (effect.Cycling)
                text += $"  ×{effect.Cycles}";
            if (effect.Muted)
                text += "  MUTED";
            UiText.Set(label, text, buffer);
            UiText.SetColor(label, Palette.Accent, buffer);
        }
        else
        {
            UiText.Set(label, "NO EFFECT SELECTED", buffer);
            UiText.SetColor(label, Palette.TextDim, buffer);
        }

        UiText.Set(Piece(EditorGroupPart.SelectionTarget), "TARGET", buffer);
        Tint(Piece(EditorGroupPart.SelectionTarget), move ? ActionIdle : Unavailable, buffer);
        UiText.Set(Piece(EditorGroupPart.SelectionDuplicate), "DUP", buffer);
        Tint(
            Piece(EditorGroupPart.SelectionDuplicate),
            selected && group.Effects.Count < EditorGroupLayout.MaxRows ? ActionIdle : Unavailable,
            buffer);
        UiText.Set(Piece(EditorGroupPart.SelectionMute), effect.Muted ? "UNMUTE" : "MUTE", buffer);
        Tint(
            Piece(EditorGroupPart.SelectionMute),
            selected ? (effect.Muted ? Palette.DangerDeep : ActionIdle) : Unavailable,
            buffer);
        Tint(Piece(EditorGroupPart.SelectionNudgeLeft), selected ? ActionIdle : Unavailable, buffer);
        Tint(Piece(EditorGroupPart.SelectionNudgeRight), selected ? ActionIdle : Unavailable, buffer);
        UiText.Set(Piece(EditorGroupPart.SelectionPick), "PICK", buffer);
        Tint(
            Piece(EditorGroupPart.SelectionPick),
            colour ? OpaqueTint(effect).Darkened(0.35f) : Unavailable,
            buffer);

        UiLayout.SetDisabled(Piece(EditorGroupPart.SelectionTarget), buffer, !move);
        UiLayout.SetDisabled(
            Piece(EditorGroupPart.SelectionDuplicate),
            buffer,
            !selected || group.Effects.Count >= EditorGroupLayout.MaxRows);
        UiLayout.SetDisabled(Piece(EditorGroupPart.SelectionMute), buffer, !selected);
        UiLayout.SetDisabled(Piece(EditorGroupPart.SelectionNudgeLeft), buffer, !selected);
        UiLayout.SetDisabled(Piece(EditorGroupPart.SelectionNudgeRight), buffer, !selected);
        UiLayout.SetDisabled(Piece(EditorGroupPart.SelectionPick), buffer, !colour);

        UiText.Set(
            Piece(EditorGroupPart.SoloToggle),
            state.SoloGroup == state.Open ? "SOLO ON" : "SOLO",
            buffer);
        Tint(
            Piece(EditorGroupPart.SoloToggle),
            state.SoloGroup == state.Open ? ToggleOn : ActionIdle,
            buffer);
    }

    /// <summary>The colour palette, shown over the dialog while PICK is on.</summary>
    private void ShowPalette(
        EditorGroupState state,
        GroupDefinition group,
        Vector2 dialog,
        Vector2 origin,
        CommandBuffer buffer)
    {
        GroupEffect effect = default;
        bool forBuffer = state.PaletteOpen && state.PaletteForBuffer;
        bool show = forBuffer ||
                    (state.PaletteOpen &&
                     TrySelected(state, group, out _, out effect) &&
                     effect.Kind == GroupEffectKind.Color);
        if (!show)
        {
            Hide(Piece(EditorGroupPart.PalettePanel), buffer);
            Hide(Piece(EditorGroupPart.PaletteTitle), buffer);
            Hide(Piece(EditorGroupPart.PaletteClose), buffer);
            for (int i = 0; i < EditorGroupPalette.Swatches.Length; i++)
                Hide(Piece(EditorGroupPart.PaletteSwatch, i), buffer);
            for (int i = 0; i < EditorGroupPalette.Alphas.Length; i++)
                Hide(Piece(EditorGroupPart.PaletteAlpha, i), buffer);
            return;
        }

        Place(Piece(EditorGroupPart.PalettePanel), EditorGroupLayout.Place(EditorGroupPart.PalettePanel, 0, dialog), origin, buffer);
        Place(Piece(EditorGroupPart.PaletteTitle), EditorGroupLayout.Place(EditorGroupPart.PaletteTitle, 0, dialog), origin, buffer);
        UiText.Set(Piece(EditorGroupPart.PaletteTitle), forBuffer ? "BUFFER TINT" : "COLOUR", buffer);
        Place(Piece(EditorGroupPart.PaletteClose), EditorGroupLayout.Place(EditorGroupPart.PaletteClose, 0, dialog), origin, buffer);
        for (int i = 0; i < EditorGroupPalette.Swatches.Length; i++)
            Place(Piece(EditorGroupPart.PaletteSwatch, i), EditorGroupLayout.Place(EditorGroupPart.PaletteSwatch, i, dialog), origin, buffer);
        for (int i = 0; i < EditorGroupPalette.Alphas.Length; i++)
        {
            Entity alpha = Piece(EditorGroupPart.PaletteAlpha, i);

            // The buffer's alpha is its own setting beside the tint.
            if (forBuffer)
            {
                Hide(alpha, buffer);
                continue;
            }

            Place(alpha, EditorGroupLayout.Place(EditorGroupPart.PaletteAlpha, i, dialog), origin, buffer);
            Tint(
                alpha,
                Mathf.IsEqualApprox(effect.Tint.A, EditorGroupPalette.Alphas[i])
                    ? ToggleOn
                    : ActionIdle,
                buffer);
        }
    }

    private void HideOverlay(CommandBuffer buffer)
    {
        foreach (Entity entity in _pieces.Values)
            Hide(entity, buffer);

        // A disabled dropdown closes, so a list can't float on after the panel shuts.
        UiLayout.SetDisabled(Piece(EditorGroupPart.ShaderSlot), buffer, true);
        UiLayout.SetDisabled(Piece(EditorGroupPart.BufferBlend), buffer, true);
        UiLayout.SetDisabled(Piece(EditorGroupPart.CycleMode), buffer, true);
    }
}
