using System;
using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Host.Rendering;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Slang;

namespace Xanadu.Labs.Editor;

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
    private (string Name, int Channel)? _dragging;

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
        float y = Area.Position.Y;
        _canvas.DrawTextStyledLeft(_item, new Rect2(Area.Position.X, y, Area.Size.X, Row), "VALUES", Palette.TextDim, 13);
        y += Row + 2;
        if (_values.Count == 0)
        {
            _canvas.DrawTextStyledLeft(_item, new Rect2(Area.Position.X, y, Area.Size.X, Row), "declare a uniform to tune it here", Palette.TextMuted, 14);
            return;
        }

        foreach ((string name, int channels, bool colour) in _values)
        {
            for (int c = 0; c < channels && y + Row <= Area.End.Y; c++)
            {
                string label = channels == 1 ? name : $"{name}.{(colour ? "rgba" : "xyzw")[c]}";
                DrawSlider(label, name, c, colour, y);
                y += Row;
            }
        }
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
