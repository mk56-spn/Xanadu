using System;
using System.Collections.Generic;
using Xanadu.Ecs;
using Godot;
using Xanadu.Rendering;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Changing a shader track's keys from the editor: the group panel's item-shader
/// track and the layers panel's pass track both key instance uniforms this way.
/// </summary>
public static class EditorShaderKeyEdits
{
    /// <summary>
    /// Changes one component of a uniform's value at a moment and writes it as a
    /// key there, replacing a key at that moment. The other components keep the
    /// value the track has at that moment.
    /// </summary>
    public static void KeyValue(
        List<ShaderKey> keys,
        InstanceUniform uniform,
        int channel,
        float delta,
        float time)
    {
        Vector4 value = ShaderKeyTrack.ValueAt(keys, uniform.Name, time);
        value = channel switch
        {
            0 => value with { X = value.X + delta },
            1 => value with { Y = value.Y + delta },
            2 => value with { Z = value.Z + delta },
            _ => value with { W = value.W + delta },
        };

        keys.RemoveAll(key => key.Param == uniform.Name && Math.Abs(key.Time - time) < 0.0005f);
        keys.Add(new ShaderKey(uniform.Name, time, value, uniform.Channels, GroupEase.Linear));
    }

    /// <summary>Drops every key of one setting.</summary>
    public static void Clear(List<ShaderKey> keys, InstanceUniform uniform) =>
        keys.RemoveAll(key => key.Param == uniform.Name);
}
