using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Timeline;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Changing a shader track's keys from the editor: the group panel's item-shader
/// and buffer tracks and the layers panel's pass track all key instance
/// uniforms this way. A key is found by its setting and its moment, so edits
/// don't depend on the list's order.
/// </summary>
public static class EditorShaderKeyEdits
{
    /// <summary>Two keys closer than this are at the same moment.</summary>
    public const float SameMoment = 0.0005f;

    /// <summary>
    /// Changes one component of a uniform's value at a moment and writes it as a
    /// key there, replacing a key at that moment (and keeping its easing). The
    /// other components keep the value the track has at that moment, which is
    /// the shader's declared default before anything is keyed.
    /// </summary>
    public static void KeyValue(
        List<ShaderKey> keys,
        InstanceUniform uniform,
        int channel,
        float delta,
        float time)
    {
        Vector4 value = ShaderKeyTrack.ValueAt(keys, uniform.Name, time, uniform.Default);
        value = channel switch
        {
            0 => value with { X = value.X + delta },
            1 => value with { Y = value.Y + delta },
            2 => value with { Z = value.Z + delta },
            _ => value with { W = value.W + delta },
        };

        Put(keys, uniform, time, value);
    }

    /// <summary>Writes a key with <paramref name="value"/> at a moment, replacing one there and keeping its easing.</summary>
    public static void Put(List<ShaderKey> keys, InstanceUniform uniform, float time, Vector4 value)
    {
        int at = Find(keys, uniform.Name, time);
        EasePreset ease = at >= 0 ? keys[at].Ease : EasePreset.Linear;
        keys.RemoveAll(key => key.Param == uniform.Name && Math.Abs(key.Time - time) < SameMoment);
        keys.Add(new ShaderKey(uniform.Name, time, value, uniform.Channels, ease));
    }

    /// <summary>Drops every key of one setting.</summary>
    public static void Clear(List<ShaderKey> keys, InstanceUniform uniform) =>
        keys.RemoveAll(key => key.Param == uniform.Name);

    /// <summary>The index of a setting's key at a moment, or -1.</summary>
    public static int Find(IReadOnlyList<ShaderKey> keys, string param, float time)
    {
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i].Param == param && Math.Abs(keys[i].Time - time) < SameMoment)
                return i;
        }

        return -1;
    }

    /// <summary>A setting's keys in time order.</summary>
    public static List<ShaderKey> Lane(IReadOnlyList<ShaderKey> keys, string param) =>
        keys.Where(key => key.Param == param).OrderBy(key => key.Time).ToList();

    /// <summary>
    /// Moves a key to another moment. A key of the same setting already there
    /// is replaced by the moved one. False when there is no key to move.
    /// </summary>
    public static bool Retime(List<ShaderKey> keys, string param, float from, float to)
    {
        int at = Find(keys, param, from);
        if (at < 0)
            return false;

        ShaderKey moved = keys[at] with { Time = to };
        keys.RemoveAt(at);
        keys.RemoveAll(key => key.Param == param && Math.Abs(key.Time - to) < SameMoment);
        keys.Add(moved);
        return true;
    }

    /// <summary>Sets a key's easing curve. False when there is no key there.</summary>
    public static bool SetEase(List<ShaderKey> keys, string param, float time, EasePreset ease)
    {
        int at = Find(keys, param, time);
        if (at < 0)
            return false;

        keys[at] = keys[at] with { Ease = ease };
        return true;
    }

    /// <summary>Removes one key. False when there is no key there.</summary>
    public static bool Delete(List<ShaderKey> keys, string param, float time)
    {
        int at = Find(keys, param, time);
        if (at < 0)
            return false;

        keys.RemoveAt(at);
        return true;
    }

    /// <summary>
    /// After the track's shader changes: keeps the keys of settings the new
    /// shader still declares (resized to its channels) and drops the rest.
    /// </summary>
    public static void KeepFor(List<ShaderKey> keys, IReadOnlyList<InstanceUniform> uniforms)
    {
        for (int i = keys.Count - 1; i >= 0; i--)
        {
            int match = -1;
            for (int u = 0; u < uniforms.Count; u++)
            {
                if (uniforms[u].Name == keys[i].Param)
                {
                    match = u;
                    break;
                }
            }

            if (match < 0)
                keys.RemoveAt(i);
            else
                keys[i] = keys[i] with { Size = uniforms[match].Channels };
        }
    }

    /// <summary>
    /// Reads a typed value: one number per channel separated by spaces or
    /// commas (one number alone fills every channel), or a <c>#hex</c> colour
    /// for a three- or four-channel setting.
    /// </summary>
    public static bool TryParse(string text, InstanceUniform uniform, out Vector4 value)
    {
        value = Vector4.Zero;
        string trimmed = text.Trim();
        if (trimmed.Length == 0)
            return false;

        if (trimmed.StartsWith('#'))
        {
            if (uniform.Channels < 3 || !Color.HtmlIsValid(trimmed))
                return false;
            Color color = new(trimmed);
            value = new Vector4(color.R, color.G, color.B, uniform.Channels == 4 ? color.A : 0f);
            return true;
        }

        string[] parts = trimmed.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 1 && parts.Length != uniform.Channels)
            return false;

        float[] read = new float[4];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out read[i]))
                return false;
        }

        for (int i = parts.Length; i < uniform.Channels; i++)
            read[i] = read[0];
        value = new Vector4(read[0], read[1], read[2], read[3]);
        return true;
    }

    /// <summary>A value as the fields show it: one number per channel.</summary>
    public static string Format(Vector4 value, int channels)
    {
        float[] parts = { value.X, value.Y, value.Z, value.W };
        return string.Join(
            " ",
            parts.Take(Math.Clamp(channels, 1, 4))
                .Select(part => part.ToString("0.###", CultureInfo.InvariantCulture)));
    }
}

/// <summary>
/// The shader slots as a list to choose from, by name: what the group panel's
/// and the layers panel's SHD dropdowns show.
/// </summary>
public static class ShaderSlotChoice
{
    /// <summary>Options: OFF, then every slot (1-based) with its name, or (empty).</summary>
    public static string[] Labels(LevelShaders? shaders)
    {
        var labels = new string[ShaderLimits.Count + 1];
        labels[0] = "OFF";
        for (int slot = 1; slot <= ShaderLimits.Count; slot++)
            labels[slot] = $"{slot}  {Name(shaders, slot)}";
        return labels;
    }

    /// <summary>What the closed dropdown reads.</summary>
    public static string Button(LevelShaders? shaders, int slot) =>
        slot == 0 ? "SHD OFF ▾" : $"SHD {slot} {Clip(Name(shaders, slot), 9)} ▾";

    private static string Name(LevelShaders? shaders, int slot)
    {
        ShaderSlot? found = shaders is { Slots: not null } level ? level.Get(slot) : null;
        if (found is null || found.Source.Length == 0)
            return "(empty)";
        return found.Name.Length > 0 ? found.Name.ToUpperInvariant() : "UNNAMED";
    }

    private static string Clip(string text, int length) =>
        text.Length <= length ? text : text[..(length - 1)] + "…";
}
