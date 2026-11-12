using System;
using System.Collections.Generic;
using Xanadu.Ecs;

namespace Xanadu.Ui.Tree;

/// <summary>
/// A pooled list (<see cref="UiList"/>): only the rows that can show exist. Each
/// pooled row is a row <paramref name="rowHeight"/> tall stretched across the
/// list; <see cref="Row"/> gives its contents, once per pooled row (the argument
/// is the row's index from the top). <see cref="Changed"/> refills the rows
/// whenever the list scrolls, resizes or is given a count (<see cref="UiLists"/>).
/// </summary>
public sealed class PooledList(float rowHeight, float gap) : UiSpec
{
    public required Func<int, IEnumerable<UiSpec?>> Row { get; init; }

    /// <summary>Fills the shown rows from the owner's data.</summary>
    public Action? Changed { get; init; }

    protected override UiNode Create(UiNode parent)
    {
        int index = 0;
        UiNode list = parent.List(rowHeight, gap, row =>
        {
            foreach (UiSpec? spec in Row(index))
            {
                if (spec is not null)
                    row.Add(spec);
            }

            index++;
        });

        if (Changed is { } changed)
            list.Entity.OnChanged(changed);
        return list;
    }
}
