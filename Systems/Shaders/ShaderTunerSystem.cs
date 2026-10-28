using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Shaders;

/// <summary>
/// F8 opens a panel with a slider for every channel of every uniform on the
/// shaders in use. Edits apply live; SAVE, or closing the panel, writes them
/// to the shader parameter file, which is applied again on the next launch.
/// </summary>
public sealed class ShaderTunerSystem(
    EntityStore entities,
    IKeyboardState keyboard,
    ShaderParamStore store,
    Key key = Key.F8) : QuerySystem<UiSlider, ShaderParam>
{
    private const int LayerZ = 1003;
    private const float PanelCenterX = 1640;
    private const float PanelWidth = 520;
    private const float RowHeight = 30;
    private const float PickerBlockHeight = ColorPickerLayout.Height + 34;
    private const string SquareShader = "res://Assets/Shaders/Ui/color_sv.gdshader";
    private const string HueShader = "res://Assets/Shaders/Ui/color_hue.gdshader";
    private const string UiShaders = "res://Assets/Shaders/Ui/";

    private static readonly Color PanelColor = DebugPanelStyle.Background;
    private static readonly Color Accent = DebugPanelStyle.Accent;

    private readonly List<Entity> _parts = new();
    private readonly List<ShaderTarget> _targets = new();
    private Entity _layer;
    private Entity _footer;
    private readonly OverlayLatch _latch = new(new KeyboardToggleSource(keyboard, key));

    /// <param name="Slot">
    /// Set for a level's own shader: its tuned values live in the level, so
    /// they are written there as they change and saved with the level.
    /// </param>
    private sealed record ShaderTarget(
        string Shader,
        List<ShaderMaterial> Materials,
        List<ShaderUniform> Uniforms,
        ShaderSlot? Slot = null,
        string? Title = null);

    public bool IsOpen => !_layer.IsNull;

    protected override void OnUpdate()
    {
        if (_latch.Update())
        {
            if (_latch.Visible)
                Open();
            else
                Close();
            if (!IsOpen)
                _latch.Hide();
        }

        if (!IsOpen)
            return;

        HandleButtons();
        ApplySliders();
    }

    private void Open()
    {
        CollectTargets();
        if (_targets.Count == 0)
            return;

        int rows = 0;
        int pickers = 0;
        foreach (ShaderTarget target in _targets)
        {
            foreach (ShaderUniform uniform in target.Uniforms)
            {
                if (uniform.Kind == UniformKind.Color)
                    pickers++;
                else
                    rows += uniform.Channels;
            }
        }

        float height = 30 + _targets.Count * 36 + rows * RowHeight +
            pickers * PickerBlockHeight + 90;
        float top = 20;
        float centerY = top + height / 2;

        UiBuilder ui = UiBuilder.Begin(entities).Layer(LayerZ);
        _layer = ui.Current;
        Track(ui.RectEntity(
            new Vector2(PanelCenterX, centerY),
            new Vector2(PanelWidth, height),
            PanelColor));

        float y = top + 30;
        foreach (ShaderTarget target in _targets)
        {
            Entity header = Label(
                ui,
                PanelCenterX - PanelWidth / 2 + 16,
                y,
                PanelWidth - 32,
                target.Title ?? target.Shader.Split('/')[^1],
                18,
                Accent);
            Track(header);
            y += 36;

            ShaderMaterial material = target.Materials[0];
            foreach (ShaderUniform uniform in target.Uniforms)
            {
                float[] values = ShaderUniforms.Read(material, uniform);
                float[] defaults = ShaderUniforms.Default(material.Shader, uniform);
                if (uniform.Kind == UniformKind.Color)
                {
                    AddColorPicker(ui, target.Shader, uniform, values, defaults, y);
                    y += PickerBlockHeight;
                    continue;
                }

                for (int channel = 0; channel < uniform.Channels; channel++)
                {
                    AddRow(ui, target.Shader, uniform, channel, values[channel], defaults[channel], y);
                    y += RowHeight;
                }
            }
        }

        y += 14;
        AddButton(ui, "SAVE", ShaderTunerAction.Save, PanelCenterX - 70, y);
        AddButton(ui, "RESET", ShaderTunerAction.Reset, PanelCenterX + 70, y);
        _footer = Label(
            ui,
            PanelCenterX - PanelWidth / 2 + 16,
            y + 40,
            PanelWidth - 32,
            store.Path,
            11,
            new Color("#94a3b8"));
        Track(_footer);
    }

    private void CollectTargets()
    {
        _targets.Clear();
        foreach (Entity entity in entities.Query<UiMaterial>().ToEntityList())
        {
            if (entity.GetComponent<UiMaterial>().Material is not ShaderMaterial
                {
                    Shader: { ResourcePath: { Length: > 0 } path } shader,
                } material)
            {
                continue;
            }

            // The tuner's own colour pickers are drawn with shaders too.
            if (path.StartsWith(UiShaders, StringComparison.Ordinal))
                continue;

            ShaderTarget? target = _targets.Find(existing => existing.Shader == path);
            if (target is null)
            {
                target = new ShaderTarget(path, new(), ShaderUniforms.Describe(shader));
                if (target.Uniforms.Count == 0)
                    continue;

                _targets.Add(target);
            }

            target.Materials.Add(material);
        }

        CollectLevelShaders();
    }

    private void CollectLevelShaders()
    {
        foreach (Entity entity in entities.Query<LevelShaders>().ToEntityList())
        {
            LevelShaders shaders = entity.GetComponent<LevelShaders>();
            for (int i = 0; i < shaders.Slots.Length; i++)
            {
                if (shaders.Slots[i] is not { Material: { Shader: { } shader } material } slot)
                    continue;

                List<ShaderUniform> uniforms = ShaderUniforms.Describe(shader);
                if (uniforms.Count == 0)
                    continue;

                string name = slot.Name.Length > 0 ? slot.Name : "unnamed";
                _targets.Add(new ShaderTarget(
                    $"level:{i + 1}",
                    [material],
                    uniforms,
                    slot,
                    $"Level shader {i + 1}: {name}"));
            }
        }
    }

    private void AddRow(
        UiBuilder ui,
        string shader,
        ShaderUniform uniform,
        int channel,
        float value,
        float defaultValue,
        float y)
    {
        float left = PanelCenterX - PanelWidth / 2 + 16;
        Entity text = Label(
            ui,
            left,
            y,
            250,
            "",
            14,
            new Color("#e2e8f0"));
        Entity track = ui.RectEntity(
            new Vector2(left + 250 + 120, y),
            new Vector2(230, 14),
            new Color("#334155"),
            takesInput: true);
        Entity fill = ui.RectEntity(
            new Vector2(left + 250 + 120, y),
            new Vector2(230, 14),
            Accent,
            "");
        track.AddComponent(new UiSlider(
            fill,
            text,
            uniform.Min,
            uniform.Max,
            Mathf.Clamp(value, uniform.Min, uniform.Max),
            uniform.ChannelName(channel) + "  ",
            uniform.Kind == UniformKind.Int ? "0" : "0.###",
            defaultValue));
        track.AddComponent(new ShaderParam(shader, uniform.Name, channel, defaultValue));
        Track(text);
        Track(track);
        Track(fill);
    }

    private void AddColorPicker(
        UiBuilder ui,
        string shader,
        ShaderUniform uniform,
        float[] value,
        float[] defaults,
        float y)
    {
        float left = PanelCenterX - PanelWidth / 2 + 16;
        Track(Label(ui, left, y, PanelWidth - 32, uniform.Name, 14, new Color("#e2e8f0")));

        Vector2 center = new(
            left + ColorPickerLayout.Size.X / 2,
            y + 16 + ColorPickerLayout.Size.Y / 2);
        List<Entity> parts = ui.ColorPicker(
            center,
            new Color(value[0], value[1], value[2], value[3]),
            ShaderMaterials.Load(SquareShader),
            ShaderMaterials.Load(HueShader));
        parts[0].AddComponent(new ShaderColorParam(
            shader,
            uniform.Name,
            new Color(defaults[0], defaults[1], defaults[2], defaults[3])));
        _parts.AddRange(parts);
    }

    private void AddButton(UiBuilder ui, string label, ShaderTunerAction action, float x, float y)
    {
        Entity button = ui.RectEntity(
            new Vector2(x, y + 14),
            new Vector2(120, 34),
            new Color("#334155"),
            label,
            takesInput: true);
        button.AddComponent(new TextStyle(16, Colors.White));
        button.AddComponent(ButtonVisual.Create(new Color("#334155")));
        button.AddComponent(new ShaderTunerButton(action));
        Track(button);
    }

    private static Entity Label(
        UiBuilder ui,
        float left,
        float y,
        float width,
        string text,
        int fontSize,
        Color color)
    {
        Entity label = ui.Text(
            text,
            new Vector2(width, fontSize * 1.5f),
            fontSize,
            color,
            TextAlign.Left,
            new Vector2(left + width / 2, y));
        return label;
    }

    private void Track(Entity entity) => _parts.Add(entity);

    /// <summary>Current slider values, grouped as one array per uniform.</summary>
    private Dictionary<(string, string), float[]> GatherValues()
    {
        var values = new Dictionary<(string, string), float[]>();
        foreach (ShaderTarget target in _targets)
        {
            foreach (ShaderUniform uniform in target.Uniforms)
                values[(target.Shader, uniform.Name)] = new float[uniform.Channels];
        }

        Query.ForEachEntity((ref slider, ref param, _) =>
        {
            if (values.TryGetValue((param.Shader, param.Uniform), out float[]? channels) &&
                param.Channel < channels.Length)
            {
                channels[param.Channel] = slider.Value;
            }
        });

        foreach (Entity picker in entities.Query<UiColorPicker, ShaderColorParam>().ToEntityList())
        {
            ShaderColorParam param = picker.GetComponent<ShaderColorParam>();
            if (!values.TryGetValue((param.Shader, param.Uniform), out float[]? channels))
                continue;

            Color color = picker.GetComponent<UiColorPicker>().Color;
            channels[0] = color.R;
            channels[1] = color.G;
            channels[2] = color.B;
            channels[3] = color.A;
        }

        return values;
    }

    /// <summary>Pushes each slider's value onto every material using its shader.</summary>
    private void ApplySliders()
    {
        var values = GatherValues();

        foreach (ShaderTarget target in _targets)
        {
            foreach (ShaderUniform uniform in target.Uniforms)
            {
                float[] channels = values[(target.Shader, uniform.Name)];
                foreach (ShaderMaterial material in target.Materials)
                {
                    if (GodotObject.IsInstanceValid(material))
                        ShaderUniforms.Write(material, uniform, channels);
                }

                if (target.Slot is { } slot)
                    slot.Params[uniform.Name] = (float[])channels.Clone();
            }
        }
    }

    private void HandleButtons()
    {
        foreach (Entity button in entities
                     .Query<ShaderTunerButton>()
                     .AllTags(Tags.Get<Pressed>())
                     .ToEntityList())
        {
            button.RemoveTag<Pressed>();
            if (button.GetComponent<ShaderTunerButton>().Action == ShaderTunerAction.Save)
                Save();
            else
                ResetSliders();
        }
    }

    private void ResetSliders()
    {
        foreach (Entity slider in entities.Query<UiSlider, ShaderParam>().ToEntityList())
        {
            slider.GetComponent<UiSlider>().Value = slider.GetComponent<ShaderParam>().Default;
        }

        foreach (Entity picker in entities.Query<UiColorPicker, ShaderColorParam>().ToEntityList())
            picker.GetComponent<UiColorPicker>().Color = picker.GetComponent<ShaderColorParam>().Default;
    }

    private void Save()
    {
        var values = GatherValues();

        // A level's own shaders were already written into the level as they changed.
        foreach (((string shader, string uniform), float[] channels) in values)
        {
            if (!_targets.Exists(target => target.Shader == shader && target.Slot is not null))
                store.Set(shader, uniform, channels);
        }

        try
        {
            store.Save();
            SceneText(_footer, "saved: " + store.Path);
        }
        catch (Exception exception)
        {
            GD.PushError($"Could not save shader parameters: {exception.Message}");
            SceneText(_footer, "save failed: " + exception.Message);
        }
    }

    private static void SceneText(Entity entity, string text)
    {
        if (entity.IsNull || !entity.HasComponent<Text>())
            return;

        entity.GetComponent<Text>().Value = text;
        entity.AddTag<Dirty>();
    }

    private void Close()
    {
        Save();
        foreach (Entity part in _parts)
        {
            if (!part.IsNull)
                part.DeleteEntity();
        }

        _parts.Clear();
        _layer.DeleteEntity();
        _layer = default;
        _targets.Clear();
    }
}
