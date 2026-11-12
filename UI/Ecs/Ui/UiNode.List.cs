using System;
using Friflo.Engine.ECS;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

/// <summary>
/// A pooled list: a clipped column holding only as many rows as the stage can
/// ever show, standing in for <see cref="Count"/> items. The rows show items
/// <see cref="First"/> onwards; the wheel moves <see cref="First"/>, and rows
/// past the last item or below the viewport are hidden. The owner fills the
/// rows from its own data whenever the list changes (<see cref="UiLists.OnChanged"/>),
/// so a list of thousands costs only its visible rows.
/// </summary>
public struct UiList : IComponent
{
    /// <summary>How many items the list stands for.</summary>
    public int Count;

    /// <summary>The item the top row shows.</summary>
    public int First;

    /// <summary>How many rows show an item now; the rest are hidden.</summary>
    public int Shown;

    /// <summary>One row's height.</summary>
    public float RowHeight;

    /// <summary>The room between rows.</summary>
    public float Gap;
}

public readonly partial struct UiNode
{
    /// <summary>
    /// A pooled list (<see cref="UiList"/>) of rows <paramref name="rowHeight"/>
    /// tall, <paramref name="gap"/> apart. <paramref name="buildRow"/> builds
    /// each pooled row's parts once, into a row that stretches across the list;
    /// the owner then fills them from <see cref="UiLists.Row"/>. The list has no
    /// size of its own: grow or size it.
    /// </summary>
    public UiNode List(float rowHeight, float gap, Action<UiNode> buildRow)
    {
        UiNode list = Column().Gap(gap).Clip().Size(0, 0).Cross(StackCrossAlignment.Stretch);
        int pool = (int)MathF.Ceiling(Stage.Bounds.Size.Y / (rowHeight + gap)) + 1;
        for (int i = 0; i < pool; i++)
        {
            UiNode row = list.Row().Height(rowHeight).Gap(gap).Hidden();
            buildRow(row);
        }

        list.Entity.AddComponent(new UiList { RowHeight = rowHeight, Gap = gap });
        return list;
    }
}

/// <summary>Driving a pooled list (<see cref="UiList"/>) from its owner.</summary>
public static class UiLists
{
    /// <summary>
    /// Calls <paramref name="changed"/> each time the list's items move: it
    /// scrolled, it was resized so more or fewer rows show, or its count was set.
    /// It is the entity's native <c>OnComponentChanged</c>, narrowed to updates of
    /// <see cref="UiList"/>; the handler is dropped with the entity.
    /// </summary>
    public static Entity OnChanged(this Entity list, Action changed)
    {
        list.OnComponentChanged += change =>
        {
            if (change.Action == ComponentChangedAction.Update && change.Type == typeof(UiList))
                changed();
        };
        return list;
    }

    /// <summary>
    /// Makes the list stand for <paramref name="count"/> items, back at the top
    /// unless <paramref name="keepPlace"/>. The owner hears it straight away, so it
    /// refills the rows from <see cref="OnChanged"/> like after a scroll.
    /// </summary>
    public static void SetCount(Entity list, int count, bool keepPlace = false)
    {
        if (list.IsNull || !list.HasComponent<UiList>())
            return;

        UiList view = list.GetComponent<UiList>();
        view.Count = Math.Max(0, count);
        if (!keepPlace)
            view.First = 0;
        Settle(list, ref view);
        list.AddComponent(view);
    }

    /// <summary>The pooled row at <paramref name="index"/>, from the top.</summary>
    public static Entity Row(Entity list, int index) => list.ChildEntities[index];

    /// <summary>How many pooled rows the list has.</summary>
    public static int Pool(Entity list) => list.ChildCount;

    /// <summary>
    /// Fits the list to its viewport: how many rows fit, where the top may go, how
    /// many show an item, and which rows are hidden. True when anything the owner
    /// shows changed.
    /// </summary>
    internal static bool Settle(Entity list, ref UiList view)
    {
        float height = list.GetComponent<Rectangle>().Bounds.Size.Y;
        float pitch = view.RowHeight + view.Gap;
        int fits = pitch > 0 ? Math.Min(Pool(list), (int)((height + view.Gap) / pitch)) : 0;
        int first = Math.Clamp(view.First, 0, Math.Max(0, view.Count - fits));
        int shown = Math.Clamp(view.Count - first, 0, fits);
        bool changed = first != view.First || shown != view.Shown;
        view.First = first;
        view.Shown = shown;

        for (int i = 0; i < Pool(list); i++)
        {
            Entity row = Row(list, i);
            if (UiLayout.IsHidden(row) != (i >= shown))
                UiLayout.SetHidden(row, i >= shown);
        }

        return changed;
    }
}
