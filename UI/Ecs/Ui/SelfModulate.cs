using System.Globalization;
using Friflo.Engine.ECS;
using Godot;

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
/// A typed colour with a swatch, carried by its text field. The field keeps
/// showing <see cref="Value"/> until it loses focus with a different valid
/// colour; then <see cref="Committed"/> is set for the owning tool to take.
/// </summary>
public struct UiColorField(Entity swatch) : IComponent
{
    public Entity Swatch = swatch;
    public Color Value = Colors.White;
    public bool Committed;
    public bool WasFocused;

    /// <summary>Hands the tool a colour just typed, once.</summary>
    public static bool TakeCommit(Entity field, out Color color)
    {
        ref UiColorField state = ref field.GetComponent<UiColorField>();
        color = state.Value;
        if (!state.Committed)
            return false;

        state.Committed = false;
        return true;
    }

    /// <summary>Shows the selection's colour, unless the user is typing or a typed one is still in flight.</summary>
    public static void Show(Entity field, Color color)
    {
        ref UiColorField state = ref field.GetComponent<UiColorField>();
        if (!state.Committed && !state.WasFocused && !field.Tags.Has<TextFieldFocused>())
            state.Value = color;
    }
}
