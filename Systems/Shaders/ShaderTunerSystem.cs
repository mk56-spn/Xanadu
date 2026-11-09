using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Shaders;

/// <summary>
/// F2 opens a panel with a slider for every channel of every uniform of one
/// material in use, and PREV and NEXT step between materials. Each material is
/// tuned alone, even when others share its shader. It is a debugging aid: edits
/// apply live and nothing is saved.
/// </summary>
public sealed class ShaderTunerSystem(
    EntityStore entities,
    IKeyboardState keyboard,
    Key key = DebugKeys.ShaderTuner) : QuerySystem<UiSlider, ShaderParam>
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

    private const string UiShaders = "res://Assets/Shaders/Ui/";


    private readonly List<ShaderTarget> _targets = new();

    // What the sliders last held, so only what they change reaches a material:
    // other systems drive some of these uniforms, such as a tint or the spectrum.
    private readonly Dictionary<string, float[]> _applied = new();
    private int _index;
    private Entity _layer;
    private readonly OverlayLatch _latch = new(new KeyboardToggleSource(keyboard, key));

    /// <param name="Material">The one material this target tunes.</param>
    /// <param name="Uniforms">Its tunable uniforms.</param>
    /// <param name="Title">The panel's heading for it.</param>
    private sealed record ShaderTarget(
        ShaderMaterial Material,
        List<ShaderUniform> Uniforms,
        string Title);

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

        _index = 0;
        Build();
    }

    private void Build()
    {
        _applied.Clear();
        ShaderTarget target = _targets[_index];

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

        if (_targets.Count > 1)
        {
            ui.Container(
                Vector2.Zero,
                new Vector2(Inner, ButtonSize.Y),
                StackDirection.Horizontal,
                spacing: UiMetrics.Space.Md,
                color: Clear);
            AddButton(ui, "PREV", ShaderTunerAction.Previous);
            AddButton(ui, "NEXT", ShaderTunerAction.Next);
            ui.End();
        }

        Label(ui, Inner, $"{_index + 1}/{_targets.Count}  {target.Title}", UiType.Label, DebugPalette.Title);

        foreach (ShaderUniform uniform in target.Uniforms)
        {
            float[] values = ShaderUniforms.Read(target.Material, uniform);
            float[] defaults = ShaderUniforms.Default(target.Material.Shader, uniform);
            if (uniform.Kind == UniformKind.Color)
            {
                AddColorPicker(ui, uniform, values, defaults);
                continue;
            }

            for (int channel = 0; channel < uniform.Channels; channel++)
                AddRow(ui, uniform, channel, values[channel], defaults[channel]);
        }

        ui.Container(
            Vector2.Zero,
            new Vector2(Inner, ButtonSize.Y),
            StackDirection.Horizontal,
            spacing: UiMetrics.Space.Md,
            color: Clear);
        AddButton(ui, "RESET", ShaderTunerAction.Reset);
        ui.End();
        ui.End();
    }

    private void CollectTargets()
    {
        _targets.Clear();
        foreach (Entity entity in entities.Query<UiMaterial>().WithDisabled().ToEntityList())
        {
            // UI materials are engine-free now (docs/moonworks-port.md step 5), so
            // nothing here matches until the tuner reads Slang reflection.
            if ((object)entity.GetComponent<UiMaterial>().Material is not ShaderMaterial
                {
                    Shader: { ResourcePath: { Length: > 0 } path } shader,
                } material)
            {
                continue;
            }

            // The tuner's own colour pickers are drawn with shaders too.
            if (path.StartsWith(UiShaders, StringComparison.Ordinal))
                continue;

            // Entities can share a material, and then they are one thing to tune.
            if (_targets.Exists(existing => existing.Material == material))
                continue;

            List<ShaderUniform> uniforms = ShaderUniforms.Describe(shader);
            if (uniforms.Count == 0)
                continue;

            string title = path.Split('/')[^1];
            if (material.HasMeta(ShaderMaterials.InstanceMeta))
                title += ": " + material.GetMeta(ShaderMaterials.InstanceMeta).AsString();

            _targets.Add(new ShaderTarget(material, uniforms, title));
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
                    material,
                    uniforms,
                    $"Level shader {i + 1}: {name}"));
            }
        }
    }

    private void AddRow(
        UiBuilder ui,
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
        track.AddComponent(new ShaderParam(uniform.Name, channel, defaultValue));
    }

    private static void AddColorPicker(
        UiBuilder ui,
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
            ColorPickerMaterials.Square(),
            ColorPickerMaterials.Hue());
        foreach (Entity part in parts)
            part.AddTag<LayoutIgnore>();
        ui.End();

        parts[0].AddComponent(new ShaderColorParam(
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

    /// <summary>Current slider values, as one array per uniform of the target on screen.</summary>
    private Dictionary<string, float[]> GatherValues(ShaderTarget target)
    {
        var values = new Dictionary<string, float[]>();
        foreach (ShaderUniform uniform in target.Uniforms)
            values[uniform.Name] = new float[uniform.Channels];

        Query.ForEachEntity((ref slider, ref param, _) =>
        {
            if (values.TryGetValue(param.Uniform, out float[]? channels) &&
                param.Channel < channels.Length)
            {
                channels[param.Channel] = slider.Value;
            }
        });

        foreach (Entity picker in entities.Query<UiColorPicker, ShaderColorParam>().ToEntityList())
        {
            ShaderColorParam param = picker.GetComponent<ShaderColorParam>();
            if (!values.TryGetValue(param.Uniform, out float[]? channels))
                continue;

            Color color = picker.GetComponent<UiColorPicker>().Color;
            channels[0] = color.R;
            channels[1] = color.G;
            channels[2] = color.B;
            channels[3] = color.A;
        }

        return values;
    }

    /// <summary>Pushes whatever the sliders changed onto the material on screen.</summary>
    private void ApplySliders()
    {
        ShaderTarget target = _targets[_index];
        var values = GatherValues(target);

        foreach (ShaderUniform uniform in target.Uniforms)
        {
            float[] channels = values[uniform.Name];
            if (!_applied.TryGetValue(uniform.Name, out float[]? applied))
            {
                _applied[uniform.Name] = channels;
                continue;
            }

            if (applied.AsSpan().SequenceEqual(channels))
                continue;

            _applied[uniform.Name] = channels;
            if (GodotObject.IsInstanceValid(target.Material))
                ShaderUniforms.Write(target.Material, uniform, channels);
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
            switch (button.GetComponent<ShaderTunerButton>().Action)
            {
                case ShaderTunerAction.Reset:
                    ResetSliders();
                    break;
                case ShaderTunerAction.Previous:
                    Step(-1);
                    return;
                case ShaderTunerAction.Next:
                    Step(1);
                    return;
            }
        }
    }

    /// <summary>Moves to the next material still alive, rebuilding the panel for it.</summary>
    private void Step(int direction)
    {
        ApplySliders();
        for (int i = 1; i <= _targets.Count; i++)
        {
            int next = Mathf.PosMod(_index + direction * i, _targets.Count);
            if (!GodotObject.IsInstanceValid(_targets[next].Material))
                continue;

            _index = next;
            UiLayout.DeleteSubtree(_layer);
            Build();
            return;
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

    private void Close()
    {
        UiLayout.DeleteSubtree(_layer);
        _layer = default;
        _targets.Clear();
    }
}
