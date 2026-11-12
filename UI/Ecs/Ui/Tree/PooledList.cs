using System;
using System.Collections.Generic;
using Xanadu.Ecs;

namespace Xanadu.Ui.Tree;

/// <summary>
/// A pooled list (<see cref="UiList"/>): only the rows that can show exist. Each
/// pooled row is <paramref name="rowHeight"/> tall and stretched across the list;
/// <see cref="Row"/> gives its contents, once per pooled row (the argument is the
/// row's index from the top). <paramref name="changed"/> refills the rows whenever
/// the list scrolls, resizes or is given a count (<see cref="UiLists"/>).
/// </summary>
public sealed class PooledList(
    float rowHeight,
    float gap,
    Action? changed = null,
    float? width = null,
    float grow = 0,
    bool stretch = false)
    : UiSpec(width, null, null, grow, stretch)
{
    public required Func<int, IEnumerable<UiSpec?>> Row { get; init; }

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

        if (changed is not null)
            list.Entity.OnChanged(changed);
        return list;
    }
}
