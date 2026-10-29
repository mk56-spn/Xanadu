using System;
using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Items;

/// <summary>Deep copy and comparison of items, for snapshots that must not share them.</summary>
public static class XifCopy
{
    public static Xif Clone(Xif item)
    {
        var copy = new Xif { Id = item.Id, Name = item.Name, Owner = item.Owner };
        foreach (XifPrimitive primitive in item.Primitives)
        {
            copy.Primitives.Add(new XifPrimitive
            {
                Kind = primitive.Kind,
                Ref = primitive.Ref,
                Shader = Clone(primitive.Shader),
                Points = new(primitive.Points),
                Fill = primitive.Fill,
            });
        }
        foreach (XifShaderRef shader in item.Shaders)
            copy.Shaders.Add(Clone(shader));
        foreach (KeyValuePair<string, float[]> param in item.Params)
            copy.Params[param.Key] = (float[])param.Value.Clone();
        return copy;
    }

    public static bool AreEqual(Xif a, Xif b)
    {
        if (a.Id != b.Id || a.Name != b.Name || a.Owner != b.Owner ||
            a.Primitives.Count != b.Primitives.Count || a.Shaders.Count != b.Shaders.Count ||
            a.Params.Count != b.Params.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Primitives.Count; i++)
        {
            XifPrimitive x = a.Primitives[i];
            XifPrimitive y = b.Primitives[i];
            if (x.Kind != y.Kind || x.Ref != y.Ref || !ShaderEqual(x.Shader, y.Shader) ||
                x.Fill != y.Fill || !x.Points.SequenceEqual(y.Points))
            {
                return false;
            }
        }

        for (int i = 0; i < a.Shaders.Count; i++)
        {
            if (!ShaderEqual(a.Shaders[i], b.Shaders[i]))
                return false;
        }

        foreach (KeyValuePair<string, float[]> param in a.Params)
        {
            if (!b.Params.TryGetValue(param.Key, out float[]? other) || !param.Value.AsSpan().SequenceEqual(other))
                return false;
        }
        return true;
    }

    private static XifShaderRef Clone(XifShaderRef shader) =>
        new() { Scope = shader.Scope, Slot = shader.Slot, Path = shader.Path };

    private static bool ShaderEqual(XifShaderRef a, XifShaderRef b) =>
        a.Scope == b.Scope && a.Slot == b.Slot && a.Path == b.Path;
}
