using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>One entry of a right-click menu: what it reads, and whether it asks once more before it counts.</summary>
/// <param name="Confirm">A press turns the menu into its question with CONFIRM and CANCEL; only CONFIRM picks it.</param>
/// <param name="Question">What it asks, such as "DELETE Lab/glow?"; "<paramref name="Label"/>?" without one.</param>
public sealed record MenuItem(string Label, bool Confirm = false, string? Question = null);

/// <summary>
/// What a right-click on this node (or anything under it that takes input) offers, in order
/// (<see cref="UiNode.Menu"/>). The menu opens at the pointer; picking an entry emits
/// <see cref="MenuPicked"/> on this node.
/// </summary>
public struct MenuItems(IReadOnlyList<MenuItem> items) : IComponent
{
    public IReadOnlyList<MenuItem> Items = items;
}

/// <summary>Signal emitted on a node when the user picks entry <see cref="Item"/> of its menu. Listen with <see cref="ContextMenus.OnMenuPicked"/>.</summary>
public readonly struct MenuPicked(int item)
{
    public readonly int Item = item;
}

/// <summary>Listening to right-click menus through the node's native signals.</summary>
public static class ContextMenus
{
    /// <summary>Calls <paramref name="picked"/> with the entry's place each time the user picks one of the node's menu.</summary>
    public static Entity OnMenuPicked(this Entity node, Action<int> picked)
    {
        node.AddSignalHandler<MenuPicked>(signal => picked(signal.Event.Item));
        return node;
    }
}

public readonly partial struct UiNode
{
    /// <summary>Gives the node a right-click menu of <paramref name="items"/> (<see cref="MenuItems"/>); listen with <see cref="ContextMenus.OnMenuPicked"/>.</summary>
    public UiNode Menu(params MenuItem[] items) => With(new MenuItems(items));
}
