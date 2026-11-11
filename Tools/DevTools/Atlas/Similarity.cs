using System;
using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Tools.Atlas;

/// <summary>How alike two components are: by their fields' types and by the types that use them, each 0 to 1.</summary>
internal readonly record struct Match(int A, int B, float Shape, float Users)
{
    /// <summary>The two, evenly.</summary>
    public float Score => (Shape + Users) / 2;
}

/// <summary>
/// Component types that could be one type: the same field types (shape), and
/// the same types reading them (users). Both are overlaps over unions, so 1 is
/// identical and 0 is nothing shared. Field names don't count, so
/// <c>{ float Value }</c> and <c>{ float Amount }</c> are the same shape.
/// </summary>
internal sealed class Similarity
{
    private readonly CodeMap _map;
    private readonly List<CodeNode> _components;
    private readonly Dictionary<int, HashSet<int>> _users = new();
    private readonly Dictionary<int, Dictionary<string, int>> _shapes = new();
    private readonly Dictionary<int, List<Match>> _like = new();
    private List<Match>? _pairs;

    public Similarity(CodeMap map)
    {
        _map = map;
        _components = map.Nodes.Where(n => n.Kind == NodeKind.Component).ToList();
        foreach (CodeNode node in _components)
        {
            _users[node.Id] = node.In.Values.SelectMany(set => set).ToHashSet();
            _shapes[node.Id] = node.Fields.GroupBy(f => f.Type).ToDictionary(g => g.Key, g => g.Count());
        }
    }

    public Match Compare(int a, int b) => new(a, b, Shape(_shapes[a], _shapes[b]), Users(a, b));

    /// <summary>Every other component against one, most alike first; ones sharing nothing left out.</summary>
    public List<Match> Like(int id)
    {
        if (!_like.TryGetValue(id, out var list))
        {
            list = _components.Where(n => n.Id != id).Select(n => Compare(id, n.Id))
                .Where(m => m.Score > 0).OrderByDescending(m => m.Score).ThenBy(m => _map.Nodes[m.B].Name, StringComparer.OrdinalIgnoreCase).ToList();
            _like[id] = list;
        }

        return list;
    }

    /// <summary>The most alike pairs of all components, best first.</summary>
    public List<Match> Pairs(int count = 400)
    {
        if (_pairs is null)
        {
            var all = new List<Match>();
            for (int i = 0; i < _components.Count; i++)
            {
                for (int j = i + 1; j < _components.Count; j++)
                {
                    Match match = Compare(_components[i].Id, _components[j].Id);
                    if (match.Score > 0)
                        all.Add(match);
                }
            }

            _pairs = all.OrderByDescending(m => m.Score).ThenBy(m => _map.Nodes[m.A].Name, StringComparer.OrdinalIgnoreCase).Take(count).ToList();
        }

        return _pairs;
    }

    /// <summary>Field types shared, counting repeats, over field types either has. Fieldless types share no shape.</summary>
    private static float Shape(Dictionary<string, int> a, Dictionary<string, int> b)
    {
        int both = 0, either = 0;
        foreach (string type in a.Keys.Union(b.Keys))
        {
            int x = a.GetValueOrDefault(type), y = b.GetValueOrDefault(type);
            both += Math.Min(x, y);
            either += Math.Max(x, y);
        }

        return either == 0 ? 0 : (float)both / either;
    }

    /// <summary>Types using both over types using either, the two themselves left out.</summary>
    private float Users(int a, int b)
    {
        HashSet<int> x = _users[a], y = _users[b];
        int both = x.Count(id => id != b && y.Contains(id));
        int either = x.Union(y).Count(id => id != a && id != b);
        return either == 0 ? 0 : (float)both / either;
    }
}
