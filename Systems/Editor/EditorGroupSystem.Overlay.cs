using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Levels;

namespace Writer.Systems.Editor;

/// <summary>Placing the group overlay's pieces: rows, bars and the beat ruler.</summary>
public sealed partial class EditorGroupSystem
{
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
        Place(Piece(EditorGroupPart.Scrim), new Rect2(Vector2.Zero, screen), Vector2.Zero, buffer);
        foreach (EditorGroupPart part in Fixed)
        {
            Entity entity = Piece(part);
            Place(entity, EditorGroupLayout.Place(part, 0, dialog), origin, buffer);
        }

        string title = group.Name.Length > 0 ? $"GROUP {state.Open + 1}" : $"GROUP {state.Open + 1}";
        SetText(Piece(EditorGroupPart.Title), title, buffer);

        int members = CountMembers(state.Open);
        int mine = 0;
        foreach (Entity item in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (!item.IsNull && item.TryGetComponent(out ItemGroups mask) && mask.Has(state.Open))
                mine++;
        }

        SetText(Piece(EditorGroupPart.MemberText), mine > 0 ? $"{members} ITEMS  ({mine} SELECTED)" : $"{members} ITEMS", buffer);
        SetText(Piece(EditorGroupPart.SnapToggle), state.Snap ? "SNAP ON" : "SNAP OFF", buffer);
        Tint(Piece(EditorGroupPart.SnapToggle), state.Snap ? CellUsed : SceneColors.Muted, buffer);

        Entity name = Piece(EditorGroupPart.NameField);
        if (!name.IsNull && !name.Tags.Has<TextFieldFocused>())
            SetFieldText(name, group.Name, buffer);

        ShowRows(state, group, dialog, origin, buffer);
        ShowRuler(state, playhead, dialog, origin, buffer);
    }

    private static readonly EditorGroupPart[] Fixed =
    {
        EditorGroupPart.Dialog, EditorGroupPart.Title, EditorGroupPart.NameField, EditorGroupPart.Close,
        EditorGroupPart.MemberText, EditorGroupPart.AddSelected, EditorGroupPart.RemoveSelected,
        EditorGroupPart.SelectMembers, EditorGroupPart.SnapToggle, EditorGroupPart.AddMove,
        EditorGroupPart.AddColor, EditorGroupPart.ScrollLeft, EditorGroupPart.ScrollRight,
        EditorGroupPart.ZoomOut, EditorGroupPart.ZoomIn, EditorGroupPart.Hint,
    };

    private static readonly EditorGroupPart[] RowParts =
    {
        EditorGroupPart.RowIndex, EditorGroupPart.RowKind, EditorGroupPart.RowStart, EditorGroupPart.RowEnd,
        EditorGroupPart.RowA, EditorGroupPart.RowB, EditorGroupPart.RowEase, EditorGroupPart.RowDelete,
        EditorGroupPart.RowTrack, EditorGroupPart.RowBar,
    };

    private void ShowRows(
        EditorGroupState state,
        GroupDefinition group,
        Vector2 dialog,
        Vector2 origin,
        CommandBuffer buffer)
    {
        Rect2 timeline = Offset(EditorGroupLayout.Timeline(dialog), origin);
        for (int row = 0; row < EditorGroupLayout.MaxRows; row++)
        {
            if (row >= group.Effects.Count)
            {
                foreach (EditorGroupPart part in RowParts)
                    Hide(Piece(part, row), buffer);
                // An empty row still shows its track so the timeline reads as a grid.
                Place(Piece(EditorGroupPart.RowTrack, row), EditorGroupLayout.Place(EditorGroupPart.RowTrack, row, dialog), origin, buffer);
                continue;
            }

            GroupEffect effect = group.Effects[row];
            bool isMove = effect.Kind == GroupEffectKind.Move;
            foreach (EditorGroupPart part in RowParts)
            {
                if (part == EditorGroupPart.RowBar)
                    continue;
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

            SetText(Piece(EditorGroupPart.RowKind, row), isMove ? "MOVE" : "COLOR", buffer);
            Tint(Piece(EditorGroupPart.RowKind, row), isMove ? MoveColor.Darkened(0.55f) : OpaqueTint(effect).Darkened(0.45f), buffer);
            Entity ease = Piece(EditorGroupPart.RowEase, row);
            SetText(ease, EaseLabel(effect.Ease), buffer);
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

            Rect2 bar = BarRect(effect, row, state, timeline);
            ShowBar(Piece(EditorGroupPart.RowBar, row), bar, timeline, isMove ? MoveColor : OpaqueTint(effect), buffer);
        }
    }

    private static Color OpaqueTint(GroupEffect effect) => new(effect.Tint.R, effect.Tint.G, effect.Tint.B);

    private void ShowBar(Entity bar, Rect2 rect, Rect2 timeline, Color color, CommandBuffer buffer)
    {
        if (bar.IsNull)
            return;

        // A bar off the visible window is hidden rather than drawn over the table.
        if (rect.End.X < timeline.Position.X || rect.Position.X > timeline.End.X)
        {
            Hide(bar, buffer);
            return;
        }

        float left = Math.Max(rect.Position.X, timeline.Position.X);
        float right = Math.Min(rect.End.X, timeline.End.X);
        var clipped = new Rect2(left, rect.Position.Y, Math.Max(2f, right - left), rect.Size.Y);
        Place(bar, new Rect2(Vector2.Zero, clipped.Size), clipped.Position, buffer);
        Resize(bar, clipped.Size, buffer);
        Tint(bar, color, buffer);
    }

    /// <summary>Beat-grid tick lines and second labels across the ruler.</summary>
    private void ShowRuler(
        EditorGroupState state,
        Playhead playhead,
        Vector2 dialog,
        Vector2 origin,
        CommandBuffer buffer)
    {
        Rect2 timeline = Offset(EditorGroupLayout.Timeline(dialog), origin);
        Rect2 ruler = Offset(EditorGroupLayout.Ruler(dialog), origin);
        double baseTick = TimingGrid.TickMs(playhead.Bpm, playhead.SnapDivision) / 1000.0;
        double step = EditorGroupLayout.TickSeconds(playhead, state.ViewSpan, timeline.Size.X);
        int skip = Math.Max(1, (int)Math.Round(step / baseTick));
        double startMs = state.ViewStart * 1000.0;
        double endMs = (state.ViewStart + state.ViewSpan) * 1000.0;
        int first = TimingGrid.FirstTick(
            (long)startMs, playhead.OffsetMs, baseTick * 1000.0);
        int last = TimingGrid.LastTick((long)endMs, playhead.OffsetMs, baseTick * 1000.0);
        first -= ((first % skip) + skip) % skip;

        int slot = 0;
        for (int tick = first; tick <= last && slot < EditorGroupLayout.TickSlots; tick += skip)
        {
            double seconds = TimingGrid.TickTime(tick, playhead.OffsetMs, baseTick * 1000.0) / 1000.0;
            if (seconds < state.ViewStart - 0.0001 || seconds > state.ViewStart + state.ViewSpan + 0.0001)
                continue;

            float x = EditorGroupLayout.XForTime(timeline, state.ViewStart, state.ViewSpan, (float)seconds);
            bool beat = tick % Math.Max(1, playhead.SnapDivision) == 0;
            Entity line = Piece(EditorGroupPart.TickLine, slot);
            Place(line, new Rect2(0, 0, 1, EditorGroupLayout.MaxRows * EditorGroupLayout.RowHeight), new Vector2(x, timeline.Position.Y), buffer);
            Tint(line, beat ? new Color("#475569") : new Color("#1e293b"), buffer);
            slot++;
        }

        for (; slot < EditorGroupLayout.TickSlots; slot++)
            Hide(Piece(EditorGroupPart.TickLine, slot), buffer);

        // Second labels at round numbers.
        double labelStep = LabelStep(state.ViewSpan);
        int label = 0;
        double firstLabel = Math.Ceiling(state.ViewStart / labelStep) * labelStep;
        for (double t = firstLabel; t <= state.ViewStart + state.ViewSpan && label < EditorGroupLayout.LabelSlots; t += labelStep)
        {
            float x = EditorGroupLayout.XForTime(timeline, state.ViewStart, state.ViewSpan, (float)t);
            Entity text = Piece(EditorGroupPart.TickLabel, label);
            Place(text, new Rect2(0, 0, 70, 16), new Vector2(x - 35, ruler.Position.Y + 8), buffer);
            SetText(text, t.ToString("0.##", CultureInfo.InvariantCulture) + "s", buffer);
            label++;
        }

        for (; label < EditorGroupLayout.LabelSlots; label++)
            Hide(Piece(EditorGroupPart.TickLabel, label), buffer);
    }

    /// <summary>A round spacing for second labels that gives no more than the label slots.</summary>
    public static double LabelStep(float span)
    {
        foreach (double step in new[] { 0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60, 120 })
        {
            if (span / step <= EditorGroupLayout.LabelSlots - 1)
                return step;
        }

        return 300;
    }

    private void HideOverlay(CommandBuffer buffer)
    {
        foreach (Entity entity in _pieces.Values)
            Hide(entity, buffer);
    }
}
