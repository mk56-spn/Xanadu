using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Scenes;

/// <summary>
/// Drives the offset calibration wizard: plays the click track, records taps
/// against the nearest beat, averages them and writes the global offset.
/// </summary>
public sealed class OffsetWizardSystem : QuerySystem<OffsetWizard>
{
    public const string TrackPath = "res://Audio/offset-calibration.mp3";

    private const double LeadInSeconds = 1.0;
    private const double BeatIntervalSeconds = 0.5;
    private const int BeatCount = 8;
    private const double HitToleranceSeconds = 0.25;

    private readonly ITrackAudio _audio;
    private readonly OffsetSettings _settings;
    private readonly IKeyboardState? _keyboard;
    private readonly Action? _onBack;
    private bool _wasSpace;
    private bool _wasEscape;
    private bool _sawPlayback;

    public OffsetWizardSystem(
        EntityStore entities,
        ITrackAudio audio,
        OffsetSettings settings,
        IKeyboardState? keyboard = null,
        Action? onBack = null)
    {
        _audio = audio;
        _settings = settings;
        _keyboard = keyboard;
        _onBack = onBack;
    }

    protected override void OnUpdate()
    {
        foreach (Entity entity in Query.ToEntityList())
        {
            ref OffsetWizard wizard = ref entity.GetComponent<OffsetWizard>();
            Handle(ref wizard);
        }
    }

    private void Handle(ref OffsetWizard wizard)
    {
        if (!wizard.Initialized)
        {
            wizard.Initialized = true;
            UiText.Set(
                wizard.StatusLabel,
                "Press START, then tap SPACE on every click.");
            UpdateOffsetLabel(ref wizard);
        }

        if (_keyboard is not null)
        {
            bool space = _keyboard.IsPressed(Key.Space);
            bool escape = _keyboard.IsPressed(Key.Escape);
            if (space && !_wasSpace)
                TapOrStart(ref wizard);
            if (escape && !_wasEscape)
            {
                _audio.Stop();
                _onBack?.Invoke();
            }

            _wasSpace = space;
            _wasEscape = escape;
        }

        if (IsPressed(wizard.Start) && !wizard.Listening)
        {
            Start(ref wizard);
            wizard.Start.RemoveTag<Pressed>();
        }

        if (IsPressed(wizard.Reset))
        {
            Reset(ref wizard);
            wizard.Reset.RemoveTag<Pressed>();
        }

        if (IsPressed(wizard.Save))
        {
            Save(ref wizard);
            wizard.Save.RemoveTag<Pressed>();
        }

        if (!wizard.Listening)
            return;

        if (_audio.IsPlaying)
        {
            _sawPlayback = true;
        }
        else if (_sawPlayback)
        {
            Finish(ref wizard);
        }
    }

    private void TapOrStart(ref OffsetWizard wizard)
    {
        if (!wizard.Listening)
        {
            Start(ref wizard);
            return;
        }

        double position = _audio.PositionSeconds;
        double relative = (position - LeadInSeconds) / BeatIntervalSeconds;
        int beat = (int)Math.Round(relative);
        double beatPosition = LeadInSeconds + beat * BeatIntervalSeconds;

        if (beat < 0 ||
            beat >= BeatCount ||
            Math.Abs(position - beatPosition) > HitToleranceSeconds)
        {
            return;
        }

        wizard.Samples.Add((position - beatPosition) * 1000.0);
        wizard.Average = Average(wizard.Samples);
        UiText.Set(
            wizard.StatusLabel,
            $"{wizard.Samples.Count} / {BeatCount} clicks   ·   " +
            $"average {wizard.Average:+0;-0;0} ms");

        if (wizard.Samples.Count >= BeatCount)
            Finish(ref wizard);
    }

    private void Start(ref OffsetWizard wizard)
    {
        if (!_audio.Play(TrackPath, 1f))
        {
            UiText.Set(wizard.StatusLabel, "Calibration track is missing.");
            return;
        }

        wizard.Samples.Clear();
        wizard.Average = 0;
        wizard.Listening = true;
        _sawPlayback = false;
        UiText.Set(wizard.StatusLabel, "Listening...");
    }

    private void Finish(ref OffsetWizard wizard)
    {
        if (!wizard.Listening && wizard.Samples.Count == 0)
            return;

        wizard.Listening = false;
        _audio.Stop();

        if (wizard.Samples.Count == 0)
        {
            UiText.Set(wizard.StatusLabel, "No clicks detected. Try again.");
            return;
        }

        wizard.Average = Average(wizard.Samples);
        UiText.Set(
            wizard.StatusLabel,
            $"Average {wizard.Average:+0;-0;0} ms   ·   " +
            "Press SAVE to use this offset.");
    }

    private void Save(ref OffsetWizard wizard)
    {
        if (wizard.Samples.Count == 0)
            return;

        _settings.Set((long)Math.Round(wizard.Average));
        UpdateOffsetLabel(ref wizard);
        UiText.Set(
            wizard.StatusLabel,
            $"Saved global offset {_settings.OffsetMs:+0;-0;0} ms.");
    }

    private void Reset(ref OffsetWizard wizard)
    {
        _settings.Reset();
        _audio.Stop();
        wizard.Samples.Clear();
        wizard.Average = 0;
        wizard.Listening = false;
        UpdateOffsetLabel(ref wizard);
        UiText.Set(wizard.StatusLabel, "Global offset reset to 0 ms.");
    }

    private void UpdateOffsetLabel(ref OffsetWizard wizard) =>
        UiText.Set(
            wizard.OffsetLabel,
            $"GLOBAL OFFSET   {_settings.OffsetMs:+0;-0;0} ms");

    private static double Average(List<double> samples)
    {
        double total = 0;
        foreach (double sample in samples)
            total += sample;

        return total / samples.Count;
    }

    private static bool IsPressed(Entity entity) =>
        !entity.IsNull && entity.Tags.Has<Pressed>();
}
