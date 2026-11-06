using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The group's item-shader track (docs/render-architecture.md step 8): which
/// shader slot it draws with, which of that shader's instance uniforms is being
/// keyed, and the value buttons that write a key at the playhead.
/// </summary>
public sealed partial class EditorGroupSystem
{
    /// <summary>
    /// The setting the shader row edits and the keys of the track it's on (the
    /// item-shader track, or the buffer's while TRACK BUF is on). False when the
    /// track has no shader or its shader declares no instance uniforms.
    /// </summary>
    public static bool TrySelectedSetting(
        Entity editor,
        EditorGroupState state,
        GroupDefinition group,
        out InstanceUniform uniform,
        out List<ShaderKey> keys)
    {
        keys = state.BufferTrack ? group.BufferKeys : group.ShaderKeys;
        List<InstanceUniform> uniforms = ShaderParams(editor, state.BufferTrack ? group.BufferShader : group.Shader);
        if (uniforms.Count == 0)
        {
            uniform = default;
            return false;
        }

        uniform = uniforms[Math.Clamp(state.ShaderParam, 0, uniforms.Count - 1)];
        return true;
    }

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
        LevelShaders? shaders = editor.TryGetComponent(out LevelShaders level) ? level : null;
        ShowShaderChoice(Piece(EditorGroupPart.ShaderSlot), shaders, trackSlot, buffer);
        // Two tabs: the active one is on (bright); BUFFER is slate while the group isn't buffered.
        Tint(Piece(EditorGroupPart.TrackItem), bufferTrack ? ActionIdle : ToggleOn, buffer);
        Tint(Piece(EditorGroupPart.TrackBuffer), bufferTrack ? ToggleOn : group.Buffered ? ActionIdle : Unavailable, buffer);

        List<InstanceUniform> uniforms = ShaderParams(editor, trackSlot);
        if (trackSlot == 0 || uniforms.Count == 0)
        {
            UiText.Set(
                Piece(EditorGroupPart.ShaderText),
                trackSlot == 0 ? "OFF" : "NO SETTINGS",
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
        Vector4 value = ShaderKeyTrack.ValueAt(trackKeys, uniform.Name, time, uniform.Default);
        int keys = trackKeys.Count(key => key.Param == uniform.Name);

        UiText.Set(
            Piece(EditorGroupPart.ShaderText),
            $"{keys} KEY{(keys == 1 ? "" : "S")}",
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
        if (JustPressed(Piece(EditorGroupPart.TrackItem)) && state.BufferTrack)
        {
            state.BufferTrack = false;
            state.SelectedKey = -1f;
        }

        if (JustPressed(Piece(EditorGroupPart.TrackBuffer)) && group.Buffered && !state.BufferTrack)
        {
            state.BufferTrack = true;
            state.SelectedKey = -1f;
        }

        // The buffer's settings only mean something while the group is buffered.
        if (state.BufferTrack && !group.Buffered)
            state.BufferTrack = false;
        bool bufferTrack = state.BufferTrack;

        Entity slotChoice = Piece(EditorGroupPart.ShaderSlot);
        if (!slotChoice.IsNull && slotChoice.TryGetComponent(out DropdownChanged chosen))
        {
            CommandBuffer.RemoveComponent<DropdownChanged>(slotChoice.Id);
            int next = Math.Clamp(chosen.Value, 0, ShaderLimits.Count);
            int current = bufferTrack ? group.BufferShader : group.Shader;
            if (next != current)
            {
                // Keys of settings the new shader also declares carry over.
                List<InstanceUniform> declared = ShaderParams(editor, next);
                Edit(() =>
                {
                    if (bufferTrack)
                    {
                        group.BufferShader = next;
                        EditorShaderKeyEdits.KeepFor(group.BufferKeys, declared);
                    }
                    else
                    {
                        group.Shader = next;
                        EditorShaderKeyEdits.KeepFor(group.ShaderKeys, declared);
                    }
                });
                state.ShaderParam = 0;
                state.ShaderChannel = 0;
                state.SelectedKey = -1f;
            }
        }

        if (JustPressed(Piece(EditorGroupPart.BufferTint)) && bufferTrack)
        {
            bool showing = state.PaletteOpen && state.PaletteForBuffer;
            state.PaletteOpen = !showing;
            state.PaletteForBuffer = !showing;
        }

        if (JustPressed(Piece(EditorGroupPart.BufferAlphaDown)))
            Edit(() => group.BufferAlpha = Math.Max(0f, group.BufferAlpha - 0.1f));
        if (JustPressed(Piece(EditorGroupPart.BufferAlphaUp)))
            Edit(() => group.BufferAlpha = Math.Min(1f, group.BufferAlpha + 0.1f));
        Entity blendChoice = Piece(EditorGroupPart.BufferBlend);
        if (!blendChoice.IsNull && blendChoice.TryGetComponent(out DropdownChanged blend))
        {
            CommandBuffer.RemoveComponent<DropdownChanged>(blendChoice.Id);
            BlendMode mode = BlendModes.All[Math.Clamp(blend.Value, 0, BlendModes.All.Length - 1)];
            if (mode != group.BufferBlend)
                Edit(() => group.BufferBlend = mode);
        }

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
            state.SelectedKey = -1f;
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
        {
            Edit(() => EditorShaderKeyEdits.Clear(keys, uniform));
            state.SelectedKey = -1f;
        }

        HandleKeyLaneButtons(ref state, keys, uniform);
    }

    /// <summary>The buffer's tint, alpha and blend: on the BUFFER tab only.</summary>
    private void ShowBufferSettings(
        EditorGroupState state,
        GroupDefinition group,
        Vector2 dialog,
        Vector2 origin,
        CommandBuffer buffer)
    {
        bool show = state.BufferTrack && group.Buffered;
        foreach (EditorGroupPart part in BufferSettings)
        {
            if (show)
                Place(Piece(part), EditorGroupLayout.Place(part, 0, dialog), origin, buffer);
            else
                Hide(Piece(part), buffer);
        }

        Entity blend = Piece(EditorGroupPart.BufferBlend);
        UiLayout.SetDisabled(blend, buffer, !show || state.PaletteOpen);
        if (!show)
            return;

        Color opaque = new(group.BufferTint.R, group.BufferTint.G, group.BufferTint.B);
        UiText.Set(Piece(EditorGroupPart.BufferTint), $"TINT #{opaque.ToHtml(false)}", buffer);
        Tint(Piece(EditorGroupPart.BufferTint), opaque.Darkened(0.55f), buffer);
        UiText.Set(Piece(EditorGroupPart.BufferAlphaText), $"A {group.BufferAlpha:0.0}", buffer);
        Tint(Piece(EditorGroupPart.BufferAlphaDown), group.BufferAlpha > 0f ? ActionIdle : Unavailable, buffer);
        Tint(Piece(EditorGroupPart.BufferAlphaUp), group.BufferAlpha < 1f ? ActionIdle : Unavailable, buffer);
        UiText.Set(blend, $"BLEND {BlendModes.Name(group.BufferBlend)} ▾", buffer);
        if (blend.HasComponent<Dropdown>() && !blend.HasComponent<DropdownChanged>())
            blend.GetComponent<Dropdown>().Selected = Math.Max(0, Array.IndexOf(BlendModes.All, group.BufferBlend));
    }

    /// <summary>
    /// The SHD dropdown: what it reads closed, its options named after the
    /// level's shaders, and which one is ticked.
    /// </summary>
    public static void ShowShaderChoice(Entity choice, LevelShaders? shaders, int slot, CommandBuffer buffer)
    {
        if (choice.IsNull)
            return;

        UiText.Set(choice, ShaderSlotChoice.Button(shaders, slot), buffer);
        string[] labels = ShaderSlotChoice.Labels(shaders);
        foreach (EntityLink<DropdownOption> link in choice.GetIncomingLinks<DropdownOption>())
        {
            int value = link.Entity.GetComponent<DropdownOption>().Value;
            if (value >= 0 && value < labels.Length)
                UiText.Set(link.Entity, labels[value], buffer);
        }

        if (choice.HasComponent<Dropdown>() && !choice.HasComponent<DropdownChanged>())
            choice.GetComponent<Dropdown>().Selected = slot;
    }

    private static string Format(Vector4 value, int channels) => EditorShaderKeyEdits.Format(value, channels);
}
