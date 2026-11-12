using System;
using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Tools.Atlas;

/// <summary>
/// How alike two types are, each 0 to 1: their fields' types (shape), the types
/// using them (users), and the types they use (uses). A signal neither type
/// has anything for (two fieldless types, say) is NaN and left out of the score.
/// </summary>
internal readonly record struct Match(int A, int B, float Shape, float Users, float Uses)
{
    /// <summary>The signals both have something for, evenly.</summary>
    public float Score
    {
        get
        {
            float sum = 0;
            int count = 0;
            foreach (float signal in (ReadOnlySpan<float>)[Shape, Users, Uses])
            {
                if (!float.IsNaN(signal))
                {
                    sum += signal;
                    count++;
                }
            }

            return count == 0 ? 0 : sum / count;
        }
    }

    /// <summary>A signal as a percentage, or a dash when neither type has anything for it.</summary>
    public static string Percent(float signal) => float.IsNaN(signal) ? " —" : $"{signal * 100:0}%";
}

/// <summary>
/// Types that could be one type: the same field types (shape), the same types
/// using them (users), and the same types they use (uses). The three mean the
/// same for every kind, so components, systems and classes are all compared
/// fairly: components mostly by shape and users, systems mostly by what they use. Both are overlaps over unions, so 1 is identical
/// and 0 is nothing shared. Field names don't count, so <c>{ float Value }</c>
/// and <c>{ float Amount }</c> are the same shape.
/// <para>
/// It knows nothing of kinds: the caller says which kinds make up the pool
/// (components only, or components against classes, or everything), and only
/// types in the pool are compared. Only pairs that share a field type or a
/// user are scored, since every other pair scores zero.
/// </para>
/// </summary>
internal sealed class Similarity
{
    private readonly CodeMap _map;
    private readonly Dictionary<int, HashSet<int>> _users = new();
    private readonly Dictionary<int, HashSet<int>> _uses = new();
    private readonly Dictionary<int, Dictionary<string, int>> _shapes = new();
    private readonly Dictionary<(int Id, int Pool), List<Match>> _like = new();
    private readonly Dictionary<int, List<Match>> _pairs = new();

    public Similarity(CodeMap map)
    {
        _map = map;
        foreach (CodeNode node in map.Nodes)
        {
            _users[node.Id] = node.In.Values.SelectMany(set => set).ToHashSet();
            _uses[node.Id] = node.Out.Values.SelectMany(set => set).ToHashSet();
            _shapes[node.Id] = node.Fields.GroupBy(f => f.Type).ToDictionary(g => g.Key, g => g.Count());
        }
    }

    public Match Compare(int a, int b) =>
        new(a, b, Shape(_shapes[a], _shapes[b]), Overlap(_users[a], _users[b], a, b), Overlap(_uses[a], _uses[b], a, b));

    /// <summary>Every other type of the pool's kinds against one, most alike first; ones sharing nothing left out.</summary>
    public List<Match> Like(int id, IReadOnlySet<NodeKind> pool)
    {
        int key = Key(pool);
        if (!_like.TryGetValue((id, key), out var list))
        {
            list = Neighbours(id, pool).Select(other => Compare(id, other))
                .Where(m => m.Score > 0).OrderByDescending(m => m.Score).ThenBy(m => _map.Nodes[m.B].Name, StringComparer.OrdinalIgnoreCase).ToList();
            _like[(id, key)] = list;
        }

        return list;
    }

    /// <summary>The most alike pairs among the pool's kinds, best first.</summary>
    public List<Match> Pairs(IReadOnlySet<NodeKind> pool, int count = 400)
    {
        int key = Key(pool);
        if (!_pairs.TryGetValue(key, out var pairs))
        {
            var all = new List<Match>();
            foreach (CodeNode node in _map.Nodes)
            {
                if (!pool.Contains(node.Kind))
                    continue;

                foreach (int other in Neighbours(node.Id, pool))
                {
                    if (other > node.Id)
                    {
                        Match match = Compare(node.Id, other);
                        if (match.Score > 0)
                            all.Add(match);
                    }
                }
            }

            pairs = all.OrderByDescending(m => m.Score).ThenBy(m => _map.Nodes[m.A].Name, StringComparer.OrdinalIgnoreCase).Take(count).ToList();
            _pairs[key] = pairs;
        }

        return pairs;
    }

    /// <summary>The pool's types sharing a field type, a user or a used type with <paramref name="id"/>: the only ones that can score.</summary>
    private HashSet<int> Neighbours(int id, IReadOnlySet<NodeKind> pool)
    {
        var found = new HashSet<int>();
        foreach (int user in _users[id])
            found.UnionWith(_uses[user]);

        foreach (int used in _uses[id])
            found.UnionWith(_users[used]);

        foreach (string type in _shapes[id].Keys)
        {
            foreach (int same in ByFieldType().GetValueOrDefault(type, []))
                found.Add(same);
        }

        found.Remove(id);
        found.RemoveWhere(other => !pool.Contains(_map.Nodes[other].Kind));
        return found;
    }

    private Dictionary<string, List<int>>? _byFieldType;

    /// <summary>Which types have a field of each type, built once.</summary>
    private Dictionary<string, List<int>> ByFieldType()
    {
        if (_byFieldType is null)
        {
            _byFieldType = new Dictionary<string, List<int>>();
            foreach (var (id, shape) in _shapes)
            {
                foreach (string type in shape.Keys)
                {
                    if (!_byFieldType.TryGetValue(type, out var ids))
                        _byFieldType[type] = ids = new List<int>();
                    ids.Add(id);
                }
            }
        }

        return _byFieldType;
    }

    /// <summary>A pool as a bit set of kinds, for the caches.</summary>
    private static int Key(IReadOnlySet<NodeKind> pool)
    {
        int key = 0;
        foreach (NodeKind kind in pool)
            key |= 1 << (int)kind;
        return key;
    }

    /// <summary>Field types shared, counting repeats, over field types either has; NaN for two fieldless types.</summary>
    private static float Shape(Dictionary<string, int> a, Dictionary<string, int> b)
    {
        int both = 0, either = 0;
        foreach (string type in a.Keys.Union(b.Keys))
        {
            int x = a.GetValueOrDefault(type), y = b.GetValueOrDefault(type);
            both += Math.Min(x, y);
            either += Math.Max(x, y);
        }

        return either == 0 ? float.NaN : (float)both / either;
    }

    /// <summary>Links both have over links either has, the two themselves left out; NaN when neither has any.</summary>
    private static float Overlap(HashSet<int> x, HashSet<int> y, int a, int b)
    {
        int both = x.Count(id => id != b && id != a && y.Contains(id));
        int either = x.Union(y).Count(id => id != a && id != b);
        return either == 0 ? float.NaN : (float)both / either;
    }
}
