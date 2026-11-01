using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Theme;

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
    private const float PanelCenterX = 1640;
    private const float PanelWidth = 520;
    private const float RowHeight = 30;
    private const float Top = 20;
    private const float Padding = UiMetrics.Space.Lg;
    private const float Inner = PanelWidth - Padding * 2;
    private const float SliderWidth = 230;
    private const float SliderHeight = 14;

    private static readonly Vector2 ButtonSize = new(120, 34);
    private static readonly Color Clear = new(0, 0, 0, 0);

    private const string SquareShader = "res://Assets/Shaders/Ui/color_sv.gdshader";
    private const string HueShader = "res://Assets/Shaders/Ui/color_hue.gdshader";
    private const string UiShaders = "res://Assets/Shaders/Ui/";


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

        UiBuilder ui = UiBuilder.Begin(entities).Layer(UiLayers.ShaderTuner);
        _layer = ui.Current;
        _layer.AddTag<DebugUi>();

        // Pinned by its top edge, so it grows down with however much there is to tune.
        ui.Container(
            new Vector2(PanelCenterX, Top),
            new Vector2(PanelWidth, 4),
            StackDirection.Vertical,
            spacing: UiMetrics.Space.Xs,
            color: DebugPalette.Panel,
            padding: Padding,
            cross: StackCrossAlignment.Start,
            autoHeight: true,
            anchorTop: true,
            main: StackMainAlignment.Start);

        foreach (ShaderTarget target in _targets)
        {
            Label(ui, Inner, target.Title ?? target.Shader.Split('/')[^1], UiType.Label, DebugPalette.Title);

            ShaderMaterial material = target.Materials[0];
            foreach (ShaderUniform uniform in target.Uniforms)
            {
                float[] values = ShaderUniforms.Read(material, uniform);
                float[] defaults = ShaderUniforms.Default(material.Shader, uniform);
                if (uniform.Kind == UniformKind.Color)
                {
                    AddColorPicker(ui, target.Shader, uniform, values, defaults);
                    continue;
                }

                for (int channel = 0; channel < uniform.Channels; channel++)
                    AddRow(ui, target.Shader, uniform, channel, values[channel], defaults[channel]);
            }
        }

        ui.Container(
            Vector2.Zero,
            new Vector2(Inner, ButtonSize.Y),
            StackDirection.Horizontal,
            spacing: UiMetrics.Space.Md,
            color: Clear);
        AddButton(ui, "SAVE", ShaderTunerAction.Save);
        AddButton(ui, "RESET", ShaderTunerAction.Reset);
        ui.End();
        _footer = Label(ui, Inner, store.Path, UiType.Caption, DebugPalette.TextDim);
        ui.End();
    }

    private void CollectTargets()
    {
        _targets.Clear();
        foreach (Entity entity in entities.Query<UiMaterial>().WithDisabled().ToEntityList())
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
        float defaultValue)
    {
        ui.Container(
            Vector2.Zero,
            new Vector2(Inner, RowHeight),
            StackDirection.Horizontal,
            color: Clear,
            main: StackMainAlignment.Start);
        Entity text = Label(ui, Inner - SliderWidth, "", UiType.Small, DebugPalette.Text);
        Entity track = ui.RectEntity(
            Vector2.Zero,
            new Vector2(SliderWidth, SliderHeight),
            DebugPalette.Control,
            takesInput: true);

        // The slider system lays its fill over the track.
        Entity fill = ui.RectEntity(
            Vector2.Zero,
            new Vector2(SliderWidth, SliderHeight),
            DebugPalette.Title,
            "");
        fill.AddTag<LayoutIgnore>();
        ui.End();

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
    }

    private static void AddColorPicker(
        UiBuilder ui,
        string shader,
        ShaderUniform uniform,
        float[] value,
        float[] defaults)
    {
        Label(ui, Inner, uniform.Name, UiType.Small, DebugPalette.Text);

        // A picker places its own parts around its square, so it gets a slot its size.
        var slot = new Vector2(Inner, ColorPickerLayout.Height);
        ui.Container(Vector2.Zero, slot, StackDirection.Vertical, color: Clear);
        List<Entity> parts = ui.ColorPicker(
            (ColorPickerLayout.Size - slot) / 2,
            new Color(value[0], value[1], value[2], value[3]),
            ShaderMaterials.Load(SquareShader),
            ShaderMaterials.Load(HueShader));
        foreach (Entity part in parts)
            part.AddTag<LayoutIgnore>();
        ui.End();

        parts[0].AddComponent(new ShaderColorParam(
            shader,
            uniform.Name,
            new Color(defaults[0], defaults[1], defaults[2], defaults[3])));
    }

    private static void AddButton(UiBuilder ui, string label, ShaderTunerAction action)
    {
        Entity button = ui.RectEntity(
            Vector2.Zero,
            ButtonSize,
            DebugPalette.Control,
            label,
            takesInput: true);
        button.AddComponent(new TextStyle(UiType.Body, DebugPalette.ControlText));
        button.AddComponent(ButtonVisual.Create(DebugPalette.Control));
        button.AddComponent(new ShaderTunerButton(action));
    }

    private static Entity Label(UiBuilder ui, float width, string text, int fontSize, Color color) =>
        ui.Text(text, new Vector2(width, fontSize * 1.5f), fontSize, color, TextAlign.Left);

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
                     .WithDisabled()
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
        UiLayout.DeleteSubtree(_layer);
        _layer = default;
        _targets.Clear();
    }
}
