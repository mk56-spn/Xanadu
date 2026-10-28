using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Settings;
using Xanadu.Theme;

namespace Xanadu;

/// <summary>Native click-track calibration wizard for the global timing offset.</summary>
public partial class OffsetWizardOverlay : CanvasLayer
{
    private const double LeadInSeconds = 1.0;
    private const double BeatIntervalSeconds = 0.5;
    private const int BeatCount = 8;
    private const double HitToleranceSeconds = 0.25;
    private const string TrackPath = "res://Audio/offset-calibration.mp3";

    private readonly OffsetSettings _settings;
    private readonly Action? _onBack;
    private readonly List<double> _samples = new();
    private AudioStreamPlayer _player = null!;
    private Label _offsetLabel = null!;
    private Label _statusLabel = null!;
    private Button _startButton = null!;
    private Button _saveButton = null!;
    private bool _listening;
    private bool _closed;
    private double _average;

    public OffsetWizardOverlay(OffsetSettings settings, Action? onBack)
    {
        _settings = settings;
        _onBack = onBack;
        Layer = 110;
    }

    public override void _Ready()
    {
        BuildUi();
        LoadTrack();
        UpdateOffsetLabel();
        _statusLabel.Text = "Press START, then tap SPACE on every click.";
    }

    public override void _Process(double delta)
    {
        if (_closed)
            return;

        if (_listening && !_player.IsPlaying())
            FinishRun();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_closed || @event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        if (key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape)
        {
            Close();
            _onBack?.Invoke();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_listening)
        {
            if (key.Keycode is Key.Enter or Key.KpEnter or Key.Space ||
                key.PhysicalKeycode is Key.Enter or Key.KpEnter or Key.Space)
            {
                StartRun();
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (key.Keycode != Key.Space && key.PhysicalKeycode != Key.Space)
            return;

        double position = _player.GetPlaybackPosition();
        double relative = (position - LeadInSeconds) / BeatIntervalSeconds;
        int beat = (int)Math.Round(relative);
        double beatPosition = LeadInSeconds + beat * BeatIntervalSeconds;

        if (beat >= 0 &&
            beat < BeatCount &&
            Math.Abs(position - beatPosition) <= HitToleranceSeconds)
        {
            _samples.Add((position - beatPosition) * 1000.0);
            UpdateRunStatus();
        }

        GetViewport().SetInputAsHandled();
    }

    public void Close()
    {
        if (_closed)
            return;

        _closed = true;
        _listening = false;
        _player.Stop();
        QueueFree();
    }

    private void BuildUi()
    {
        Vector2 screen = GetViewport().GetVisibleRect().Size;
        var background = new ColorRect
        {
            Color = new Color(0.01f, 0.02f, 0.04f, 0.82f),
        };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(background);

        var panel = new ColorRect
        {
            Color = new Color(0.035f, 0.05f, 0.09f, 0.98f),
            Position = new Vector2(screen.X / 2 - 390, screen.Y / 2 - 235),
            Size = new Vector2(780, 470),
        };
        AddChild(panel);

        var title = new Label
        {
            Text = "OFFSET WIZARD",
            Position = new Vector2(0, 30),
            Size = new Vector2(780, 48),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", Palette.Accent);
        panel.AddChild(title);

        var instructions = new Label
        {
            Text = "Listen to the eight clicks and tap SPACE exactly with each one.\n" +
                   "The average timing error becomes your global offset.",
            Position = new Vector2(50, 92),
            Size = new Vector2(680, 60),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        instructions.AddThemeFontSizeOverride("font_size", 16);
        instructions.AddThemeColorOverride("font_color", Palette.TextMuted);
        panel.AddChild(instructions);

        _offsetLabel = new Label
        {
            Position = new Vector2(0, 168),
            Size = new Vector2(780, 48),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _offsetLabel.AddThemeFontSizeOverride("font_size", 26);
        _offsetLabel.AddThemeColorOverride("font_color", Palette.TextBright);
        panel.AddChild(_offsetLabel);

        _statusLabel = new Label
        {
            Position = new Vector2(50, 228),
            Size = new Vector2(680, 48),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 16);
        _statusLabel.AddThemeColorOverride("font_color", Palette.TextDim);
        panel.AddChild(_statusLabel);

        _startButton = AddButton(panel, new Vector2(70, 320), "START", StartRun);
        _saveButton = AddButton(panel, new Vector2(290, 320), "SAVE", SaveOffset);
        _saveButton.Disabled = true;
        AddButton(panel, new Vector2(510, 320), "BACK", () =>
        {
            Close();
            _onBack?.Invoke();
        });
        AddButton(panel, new Vector2(290, 386), "RESET OFFSET", ResetOffset);
    }

    private static Button AddButton(
        Control parent,
        Vector2 position,
        string text,
        Action action)
    {
        var button = new Button
        {
            Text = text,
            Position = position,
            Size = new Vector2(180, 46),
        };
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private void LoadTrack()
    {
        _player = new AudioStreamPlayer();
        AddChild(_player);

        string path = ProjectSettings.GlobalizePath(TrackPath);
        if (ResourceLoader.Exists(TrackPath))
        {
            _player.Stream = ResourceLoader.Load<AudioStream>(TrackPath);
        }

        if (_player.Stream is null && System.IO.File.Exists(path))
            _player.Stream = AudioStreamMP3.LoadFromFile(path);

        if (_player.Stream is null)
        {
            _startButton.Disabled = true;
            _statusLabel.Text = "Calibration track is missing.";
        }
    }

    private void StartRun()
    {
        if (_player.Stream is null)
            return;

        _samples.Clear();
        _average = 0;
        _saveButton.Disabled = true;
        _listening = true;
        _player.Play();
        _statusLabel.Text = "Listening...";
    }

    private void UpdateRunStatus()
    {
        _average = 0;
        foreach (double sample in _samples)
            _average += sample;

        _average /= _samples.Count;
        _statusLabel.Text =
            $"{_samples.Count} / {BeatCount} clicks   ·   " +
            $"average {_average:+0;-0;0} ms";

        if (_samples.Count >= BeatCount)
            FinishRun();
    }

    private void FinishRun()
    {
        if (!_listening && _samples.Count == 0)
            return;

        _listening = false;
        _player.Stop();

        if (_samples.Count == 0)
        {
            _statusLabel.Text = "No clicks detected. Try again.";
            return;
        }

        _average = 0;
        foreach (double sample in _samples)
            _average += sample;

        _average /= _samples.Count;
        _statusLabel.Text =
            $"Average {_average:+0;-0;0} ms   ·   " +
            $"Press SAVE to use this offset.";
        _saveButton.Disabled = false;
    }

    private void SaveOffset()
    {
        if (_samples.Count == 0)
            return;

        _settings.Set((long)Math.Round(_average));
        UpdateOffsetLabel();
        _saveButton.Disabled = true;
        _statusLabel.Text =
            $"Saved global offset {_settings.OffsetMs:+0;-0;0} ms.";
    }

    private void ResetOffset()
    {
        _settings.Reset();
        _samples.Clear();
        _average = 0;
        _saveButton.Disabled = true;
        _statusLabel.Text = "Global offset reset to 0 ms.";
        UpdateOffsetLabel();
    }

    private void UpdateOffsetLabel()
    {
        _offsetLabel.Text = $"GLOBAL OFFSET   {_settings.OffsetMs:+0;-0;0} ms";
    }
}
