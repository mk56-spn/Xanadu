using System.Collections.Generic;
using Xanadu.Rendering.Frame;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// The order a frame's steps run their preludes in (Docs/render-graph.md, stage F): tree order, except that a material
/// whose graph writes a <c>[Shared]</c> list or image goes before every material whose graph reads it, as UE5's RDG
/// orders passes by what they produce and consume. Each material sits at a level one past the highest of the writers it
/// reads from; steps run level by level, in tree order within one. Materials that read what each other write (a cycle)
/// stop climbing at the number of materials sharing, and keep tree order between them. A frame with no shared resource
/// runs in tree order and pays one pass over its steps.
/// </summary>
internal sealed class SharedOrder
{
    private readonly List<int> _order = [];
    private readonly List<SharedUse> _uses = [];
    private readonly List<int> _levels = [];
    private readonly Dictionary<CanvasMaterial, int> _user = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, List<int>> _writers = [];
    private readonly List<int> _stepLevel = [];
    private Comparison<int>? _byLevel;
    private int _first;

    /// <summary>The places of <paramref name="steps"/> <paramref name="first"/> to <paramref name="end"/> in the order their preludes run.</summary>
    public IReadOnlyList<int> Of(List<Step> steps, int first, int end, CanvasPipelines pipelines)
    {
        _first = first;
        _order.Clear();
        _uses.Clear();
        _levels.Clear();
        _user.Clear();
        foreach (List<int> writers in _writers.Values)
            writers.Clear();
        bool writes = false;
        for (int s = first; s < end; s++)
        {
            _order.Add(s);
            if (steps[s].Material is not { } material || _user.ContainsKey(material)
                || pipelines.Effect(material).Shared is not { Any: true } use)
                continue;
            _user[material] = _uses.Count;
            foreach (string key in use.Writes)
            {
                if (!_writers.TryGetValue(key, out List<int>? writers))
                    _writers[key] = writers = [];
                writers.Add(_uses.Count);
                writes = true;
            }

            _uses.Add(use);
            _levels.Add(0);
        }

        if (!writes)
            return _order;

        // Longest path from the writers, relaxed until nothing moves, a level never past the number of users.
        for (bool moved = true; moved;)
        {
            moved = false;
            for (int u = 0; u < _uses.Count; u++)
                foreach (string key in _uses[u].Reads)
                {
                    if (!_writers.TryGetValue(key, out List<int>? writers))
                        continue;
                    foreach (int w in writers)
                        if (w != u && _levels[w] + 1 > _levels[u] && _levels[w] < _uses.Count)
                        {
                            _levels[u] = _levels[w] + 1;
                            moved = true;
                        }
                }
        }

        _stepLevel.Clear();
        for (int s = first; s < end; s++)
            _stepLevel.Add(steps[s] is var step && step.Material is { } material && _user.TryGetValue(material, out int u) ? _levels[u] : 0);
        _order.Sort(_byLevel ??= ByLevel);
        return _order;
    }

    private int ByLevel(int a, int b) =>
        _stepLevel[a - _first] != _stepLevel[b - _first] ? _stepLevel[a - _first].CompareTo(_stepLevel[b - _first]) : a.CompareTo(b);
}
