using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Editor;

/// <summary>Committing the overlay's typed fields and moving its ECS pieces.</summary>
public sealed partial class EditorGroupSystem
{
    // ----------------------------------------------------------------- committing

    /// <summary>Applies a field when it loses focus, the way the other editor panels do.</summary>
    private void CommitFields(GroupDefinition group, EditorGroupState state, Playhead playhead)
    {
        Entity name = Piece(EditorGroupPart.NameField);
        CommitIfBlurred(name, text => Edit(() => group.Name = text.Length > 24 ? text[..24] : text));

        for (int row = 0; row < group.Effects.Count && row < EditorGroupLayout.MaxRows; row++)
        {
            int index = row;
            CommitIfBlurred(Piece(EditorGroupPart.RowStart, row), text =>
            {
                if (!TryNumber(text, out float value))
                    return;
                GroupEffect e = group.Effects[index];
                float start = Tidy(state, value, playhead);
                Edit(() => group.Effects[index] = e with { Start = start, End = Math.Max(e.End, start) });
            });
            CommitIfBlurred(Piece(EditorGroupPart.RowEnd, row), text =>
            {
                if (!TryNumber(text, out float value))
                    return;
                GroupEffect e = group.Effects[index];
                float end = Math.Max(e.Start, Tidy(state, value, playhead));
                Edit(() => group.Effects[index] = e with { End = end });
            });
            CommitIfBlurred(Piece(EditorGroupPart.RowA, row), text =>
            {
                GroupEffect e = group.Effects[index];
                if (e.Kind == GroupEffectKind.Move)
                {
                    if (TryNumber(text, out float x))
                        Edit(() => group.Effects[index] = e with { Offset = new Vector2(x, e.Offset.Y) });
                }
                else if (TryColor(text, out Color tint))
                {
                    Edit(() => group.Effects[index] = e with { Tint = tint });
                }
            });
            CommitIfBlurred(Piece(EditorGroupPart.RowB, row), text =>
            {
                GroupEffect e = group.Effects[index];
                if (e.Kind == GroupEffectKind.Move && TryNumber(text, out float y))
                    Edit(() => group.Effects[index] = e with { Offset = new Vector2(e.Offset.X, y) });
            });
        }
    }

    private void CommitIfBlurred(Entity field, Action<string> commit)
    {
        if (field.IsNull || !field.HasComponent<TextField>())
            return;

        bool focused = field.Tags.Has<TextFieldFocused>();
        bool was = _focused.Contains(field.Id);
        if (focused)
            _focused.Add(field.Id);
        else
            _focused.Remove(field.Id);

        if (was && !focused)
            commit(field.GetComponent<TextField>().Value);
    }

    private static float Tidy(EditorGroupState state, float seconds, Playhead playhead) =>
        EditorGroupLayout.Tidy(state.Snap ? EditorGroupLayout.SnapSeconds(seconds, playhead) : seconds);

    private static float SnapIf(EditorGroupState state, float seconds, Playhead playhead) =>
        state.Snap ? EditorGroupLayout.SnapSeconds(seconds, playhead) : EditorGroupLayout.Tidy(seconds);

    public static bool TryNumber(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    public static bool TryColor(string text, out Color color)
    {
        string code = text.Trim();
        if (code.Length > 0 && Color.HtmlIsValid(code))
        {
            color = new Color(code);
            return true;
        }

        color = default;
        return false;
    }

    // ----------------------------------------------------------------- entity helpers

    private static void Place(Entity entity, Rect2 rect, Vector2 origin, CommandBuffer buffer)
    {
        if (entity.IsNull || !entity.HasComponent<Position>())
            return;

        Vector2 center = origin + rect.Position + rect.Size / 2;
        ref Position position = ref entity.GetComponent<Position>();
        if (position.Value != center)
        {
            position.Value = center;
            buffer.AddTag<Moved>(entity.Id);
        }

        Resize(entity, rect.Size, buffer);
    }

    private static void Hide(Entity entity, CommandBuffer buffer)
    {
        if (entity.IsNull || !entity.HasComponent<Position>())
            return;

        ref Position position = ref entity.GetComponent<Position>();
        if (position.Value == UiLayout.Hidden)
            return;

        position.Value = UiLayout.Hidden;
        buffer.AddTag<Moved>(entity.Id);
    }

    private static void Resize(Entity entity, Vector2 size, CommandBuffer buffer)
    {
        if (entity.IsNull || !entity.HasComponent<Rectangle>() || size.X <= 0 || size.Y <= 0)
            return;

        ref Rectangle rect = ref entity.GetComponent<Rectangle>();
        if (rect.Bounds.Size == size)
            return;

        rect.Bounds = new Rect2(-size / 2, size);
        buffer.AddTag<Dirty>(entity.Id);
    }

    private static void SetText(Entity entity, string value, CommandBuffer buffer)
    {
        if (entity.IsNull || !entity.HasComponent<Text>())
            return;

        ref Text text = ref entity.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;
        buffer.AddTag<Dirty>(entity.Id);
    }

    private static void SetFieldText(Entity field, string value, CommandBuffer buffer)
    {
        if (field.IsNull || !field.HasComponent<TextField>())
            return;

        ref TextField text = ref field.GetComponent<TextField>();
        if (text.Value == value)
            return;

        text.Value = value;
        buffer.AddTag<Dirty>(field.Id);
    }

    private static void SetNumber(Entity field, float value, CommandBuffer buffer)
    {
        if (field.IsNull || field.Tags.Has<TextFieldFocused>())
            return;

        SetFieldText(field, value.ToString("0.###", CultureInfo.InvariantCulture), buffer);
    }

    private static void SetTextColor(Entity entity, Color color, CommandBuffer buffer)
    {
        if (entity.IsNull || !entity.HasComponent<TextStyle>())
            return;

        ref TextStyle style = ref entity.GetComponent<TextStyle>();
        if (style.Color == color)
            return;

        style.Color = color;
        buffer.AddTag<Dirty>(entity.Id);
    }

    private static void SetNumericMode(Entity field, bool numeric)
    {
        if (!field.IsNull && field.HasComponent<TextField>())
            field.GetComponent<TextField>().Numeric = numeric;
    }

    private static void Tint(Entity entity, Color color, CommandBuffer buffer)
    {
        if (entity.IsNull)
            return;

        if (entity.HasComponent<ButtonVisual>())
        {
            ref ButtonVisual visual = ref entity.GetComponent<ButtonVisual>();
            if (visual.Normal != color)
            {
                visual.Normal = color;
                visual.Hover = color.Lerp(Colors.White, 0.16f);
                visual.Flash = Colors.White;
                buffer.AddTag<Dirty>(entity.Id);
            }

            return;
        }

        if (entity.HasComponent<FillColor>())
        {
            ref FillColor fill = ref entity.GetComponent<FillColor>();
            if (fill.Value != color)
            {
                fill.Value = color;
                buffer.AddTag<Dirty>(entity.Id);
            }
        }
    }
}
