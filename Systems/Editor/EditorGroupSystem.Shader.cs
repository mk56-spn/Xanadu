using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Rendering;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The group's item-shader track (docs/render-architecture.md step 8): which
/// shader slot it draws with, which of that shader's instance uniforms is being
/// keyed, and the value buttons that write a key at the playhead.
/// </summary>
public sealed partial class EditorGroupSystem
{
    /// <summary>The instance uniforms the open group's shader declares, in order.</summary>
    private static List<InstanceUniform> ShaderParams(Entity editor, int slot)
    {
        if (slot == 0 || !editor.TryGetComponent(out LevelShaders shaders))
            return new List<InstanceUniform>();

        return shaders.Get(slot) is { } found
            ? ShaderSourceUniforms.InstanceUniforms(found.Source)
            : new List<InstanceUniform>();
    }

    /// <summary>The shader track row: its readouts and which of its buttons apply now.</summary>
    private void ShowShaderTrack(
        Entity editor,
        EditorGroupState state,
        GroupDefinition group,
        Playhead playhead,
        CommandBuffer buffer)
    {
        bool bufferTrack = state.BufferTrack;
        int trackSlot = bufferTrack ? group.BufferShader : group.Shader;
        List<ShaderKey> trackKeys = bufferTrack ? group.BufferKeys : group.ShaderKeys;
        UiText.Set(Piece(EditorGroupPart.ShaderSlot), trackSlot == 0 ? "SHD -" : $"SHD {trackSlot}", buffer);
        Tint(Piece(EditorGroupPart.ShaderSlot), trackSlot == 0 ? Unavailable : ActionIdle, buffer);
        UiText.Set(Piece(EditorGroupPart.BufferTrack), bufferTrack ? "TRACK BUF" : "TRACK ITEM", buffer);
        Tint(Piece(EditorGroupPart.BufferTrack), bufferTrack ? ToggleOn : ActionIdle, buffer);
        UiText.Set(Piece(EditorGroupPart.BufferTint), $"TINT {group.BufferTint.ToHtml()}", buffer);
        UiText.Set(Piece(EditorGroupPart.BufferBlend), $"BLEND {BlendModes.Name(group.BufferBlend)}", buffer);
        UiText.Set(Piece(EditorGroupPart.BufferAlphaDown), $"A {group.BufferAlpha:0.0}", buffer);

        List<InstanceUniform> uniforms = ShaderParams(editor, trackSlot);
        if (trackSlot == 0 || uniforms.Count == 0)
        {
            UiText.Set(
                Piece(EditorGroupPart.ShaderText),
                trackSlot == 0 ? "SHADER OFF" : "NO INSTANCE UNIFORMS",
                buffer);
            UiText.Set(Piece(EditorGroupPart.ShaderParam), "PARAM", buffer);
            UiText.Set(Piece(EditorGroupPart.ShaderChannel), "CH", buffer);
            UiText.Set(Piece(EditorGroupPart.ShaderValue), "", buffer);
            foreach (EditorGroupPart part in ShaderTrackButtons)
                Tint(Piece(part), Unavailable, buffer);
            return;
        }

        int selected = Math.Clamp(state.ShaderParam, 0, uniforms.Count - 1);
        InstanceUniform uniform = uniforms[selected];
        int channel = Math.Clamp(state.ShaderChannel, 0, uniform.Channels - 1);
        float time = SnapIf(state, playhead.TimeMs / 1000f, playhead);
        Vector4 value = ShaderKeyTrack.ValueAt(trackKeys, uniform.Name, time);
        int keys = trackKeys.Count(key => key.Param == uniform.Name);

        UiText.Set(
            Piece(EditorGroupPart.ShaderText),
            $"{(bufferTrack ? "BUFFER" : "SHADER")} {trackSlot} · {keys} KEY{(keys == 1 ? "" : "S")}",
            buffer);
        UiText.Set(Piece(EditorGroupPart.ShaderParam), uniform.Name.ToUpperInvariant(), buffer);
        UiText.Set(
            Piece(EditorGroupPart.ShaderChannel),
            $"CH {channel + 1}/{uniform.Channels}",
            buffer);
        UiText.Set(Piece(EditorGroupPart.ShaderValue), Format(value, uniform.Channels), buffer);
        foreach (EditorGroupPart part in ShaderTrackButtons)
            Tint(Piece(part), ActionIdle, buffer);
        Tint(Piece(EditorGroupPart.ShaderClear), keys > 0 ? ActionIdle : Unavailable, buffer);
    }

    private static readonly EditorGroupPart[] ShaderTrackButtons =
    {
        EditorGroupPart.ShaderParam,
        EditorGroupPart.ShaderChannel,
        EditorGroupPart.ShaderDown,
        EditorGroupPart.ShaderUp,
    };

    /// <summary>The shader track's buttons: the slot, its uniform, and keying a value.</summary>
    private void HandleShaderTrack(
        Entity editor,
        ref EditorGroupState state,
        GroupDefinition group,
        Playhead playhead)
    {
        if (JustPressed(Piece(EditorGroupPart.BufferTrack)))
            state.BufferTrack = !state.BufferTrack;
        bool bufferTrack = state.BufferTrack;

        int NextSlot(int slot) => slot >= ShaderLimits.Count ? 0 : slot + 1;
        if (JustPressed(Piece(EditorGroupPart.ShaderSlot)))
        {
            if (bufferTrack)
            {
                Edit(() =>
                {
                    group.BufferShader = NextSlot(group.BufferShader);
                    group.BufferKeys.Clear();
                });
            }
            else
            {
                Edit(() =>
                {
                    group.Shader = NextSlot(group.Shader);
                    group.ShaderKeys.Clear();
                });
            }

            state.ShaderParam = 0;
            state.ShaderChannel = 0;
        }

        if (JustPressed(Piece(EditorGroupPart.BufferTint)))
        {
            Color[] swatches = EditorGroupPalette.Swatches;
            int at = Array.FindIndex(swatches, colour => colour == group.BufferTint);
            Edit(() => group.BufferTint = swatches[(at + 1) % swatches.Length]);
        }

        if (JustPressed(Piece(EditorGroupPart.BufferAlphaDown)))
            Edit(() => group.BufferAlpha = Math.Max(0f, group.BufferAlpha - 0.1f));
        if (JustPressed(Piece(EditorGroupPart.BufferAlphaUp)))
            Edit(() => group.BufferAlpha = Math.Min(1f, group.BufferAlpha + 0.1f));
        if (JustPressed(Piece(EditorGroupPart.BufferBlend)))
            Edit(() => group.BufferBlend = BlendModes.Step(group.BufferBlend, 1));

        int slot = bufferTrack ? group.BufferShader : group.Shader;
        List<ShaderKey> keys = bufferTrack ? group.BufferKeys : group.ShaderKeys;
        List<InstanceUniform> uniforms = ShaderParams(editor, slot);
        if (uniforms.Count == 0)
            return;

        if (state.ShaderParam < 0 || state.ShaderParam >= uniforms.Count)
            state.ShaderParam = 0;

        if (JustPressed(Piece(EditorGroupPart.ShaderParam)))
        {
            state.ShaderParam = (state.ShaderParam + 1) % uniforms.Count;
            state.ShaderChannel = 0;
        }

        InstanceUniform uniform = uniforms[Math.Clamp(state.ShaderParam, 0, uniforms.Count - 1)];
        if (state.ShaderChannel < 0 || state.ShaderChannel >= uniform.Channels)
            state.ShaderChannel = 0;

        if (JustPressed(Piece(EditorGroupPart.ShaderChannel)))
            state.ShaderChannel = (state.ShaderChannel + 1) % uniform.Channels;

        float now = SnapIf(state, playhead.TimeMs / 1000f, playhead);
        int channel = state.ShaderChannel;
        if (JustPressed(Piece(EditorGroupPart.ShaderDown)))
            Edit(() => EditorShaderKeyEdits.KeyValue(keys, uniform, channel, -0.05f, now));
        if (JustPressed(Piece(EditorGroupPart.ShaderUp)))
            Edit(() => EditorShaderKeyEdits.KeyValue(keys, uniform, channel, 0.05f, now));
        if (JustPressed(Piece(EditorGroupPart.ShaderClear)))
            Edit(() => EditorShaderKeyEdits.Clear(keys, uniform));
    }

    private static string Format(Vector4 value, int channels)
    {
        float[] parts = { value.X, value.Y, value.Z, value.W };
        return string.Join(" ", parts.Take(channels).Select(part => part.ToString("0.00")));
    }
}
