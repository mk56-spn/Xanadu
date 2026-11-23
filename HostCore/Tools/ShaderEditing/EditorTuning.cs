using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Host.Rendering;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The values column: every value the shader declares, except the ones the lab
/// fills itself, as the shared setting fields the XIF editor shows too (a value
/// field of the value's shape, a pick for <c>[Options]</c>), then a field for
/// each play stand-in the code reads. Values are kept by name across
/// recompiles and put on the preview's materials each frame
/// (<see cref="Apply"/>). The fields are ECS UI on a layer of their own over
/// the editor's chrome; the column is rebuilt only when what it shows changes.
/// </summary>
internal sealed class EditorTuning : IDisposable
{
    private readonly UiNode _layer;
    private readonly UiNode _column;
    private readonly HashSet<string> _filledByLab;

    /// <summary>The shader's own values in declaration order.</summary>
    private readonly List<MaterialParam> _declared = [];

    /// <summary>The play values the code reads that the editor stands in for (<see cref="PlayStandIns"/>), under the shader's own.</summary>
    private readonly List<PlayStandIns.Slider> _standIns = [];

    /// <summary>Every value as last tuned, by name: kept when a compile drops a value, for when it comes back.</summary>
    private readonly Dictionary<string, float[]> _tuned = [];

    /// <summary>Each value's field (or a pick's button) as built.</summary>
    private readonly Dictionary<string, Entity> _fields = [];

    private Rect2 _area;
    private string _shown = "";
    private bool _holding;

    public EditorTuning(EntityStore entities, Rect2 area, IEnumerable<string> filledByLab)
    {
        _filledByLab = [.. filledByLab];
        _layer = UiNode.Layer(entities, default, UiLayers.ShaderEditorValues, null, UiStage.Full).Theme(UiThemes.Tools);
        _column = _layer.Column().Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Start).Clip();
        Area = area;
    }

    /// <summary>Where the fields go; set again when the column is resized.</summary>
    public Rect2 Area
    {
        get => _area;
        set
        {
            bool wider = !Mathf.IsEqualApprox(value.Size.X, _area.Size.X);
            _area = value;
            _column.Box(value);
            if (wider)
                Rebuild();
        }
    }

    /// <summary>Takes a fresh compile's values: new ones start at their defaults, kept ones keep their tuning.</summary>
    public void Use(ShaderParam[] declared)
    {
        _declared.Clear();
        foreach (ShaderParam param in declared)
        {
            if (_filledByLab.Contains(param.Name))
                continue;
            MaterialParam described = MaterialParams.Describe(param);
            _declared.Add(described);
            Remember(described);
        }

        Rebuild();
    }

    /// <summary>Shows a field for each stand-in the code needs; one dropped keeps its value for when it comes back.</summary>
    public void UseStandIns(List<PlayStandIns.Slider> needed)
    {
        _standIns.Clear();
        _standIns.AddRange(needed);
        foreach (PlayStandIns.Slider slider in needed)
            Remember(StandInParam(slider));
        Rebuild();
    }

    /// <summary>A stand-in's value: its field's, or <paramref name="fallback"/> in every channel before it ever had one.</summary>
    public Vector4 StandIn(string name, float fallback) =>
        _tuned.TryGetValue(name, out float[]? value) ? UiValueFields.AsVector(value) : new Vector4(fallback);

    /// <summary>The <c>uniform float2</c>s that read as places (<see cref="SlangPoints.IsPlaceName"/>), for dragging on the preview.</summary>
    public IEnumerable<(string Name, Vector2 Value)> Points()
    {
        foreach (MaterialParam param in _declared)
        {
            if (param is { Channels: 2, IsMatrix: false } && SlangPoints.IsPlaceName(param.Name))
                yield return (param.Name, new Vector2(_tuned[param.Name][0], _tuned[param.Name][1]));
        }
    }

    /// <summary>A float2 dragged on the preview: its value, and its field's span widened to hold it.</summary>
    public void SetPoint(string name, Vector2 value)
    {
        if (!_tuned.TryGetValue(name, out float[]? values) || values.Length < 2)
            return;
        values[0] = value.X;
        values[1] = value.Y;
        if (!_fields.TryGetValue(name, out Entity field) || field.IsNull || !field.HasComponent<UiValueField>())
            return;

        ref UiValueField state = ref field.GetComponent<UiValueField>();
        state.Min = MathF.Min(state.Min, MathF.Min(value.X, value.Y));
        state.Max = MathF.Max(state.Max, MathF.Max(value.X, value.Y));
        UiValueFields.Set(field, values);
    }

    /// <summary>Puts the tuned values on a material.</summary>
    public void Apply(CanvasMaterial material)
    {
        foreach (MaterialParam param in _declared)
        {
            float[] values = _tuned[param.Name];
            if (param.IsMatrix)
                material.Set(param.Name, UiValueFields.AsMatrix(values, UiValueShape.Of(param)));
            else
                material.Set(param.Name, UiValueFields.AsVector(values));
        }
    }

    /// <summary>True while a press that began over the column is held, so nothing under it takes the pointer.</summary>
    public bool Point(Vector2 pointer, bool held, bool pressed)
    {
        if (!held)
            return _holding = false;
        if (pressed)
            _holding = _area.HasPoint(pointer);
        return _holding;
    }

    public void Dispose() => UiLayout.DeleteSubtree(_layer.Entity);

    /// <summary>Starts a value on its default the first time it is seen, or when its shape changed.</summary>
    private void Remember(MaterialParam param)
    {
        float[] defaults = UiValueFields.Defaults(param, UiValueShape.Of(param));
        if (!_tuned.TryGetValue(param.Name, out float[]? kept) || kept.Length != defaults.Length)
            _tuned[param.Name] = defaults;
    }

    private static MaterialParam StandInParam(PlayStandIns.Slider slider) =>
        new(slider.Name, slider.Channels, false, slider.Default);

    /// <summary>Builds the column again, when what it shows (or how wide it is) has changed.</summary>
    private void Rebuild()
    {
        string shown = $"{_area.Size.X}|" + string.Join(",", _declared.Select(Signature)) + "|" + string.Join(",", _standIns.Select(s => s.Name));
        if (shown == _shown)
            return;
        _shown = shown;

        UiLayout.DeleteChildren(_column.Entity);
        _fields.Clear();
        float width = MathF.Max(0, _area.Size.X);
        _column.Text("VALUES", UiType.Caption).DimText();
        if (_declared.Count == 0)
            _column.Text("declare a uniform to tune it here", UiType.Small).DimText();
        foreach (MaterialParam param in _declared)
            Add(param, width, null);

        if (_standIns.Count == 0)
            return;
        _column.Text("STAND-INS  (the run, faked)", UiType.Caption).DimText();
        foreach (PlayStandIns.Slider slider in _standIns)
            Add(StandInParam(slider), width, (slider.Min, slider.Max));
    }

    /// <summary>One value's shared setting field, bound so every edit lands in <see cref="_tuned"/>.</summary>
    private void Add(MaterialParam param, float width, (float Min, float Max)? range)
    {
        string name = param.Name;
        Entity field = _column.SettingField(param, (float[])_tuned[name].Clone(), width, range).Entity;
        _fields[name] = field;
        if (field.HasComponent<Dropdown>())
        {
            field.OnPicked(value => _tuned[name] = [value]);
            return;
        }

        field.OnValueChanged((values, _) => _tuned[name] = (float[])values.Clone());
    }

    private static string Signature(MaterialParam param) =>
        $"{param.Name}:{param.Channels}:{param.Rows}:{param.IsInt}:{param.IsBool}:{string.Join("/", param.Options ?? [])}";
}
