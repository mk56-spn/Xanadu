using System;
using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Host.Rendering;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Slang;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// A slider for every value the shader declares (one per channel), except the
/// ones the lab fills itself. Dragging sets the value on the preview's
/// material straight away; values are kept by name across recompiles, and a
/// value's range is guessed once, from its declared default, as the F2 tuner does.
/// </summary>
internal sealed class EditorTuning
{
    private const float Row = 24;
    private const float LabelWidth = 150;
    private const float ValueWidth = 70;
    private const int TextSize = 13;

    private readonly IRectCanvas _canvas;
    private readonly Handle _item;
    /// <summary>Where the sliders go; set again when the column is resized.</summary>
    public Rect2 Area { get; set; }
    private readonly HashSet<string> _filledByLab;

    /// <summary>The values in declaration order, one slider per channel.</summary>
    private readonly List<(string Name, int Channels, bool Colour)> _values = new();
    private readonly Dictionary<string, Vector4> _tuned = new();
    private readonly Dictionary<string, (float Min, float Max)> _ranges = new();
    private readonly List<(Rect2 Track, string Name, int Channel)> _tracks = new();

    /// <summary>The <c>uniform bool</c>s: a switch each instead of a slider.</summary>
    private readonly HashSet<string> _switches = new();

    /// <summary>Each switch's box as last drawn, for presses.</summary>
    private readonly List<(Rect2 Box, string Name)> _switchBoxes = new();
    private (string Name, int Channel)? _dragging;

    /// <summary>The play values the code reads that the editor stands in for (<see cref="PlayStandIns"/>), under the shader's own.</summary>
    private readonly List<(string Name, int Channels, bool Colour)> _standIns = new();

    public EditorTuning(IRectCanvas canvas, Handle item, Rect2 area, IEnumerable<string> filledByLab)
    {
        _canvas = canvas;
        _item = item;
        Area = area;
        _filledByLab = new HashSet<string>(filledByLab);
    }

    /// <summary>Takes a fresh compile's values: new ones start at their defaults, kept ones keep their tuning.</summary>
    public void Use(ShaderParam[] declared)
    {
        _values.Clear();
        foreach (ShaderParam param in declared)
        {
            if (_filledByLab.Contains(param.Name))
                continue;

            int channels = Math.Clamp(param.Size / 4, 1, 4);
            bool colour = channels == 4 && UniformRanges.IsColourName(param.Name);
            if (param.IsBool)
                _switches.Add(param.Name);
            else
                _switches.Remove(param.Name);
            _values.Add((param.Name, channels, colour));
            if (!_tuned.ContainsKey(param.Name))
                _tuned[param.Name] = param.Default;
            for (int c = 0; c < channels; c++)
            {
                string key = Key(param.Name, c);
                if (!_ranges.ContainsKey(key))
                    _ranges[key] = UniformRanges.Guess(colour, Channel(param.Default, c));
            }
        }
    }

    /// <summary>Shows a slider for each stand-in the code needs; one dropped keeps its value for when it comes back.</summary>
    public void UseStandIns(List<PlayStandIns.Slider> needed)
    {
        _standIns.Clear();
        foreach (PlayStandIns.Slider slider in needed)
        {
            _standIns.Add((slider.Name, slider.Channels, false));
            if (!_tuned.ContainsKey(slider.Name))
                _tuned[slider.Name] = slider.Default;
            for (int c = 0; c < slider.Channels; c++)
                _ranges.TryAdd(Key(slider.Name, c), (slider.Min, slider.Max));
        }
    }

    /// <summary>A stand-in's value: its slider's, or <paramref name="fallback"/> in every channel before it ever had one.</summary>
    public Vector4 StandIn(string name, float fallback) =>
        _tuned.TryGetValue(name, out Vector4 value) ? value : new Vector4(fallback);

    /// <summary>Puts the tuned values on a material.</summary>
    public void Apply(CanvasMaterial material)
    {
        foreach ((string name, _, _) in _values)
            material.Set(name, _tuned[name]);
    }

    /// <summary>Starts, continues or ends a drag; true while a slider is held.</summary>
    public bool Point(Vector2 pointer, bool held, bool pressed)
    {
        if (!held)
        {
            _dragging = null;
            return false;
        }

        if (pressed)
        {
            foreach ((Rect2 box, string name) in _switchBoxes)
            {
                if (box.HasPoint(pointer))
                {
                    _tuned[name] = _tuned[name] with { X = _tuned[name].X > 0.5f ? 0 : 1 };
                    return true;
                }
            }

            foreach ((Rect2 track, string name, int channel) in _tracks)
            {
                if (new Rect2(track.Position.X - 6, track.Position.Y, track.Size.X + 12, track.Size.Y).HasPoint(pointer))
                    _dragging = (name, channel);
            }
        }

        if (_dragging is not { } drag)
            return false;

        foreach ((Rect2 track, string name, int channel) in _tracks)
        {
            if (name != drag.Name || channel != drag.Channel)
                continue;
            (float min, float max) = _ranges[Key(name, channel)];
            float t = Math.Clamp((pointer.X - track.Position.X) / track.Size.X, 0, 1);
            _tuned[name] = WithChannel(_tuned[name], channel, min + (max - min) * t);
        }

        return true;
    }

    public void Draw()
    {
        _tracks.Clear();
        _switchBoxes.Clear();
        float y = Area.Position.Y;
        _canvas.DrawTextStyledLeft(_item, new Rect2(Area.Position.X, y, Area.Size.X, Row), "VALUES", Palette.TextDim, 13);
        y += Row + 2;
        if (_values.Count == 0)
        {
            _canvas.DrawTextStyledLeft(_item, new Rect2(Area.Position.X, y, Area.Size.X, Row), "declare a uniform to tune it here", Palette.TextMuted, 14);
            y += Row;
        }

        y = DrawSliders(_values, y);
        if (_standIns.Count == 0 || y + Row * 2 > Area.End.Y)
            return;

        y += 4;
        _canvas.DrawTextStyledLeft(_item, new Rect2(Area.Position.X, y, Area.Size.X, Row), "STAND-INS  (the run, faked)", Palette.TextDim, 13);
        DrawSliders(_standIns, y + Row + 2);
    }

    private float DrawSliders(List<(string Name, int Channels, bool Colour)> values, float y)
    {
        foreach ((string name, int channels, bool colour) in values)
        {
            if (_switches.Contains(name) && y + Row <= Area.End.Y)
            {
                DrawSwitch(name, y);
                y += Row;
                continue;
            }

            for (int c = 0; c < channels && y + Row <= Area.End.Y; c++)
            {
                string label = channels == 1 ? name : $"{name}.{(colour ? "rgba" : "xyzw")[c]}";
                DrawSlider(label, name, c, colour, y);
                y += Row;
            }
        }

        return y;
    }

    private void DrawSlider(string label, string name, int channel, bool colour, float y)
    {
        (float min, float max) = _ranges[Key(name, channel)];
        float value = Channel(_tuned[name], channel);
        float left = Area.Position.X;
        var track = new Rect2(left + LabelWidth, y + Row / 2 - 2, Area.Size.X - LabelWidth - ValueWidth - 12, 4);
        _tracks.Add((track, name, channel));

        _canvas.DrawTextMono(_item, new Rect2(left - 4, y, LabelWidth, Row), label, Palette.TextMuted, TextSize, TextAlign.Left);
        _canvas.DrawRect(_item, track, Palette.BorderStrong);
        float t = max > min ? Math.Clamp((value - min) / (max - min), 0, 1) : 0;
        Color fill = colour ? ChannelColour(channel) : Palette.Accent;
        _canvas.DrawRect(_item, new Rect2(track.Position, new Vector2(track.Size.X * t, track.Size.Y)), fill);
        bool active = _dragging is { } d && d.Name == name && d.Channel == channel;
        _canvas.DrawRect(_item, new Rect2(track.Position.X + track.Size.X * t - 5, y + 5, 10, Row - 10), active ? Colors.White : fill);
        _canvas.DrawTextMono(
            _item, new Rect2(track.End.X + 8, y, ValueWidth, Row), value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            Palette.Text, TextSize, TextAlign.Left);
    }

    /// <summary>A uniform bool: its name, then one button that reads ON (bright blue) or OFF.</summary>
    private void DrawSwitch(string name, float y)
    {
        bool on = _tuned[name].X > 0.5f;
        float left = Area.Position.X;
        var box = new Rect2(left + LabelWidth, y + 3, 64, Row - 6);
        _switchBoxes.Add((box, name));
        _canvas.DrawTextMono(_item, new Rect2(left - 4, y, LabelWidth, Row), name, Palette.TextMuted, TextSize, TextAlign.Left);
        _canvas.DrawRect(_item, box, on ? Palette.Primary : Palette.Muted);
        _canvas.DrawTextMono(_item, box, on ? "ON" : "OFF", on ? Palette.TextBright : Palette.TextMuted, TextSize, TextAlign.Center);
    }

    private static Color ChannelColour(int channel) => channel switch
    {
        0 => new Color("#ef4444"),
        1 => new Color("#22c55e"),
        2 => new Color("#3b82f6"),
        _ => new Color("#cbd5e1"),
    };

    private static string Key(string name, int channel) => name + "." + channel;

    private static float Channel(Vector4 value, int channel) => channel switch
    {
        0 => value.X,
        1 => value.Y,
        2 => value.Z,
        _ => value.W,
    };

    private static Vector4 WithChannel(Vector4 value, int channel, float to) => channel switch
    {
        0 => value with { X = to },
        1 => value with { Y = to },
        2 => value with { Z = to },
        _ => value with { W = to },
    };
}
