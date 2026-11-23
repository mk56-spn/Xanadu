using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// Generic dropdown state attached to its button entity.
/// The list is an indexed link so deleting it cleans up the dropdown.
/// </summary>
public struct Dropdown(
    Entity list,
    int selected,
    Vector2? listSize = null,
    bool closeOthers = true) : ILinkComponent
{
    public Entity List = list;
    public int Selected = selected;
    public bool Open;
    public Vector2 ListSize = listSize ?? Vector2.Zero;

    /// <summary>When false, opening this dropdown leaves other lists open.</summary>
    public bool CloseOthers = closeOthers;

    public readonly Entity GetIndexedValue() => List;
}

/// <summary>
/// Signal emitted on a dropdown's button when the user picks an option (not
/// when code sets <see cref="Dropdown.Selected"/>). Not a component: listen
/// with <see cref="UiDropdowns.OnPicked"/>.
/// </summary>
public readonly struct DropdownPicked(int value)
{
    public readonly int Value = value;
}

/// <summary>Listening to dropdown picks through the entity's native signals.</summary>
public static class UiDropdowns
{
    /// <summary>
    /// Calls <paramref name="picked"/> with the option's value each time the user
    /// picks one on <paramref name="button"/> (Friflo's <c>AddSignalHandler</c>
    /// for <see cref="DropdownPicked"/>; the handler goes with the entity).
    /// </summary>
    public static Entity OnPicked(this Entity button, System.Action<int> picked)
    {
        button.AddSignalHandler<DropdownPicked>(signal => picked(signal.Event.Value));
        return button;
    }
}

/// <summary>
/// A dropdown whose button reads its pick (<see cref="UiNode.Choice"/>): the
/// dropdown system keeps the button's text on <c>Names[Selected]</c> and a
/// down arrow, whether the pick came from the list or from code.
/// </summary>
public struct DropdownShowsPick(IReadOnlyList<string> names) : IComponent
{
    public IReadOnlyList<string> Names = names;

    /// <summary>What the button reads for pick <paramref name="selected"/>.</summary>
    public readonly string Text(int selected) =>
        Names.Count == 0 ? "▼" : $"{Names[System.Math.Clamp(selected, 0, Names.Count - 1)]} ▼";
}

/// <summary>Marks the backing panel of a dropdown's list, so it draws above ordinary content.</summary>
public struct DropdownListPanel : ITag;

/// <summary>
/// One selectable option inside a dropdown, linked to its owning button.
/// </summary>
public struct DropdownOption(Entity owner, int value) : ILinkComponent
{
    public Entity Owner = owner;
    public int Value = value;

    public readonly Entity GetIndexedValue() => Owner;
}
