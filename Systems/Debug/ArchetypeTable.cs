using System;
using System.Collections.Generic;
using System.Text;
using Friflo.Engine.ECS;

namespace Xanadu.Systems.Debug;

/// <summary>How the archetype table orders its rows.</summary>
public enum ArchetypeSortOrder
{
    /// <summary>Creation order, so rows do not move while you watch them.</summary>
    Creation,

    /// <summary>Most entities first.</summary>
    Count,

    /// <summary>Largest reserve first.</summary>
    Capacity,
}

/// <summary>
/// One archetype's row in the F3 table. The signature strings are built once:
/// an archetype's component and tag set cannot change after it exists, so only
/// the numbers are refreshed.
/// </summary>
public sealed class ArchetypeRow
{
    internal ArchetypeRow(Archetype archetype, int order)
    {
        Archetype = archetype;
        Order = order;
        Components = JoinComponents(archetype);
        Tags = JoinTags(archetype.Tags);
        BytesPerEntity = BytesPerEntityOf(archetype);
        Count = archetype.Count;
        Capacity = archetype.Capacity;
        Sampled = Count;
    }

    public Archetype Archetype { get; }

    /// <summary>Position in the store's archetype enumeration: stable for the store's life.</summary>
    public int Order { get; }

    /// <summary>Comma-separated component names, untruncated.</summary>
    public string Components { get; }

    /// <summary>Comma-separated tag names, untruncated.</summary>
    public string Tags { get; }

    /// <summary>Bytes one entity occupies in this archetype.</summary>
    public long BytesPerEntity { get; }

    public int Count { get; internal set; }

    public int Capacity { get; internal set; }

    /// <summary>Entities gained (positive) or lost (negative) since the last sample.</summary>
    public int Delta { get; internal set; }

    /// <summary>Consecutive samples in which the row grew.</summary>
    public int Growth { get; internal set; }

    /// <summary>Count when the last sample was taken.</summary>
    internal int Sampled;

    /// <summary>Share of the reserved capacity in use, 0..1.</summary>
    public float Used => Capacity > 0 ? (float)Count / Capacity : 0f;

    /// <summary>Bytes reserved for this archetype, whether or not entities use them.</summary>
    public long ReservedBytes => BytesPerEntity * Capacity;

    private static string JoinComponents(Archetype archetype)
    {
        var builder = new StringBuilder();
        foreach (ComponentType type in archetype.ComponentTypes)
        {
            if (builder.Length > 0)
                builder.Append(", ");
            builder.Append(type.Name);
        }

        return builder.ToString();
    }

    private static string JoinTags(Tags tags)
    {
        string text = tags.ToString();
        const string prefix = "Tags: ";
        return text.StartsWith(prefix, StringComparison.Ordinal)
            ? text[prefix.Length..]
            : text;
    }

    private static long BytesPerEntityOf(Archetype archetype)
    {
        long total = 0;
        foreach (ComponentType type in archetype.ComponentTypes)
            total += type.StructSize;
        return total;
    }
}

/// <summary>
/// The F3 archetype table. Reads live entity counts every frame — archetype count
/// is not a usable change signal, because an entity moving archetype changes no
/// store-wide total — and samples deltas on a timer so growth is visible.
/// </summary>
public sealed class ArchetypeTable
{
    /// <summary>Rows shown per page.</summary>
    public int PageSize { get; set; } = 20;

    private readonly List<ArchetypeRow> _rows = new();
    private readonly List<ArchetypeRow> _view = new();
    private readonly Dictionary<Archetype, ArchetypeRow> _byArchetype = new();
    private ArchetypeRow? _selected;
    private long _nextSampleMs;
    private int _page;

    public ArchetypeSortOrder Sort { get; set; } = ArchetypeSortOrder.Creation;

    /// <summary>How often deltas are sampled, in milliseconds.</summary>
    public long SampleMs { get; set; } = 500;

    /// <summary>Rows in display order.</summary>
    public IReadOnlyList<ArchetypeRow> Rows => _view;

    public int Count => _rows.Count;

    public int Page => _page;

    public int PageCount => Math.Max(1, (_view.Count + PageSize - 1) / PageSize);

    public ArchetypeRow? Selected => _selected;

    public int SelectedIndex => _selected is null ? -1 : _view.IndexOf(_selected);

    /// <summary>Adds new archetypes, refreshes the live numbers, and samples on a timer.</summary>
    public void Refresh(EntityStore store, long nowMs)
    {
        bool added = false;
        foreach (Archetype archetype in store.Archetypes)
        {
            // The store always carries a component-less, tag-less archetype for
            // "no signature" use. It only matters if something actually lives there.
            if (archetype.Count == 0 &&
                archetype.ComponentTypes.Count == 0 &&
                archetype.Tags.Count == 0)
            {
                continue;
            }

            if (!_byArchetype.TryGetValue(archetype, out ArchetypeRow? row))
            {
                row = new ArchetypeRow(archetype, _rows.Count);
                _byArchetype.Add(archetype, row);
                _rows.Add(row);
                added = true;
            }

            row.Count = archetype.Count;
            row.Capacity = archetype.Capacity;
        }

        bool sampled = nowMs >= _nextSampleMs;
        if (sampled)
        {
            foreach (ArchetypeRow row in _rows)
            {
                row.Delta = row.Count - row.Sampled;
                row.Sampled = row.Count;
                row.Growth = row.Delta > 0 ? row.Growth + 1 : 0;
            }

            _nextSampleMs = nowMs + SampleMs;
        }

        if (added || sampled || _view.Count != _rows.Count)
            SortView();

        ClampPage();
    }

    public void MovePage(int delta) =>
        _page = Math.Clamp(_page + delta, 0, PageCount - 1);

    /// <summary>Moves the selection by <paramref name="delta"/>, scrolling the page to follow.</summary>
    public void MoveSelection(int delta)
    {
        if (_view.Count == 0)
        {
            _selected = null;
            return;
        }

        int from = _selected is null
            ? (delta >= 0 ? 0 : _view.Count - 1)
            : _view.IndexOf(_selected) + delta;
        SelectAt(Math.Clamp(from, 0, _view.Count - 1));
    }

    public void CycleSort()
    {
        Sort = Sort switch
        {
            ArchetypeSortOrder.Creation => ArchetypeSortOrder.Count,
            ArchetypeSortOrder.Count => ArchetypeSortOrder.Capacity,
            _ => ArchetypeSortOrder.Creation,
        };
        SortView();
        ClampPage();
    }

    /// <summary>Fills <paramref name="ids"/> with up to <paramref name="max"/> members of the selection.</summary>
    public void FillEntitySample(List<int> ids, int max)
    {
        ids.Clear();
        if (_selected is null)
            return;

        foreach (Entity entity in _selected.Archetype.Entities)
        {
            ids.Add(entity.Id);
            if (ids.Count >= max)
                return;
        }
    }

    private void SelectAt(int index)
    {
        _selected = _view[index];
        _page = index / PageSize;
    }

    private void ClampPage() => _page = Math.Clamp(_page, 0, PageCount - 1);

    private void SortView()
    {
        _view.Clear();
        _view.AddRange(_rows);

        if (Sort == ArchetypeSortOrder.Creation)
            return;

        // Ties fall back to creation order, so rows only move when the numbers do.
        _view.Sort(Sort == ArchetypeSortOrder.Count ? ByCount : ByCapacity);
    }

    private static int ByCount(ArchetypeRow left, ArchetypeRow right)
    {
        int order = right.Count.CompareTo(left.Count);
        return order != 0 ? order : left.Order.CompareTo(right.Order);
    }

    private static int ByCapacity(ArchetypeRow left, ArchetypeRow right)
    {
        int order = right.ReservedBytes.CompareTo(left.ReservedBytes);
        return order != 0 ? order : left.Order.CompareTo(right.Order);
    }
}
