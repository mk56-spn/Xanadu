using System.Runtime.CompilerServices;
using System;
using Friflo.Engine.ECS;
using Xanadu.Theme;

namespace Xanadu.Ecs;

/// <summary>Clickable boolean toggle with configurable on/off labels and colors.</summary>
public struct Toggle : IComponent
{
    public string OnText;
    public string OffText;
    public bool Value;
    public bool Held;
    public Color OnColor;
    public Color OffColor;

    public Toggle(
        string onText,
        string offText,
        bool value,
        Color? onColor = null,
        Color? offColor = null)
    {
        OnText = onText;
        OffText = offText;
        Value = value;
        Held = false;
        OnColor = onColor ?? Palette.Primary;
        OffColor = offColor ?? Palette.Muted;
    }
}

/// <summary>Listening to a toggle through the store's native component events.</summary>
public static class UiToggles
{
    /// <summary>
    /// Calls <paramref name="changed"/> with the new value each time a press
    /// flips <paramref name="toggle"/>. It is the entity's native
    /// <c>OnComponentChanged</c>, narrowed to updates of <see cref="Toggle"/>;
    /// the handler is dropped with the entity.
    /// </summary>
    public static Entity OnToggled(this Entity toggle, Action<bool> changed)
    {
        toggle.OnComponentChanged += change =>
        {
            if (change.Action == ComponentChangedAction.Update && change.Type == typeof(Toggle))
                changed(change.Component<Toggle>().Value);
        };
        return toggle;
    }

    /// <summary>
    /// Names <paramref name="toggle"/> as the store's one switch called
    /// <paramref name="id"/> (Friflo's native <see cref="UniqueEntity"/>), so
    /// anything can read it with <see cref="On"/> without a marker or a query of its own.
    /// </summary>
    public static Entity Switch(this Entity toggle, string id)
    {
        toggle.AddComponent(new UniqueEntity(id));
        return toggle;
    }

    /// <summary>The switch called <paramref name="id"/>, hidden or not, or null when the store has none.</summary>
    public static Entity FindSwitch(EntityStore store, string id)
    {
        foreach (var (uniques, _, entities) in Switches.GetValue(store, static s => s.Query<UniqueEntity, Toggle>().WithDisabled()).Chunks)
        {
            for (int i = 0; i < uniques.Length; i++)
            {
                if (uniques.Span[i].uid == id)
                    return store.GetEntityById(entities.Ids[i]);
            }
        }

        return default;
    }

    /// <summary>The value of the switch called <paramref name="id"/>, or <paramref name="none"/> when there is none.</summary>
    public static bool On(EntityStore store, string id, bool none)
    {
        Entity toggle = FindSwitch(store, id);
        return toggle.IsNull ? none : toggle.GetComponent<Toggle>().Value;
    }

    /// <summary>Read every frame by the editors' tools, so each store keeps its query.</summary>
    private static readonly ConditionalWeakTable<EntityStore, ArchetypeQuery<UniqueEntity, Toggle>> Switches = new();
}
