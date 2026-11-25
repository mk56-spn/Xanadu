using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>One tab of a tabbed picker: its name and the entries listed under it, in order.</summary>
public sealed record PickerTab(string Name, IReadOnlyList<string> Entries);

/// <summary>
/// A picker for long lists in categories (<see cref="UiNode.TabbedPicker"/>): a
/// button that reads the pick, and under it a body that opens in place, pushing
/// what's below down, with a tab per category over a pooled list of that tab's
/// entries. Lives on the picker's column; the parts come from its tree
/// (<see cref="UiTabbedPickers"/>).
/// </summary>
public struct UiTabbedPicker(PickerTab[] tabs, string empty) : IComponent
{
    public PickerTab[] Tabs = tabs;

    /// <summary>The tab the list shows.</summary>
    public int Tab;

    /// <summary>The tab of the pick, or -1 for none.</summary>
    public int PickedTab = -1;

    /// <summary>The pick's place in its tab, or -1 for none.</summary>
    public int PickedEntry = -1;

    /// <summary>What the button reads with nothing picked.</summary>
    public string Empty = empty;

    /// <summary>What the button reads now.</summary>
    public readonly string Label =>
        PickedTab >= 0 && PickedTab < Tabs.Length && PickedEntry >= 0 && PickedEntry < Tabs[PickedTab].Entries.Count
            ? $"{Tabs[PickedTab].Entries[PickedEntry]} ▼"
            : $"{Empty} ▼";
}

/// <summary>
/// Signal emitted on a tabbed picker when the user picks an entry (not when code
/// sets the pick). Listen with <see cref="UiTabbedPickers.OnPicked"/>.
/// </summary>
public readonly struct PickerPicked(int tab, int entry)
{
    public readonly int Tab = tab;
    public readonly int Entry = entry;
}

/// <summary>A tabbed picker's parts, found through its tree, and driving it from its owner.</summary>
public static class UiTabbedPickers
{
    /// <summary>The button that opens and closes the body.</summary>
    public static Entity ButtonOf(Entity picker) => picker.ChildEntities[0];

    /// <summary>The body under the button: the tabs, then the list.</summary>
    public static Entity BodyOf(Entity picker) => picker.ChildEntities[1];

    /// <summary>The row of tab buttons.</summary>
    public static Entity TabsOf(Entity picker) => BodyOf(picker).ChildEntities[0];

    /// <summary>The pooled list of the shown tab's entries.</summary>
    public static Entity ListOf(Entity picker) => BodyOf(picker).ChildEntities[1];

    /// <summary>The button inside pooled row <paramref name="row"/>.</summary>
    public static Entity EntryOf(Entity picker, int row) => UiLists.Row(ListOf(picker), row).ChildEntities[0];

    /// <summary>
    /// Calls <paramref name="picked"/> with the tab and entry each time the user picks
    /// one (Friflo's <c>AddSignalHandler</c>; the handler goes with the entity).
    /// </summary>
    public static Entity OnPicked(this Entity picker, Action<int, int> picked)
    {
        picker.AddSignalHandler<PickerPicked>(signal => picked(signal.Event.Tab, signal.Event.Entry));
        return picker;
    }

    /// <summary>Sets the pick from code (-1 for none), showing its tab the next time the body opens. Emits nothing.</summary>
    public static void Show(Entity picker, int tab, int entry)
    {
        if (picker.IsNull || !picker.HasComponent<UiTabbedPicker>())
            return;
        ref UiTabbedPicker state = ref picker.GetComponent<UiTabbedPicker>();
        if (state.PickedTab == tab && state.PickedEntry == entry)
            return;
        state.PickedTab = tab;
        state.PickedEntry = entry;
        UiText.Set(ButtonOf(picker), state.Label);
        Fill(picker);
    }

    /// <summary>Whether the body is open.</summary>
    public static bool IsOpen(Entity picker) => !UiLayout.IsHidden(BodyOf(picker));

    /// <summary>Shows tab <paramref name="tab"/>: lights its button and lists its entries from the top.</summary>
    public static void ShowTab(Entity picker, int tab)
    {
        ref UiTabbedPicker state = ref picker.GetComponent<UiTabbedPicker>();
        state.Tab = Math.Clamp(tab, 0, Math.Max(0, state.Tabs.Length - 1));
        int count = state.Tabs.Length == 0 ? 0 : state.Tabs[state.Tab].Entries.Count;
        Entity tabs = TabsOf(picker);
        for (int i = 0; i < tabs.ChildCount; i++)
            UiNode.Wrap(tabs.ChildEntities[i]).Chosen(i == state.Tab);
        UiLists.SetCount(ListOf(picker), count);
        if (state.PickedTab == state.Tab && state.PickedEntry >= 0)
            UiLists.Reveal(ListOf(picker), state.PickedEntry);
        Fill(picker);
    }

    /// <summary>Writes the shown tab's entries into the pooled rows, the pick lit.</summary>
    internal static void Fill(Entity picker)
    {
        UiTabbedPicker state = picker.GetComponent<UiTabbedPicker>();
        if (state.Tabs.Length == 0)
            return;
        IReadOnlyList<string> entries = state.Tabs[state.Tab].Entries;
        UiList view = ListOf(picker).GetComponent<UiList>();
        for (int i = 0; i < view.Shown; i++)
        {
            int entry = view.First + i;
            UiNode.Wrap(EntryOf(picker, i))
                .SetText(entries[entry])
                .Chosen(state.PickedTab == state.Tab && state.PickedEntry == entry);
        }
    }
}
