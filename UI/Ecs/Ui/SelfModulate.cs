using System.Globalization;
using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// An object's own colour, as Godot's self modulate: what it is drawn in is
/// multiplied by this. It is the default look; a group's colour fade, or later
/// an animation key, is applied over it and overrides it. Editor items are built
/// with it, white, so that undo and level loads can set it in place from inside
/// a query, where adding a component is not allowed.
/// </summary>
public struct SelfModulate(Color color) : IComponent
{
    /// <summary>White as RGBA8, the packed form files and undo keep.</summary>
    public const uint White = 0xFFFFFFFF;

    public Color Color = color;

    public static Color Of(Entity entity) =>
        entity.TryGetComponent(out SelfModulate self) ? self.Color : Colors.White;

    public static Color For(Entity entity, Color basis) =>
        entity.TryGetComponent(out SelfModulate self) ? basis * self.Color : basis;

    public static uint Packed(Entity entity) => Of(entity).ToRgba32();

    public static void Put(Entity entity, uint rgba) => Put(entity, new Color(rgba));

    /// <summary>Sets the colour, in place when the entity already carries one.</summary>
    public static void Put(Entity entity, Color color)
    {
        if (entity.HasComponent<SelfModulate>())
            entity.GetComponent<SelfModulate>().Color = color;
        else if (color != Colors.White)
            entity.AddComponent(new SelfModulate(color));
    }

    /// <summary>Reads <c>#rrggbb</c> or <c>#rrggbbaa</c>, with or without the #; blank means white.</summary>
    public static bool TryParse(string typed, out Color color)
    {
        string hex = typed.Trim().TrimStart('#');
        color = Colors.White;
        if (hex.Length == 0)
            return true;
        if (hex.Length is not (6 or 8) ||
            !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        color = new Color("#" + hex);
        return true;
    }

    public static string Format(Color color) => "#" + color.ToHtml(color.A < 1f);
}

/// <summary>
/// A colour with a swatch, carried by its text field, typed or set with its picker.
/// It keeps showing <see cref="Value"/> until the field loses focus or the picker is
/// let go with a different colour; then <see cref="Committed"/> is set for the owning
/// tool to take, so a whole drag is one edit.
/// </summary>
public struct UiColorField(Entity swatch) : IComponent
{
    public Entity Swatch = swatch;
    public Color Value = Colors.White;
    public bool Committed;
    public bool WasFocused;

    /// <summary>The square of the picker that sets this field, if it has one.</summary>
    public Entity Picker;
    public bool WasDragged;

    /// <summary>The colour last handed out by <see cref="TakeLive"/> in this drag, while <see cref="Live"/> is set.</summary>
    public Color LiveColor;
    public bool Live;

    /// <summary>
    /// A colour was just dragged in from another swatch. It counts as an edit
    /// until the tool takes the commit, so the tool picks its targets first,
    /// as it does for typing or the picker; without that the drop had nothing
    /// to land on and the field snapped back the next frame.
    /// </summary>
    public bool Dropped;

    /// <summary>Whether a colour is being typed or dragged out, so what the tool has selected now may not be what it is for.</summary>
    public static bool IsEditing(Entity field)
    {
        Entity picker = field.GetComponent<UiColorField>().Picker;
        return field.Tags.Has<TextFieldFocused>() ||
               field.GetComponent<UiColorField>().Dropped ||
               (!picker.IsNull && picker.GetComponent<UiColorPicker>().Dragging);
    }

    /// <summary>Hands the tool a colour just typed or picked, once.</summary>
    public static bool TakeCommit(Entity field, out Color color)
    {
        ref UiColorField state = ref field.GetComponent<UiColorField>();
        color = state.Value;
        state.Dropped = false;
        if (!state.Committed)
            return false;

        state.Committed = false;
        return true;
    }

    /// <summary>
    /// Hands the tool the picker's colour while it is being dragged, each time
    /// it changes, so the tool can show it on its targets before the drag ends.
    /// The drag still ends in one <see cref="TakeCommit"/>, which is where the
    /// tool records the change; what it applies live it should not record.
    /// </summary>
    public static bool TakeLive(Entity field, out Color color)
    {
        ref UiColorField state = ref field.GetComponent<UiColorField>();
        color = default;
        if (state.Picker.IsNull || !state.Picker.GetComponent<UiColorPicker>().Dragging)
        {
            state.Live = false;
            return false;
        }

        Color picked = state.Picker.GetComponent<UiColorPicker>().Color;
        if (state.Live && state.LiveColor == picked)
            return false;

        state.Live = true;
        state.LiveColor = picked;
        color = picked;
        return true;
    }

    /// <summary>
    /// Shows the selection's colour, unless the user is typing, dragging the
    /// picker, or a typed or picked one is still in flight. During a drag the
    /// selection already wears the live colour, and taking it as the field's
    /// value would leave nothing to commit when the drag ends.
    /// </summary>
    public static void Show(Entity field, Color color)
    {
        ref UiColorField state = ref field.GetComponent<UiColorField>();
        bool dragging = !state.Picker.IsNull && state.Picker.GetComponent<UiColorPicker>().Dragging;
        if (!state.Committed && !state.WasFocused && !state.WasDragged && !dragging &&
            !field.Tags.Has<TextFieldFocused>())
        {
            state.Value = color;
        }
    }
}
