using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>The pooled lists' half: a wheel notch moves a list by rows, and each list is kept fitted to its viewport.</summary>
public sealed partial class ScrollSystem
{
    /// <summary>How many rows one wheel notch moves a list.</summary>
    private const int RowsPerNotch = 3;

    private ArchetypeQuery<UiList, Rectangle, Position>? _lists;

    private ArchetypeQuery<UiList, Rectangle, Position> Lists =>
        _lists ??= Query.Store.Query<UiList, Rectangle, Position>();

    /// <summary>Moves the list's top by the wheel, held in range; the owner hears it straight away when it moved.</summary>
    private static void Scroll(Entity list, int notches)
    {
        UiList view = list.GetComponent<UiList>();
        int before = view.First;
        view.First -= notches * RowsPerNotch;
        UiLists.Settle(list, ref view);
        if (view.First != before)
            list.AddComponent(view);
    }

    /// <summary>
    /// Fits every shown list to its viewport. A change is written straight back to
    /// the store so its owner hears it (the native ComponentChanged event, Update)
    /// and refills its rows there and then: the loop runs over a copy, not inside a
    /// query, and an owner's refill restructures the tree, which must not happen
    /// in the middle of a command buffer's playback.
    /// </summary>
    private void SettleLists()
    {
        foreach (Entity list in Lists.ToEntityList())
        {
            UiList view = list.GetComponent<UiList>();
            if (UiLists.Settle(list, ref view))
                list.AddComponent(view);
        }
    }
}
