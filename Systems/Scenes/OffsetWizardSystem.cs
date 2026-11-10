using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Input;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Scenes;

/// <summary>
/// Drives the offset calibration screen: plays the click track, takes taps with the
/// clock play judges with, plots each as it would score under the saved offset, and
/// applies the measured offset when asked.
/// </summary>
public sealed class OffsetWizardSystem : QuerySystem<OffsetWizard>
{
    /// <summary>Spread above which a run is too unsteady to rely on, in milliseconds.</summary>
    public const double ShakySpreadMs = 30d;

    private readonly ITrackAudio _audio;
    private readonly OffsetSettings _settings;
    private readonly IKeyboardState? _keyboard;
    private readonly Action? _onBack;
    private bool _wasSpace;
    private bool _wasEscape;
    private bool _sawPlayback;
    private bool _clickTrackOn;

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
        var wizards = Query.ToEntityList();

        // Leaving by any route, BACK included, must not leave the clicks playing.
        if (wizards.Count == 0 && _clickTrackOn)
        {
            _audio.Stop();
            _clickTrackOn = false;
        }

        foreach (Entity entity in wizards)
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
            UiText.Set(wizard.StatusLabel, "Press SPACE or START when you are ready.");
            UiLayout.SetDisabled(wizard.Apply, true);
            ShowOffset(ref wizard);
        }

        if (_keyboard is not null)
        {
            bool space = _keyboard.IsPressed(Key.Space);
            bool escape = _keyboard.IsPressed(Key.Escape);
            if (space && !_wasSpace)
                TapOrStart(ref wizard);
            if (escape && !_wasEscape)
                _onBack?.Invoke();

            _wasSpace = space;
            _wasEscape = escape;
        }

        if (TakePress(wizard.Start))
            Start(ref wizard);
        if (TakePress(wizard.Apply))
            Apply(ref wizard);
        if (TakePress(wizard.Reset))
            SetOffset(ref wizard, 0);
        if (TakePress(wizard.NudgeDown))
            SetOffset(ref wizard, _settings.OffsetMs - 1);
        if (TakePress(wizard.NudgeUp))
            SetOffset(ref wizard, _settings.OffsetMs + 1);

        if (!wizard.Listening)
            return;

        if (_audio.IsPlaying)
        {
            _sawPlayback = true;
            if (wizard.Run.TapCount == 0 && OffsetCalibration.InCountIn(_audio.PositionSeconds))
                UiText.Set(wizard.StatusLabel, "Count in... find the beat.");
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

        if (!wizard.Run.Tap(_audio.PositionSeconds))
            return;

        Plot(ref wizard);
        UiText.Set(
            wizard.StatusLabel,
            $"{wizard.Run.TapCount} / {OffsetCalibration.MeasuredBeats} taps");
        if (wizard.Run.IsComplete)
            Finish(ref wizard);
    }

    private void Start(ref OffsetWizard wizard)
    {
        if (wizard.Listening)
            return;

        if (!_audio.Play(OffsetCalibration.TrackPath, 1f))
        {
            UiText.Set(wizard.StatusLabel, "Click track is missing.");
            return;
        }

        wizard.Run = new OffsetCalibration();
        wizard.Listening = true;
        _sawPlayback = false;
        _clickTrackOn = true;
        UiLayout.SetDisabled(wizard.Apply, true);
        Plot(ref wizard);
        UiText.Set(wizard.StatusLabel, "Count in... find the beat.");
    }

    private void Finish(ref OffsetWizard wizard)
    {
        wizard.Listening = false;
        _audio.Stop();
        _clickTrackOn = false;

        OffsetMeasurement? measured = wizard.Run.Measure();
        if (measured is not { } result || result.Used < OffsetCalibration.MinimumTaps)
        {
            int used = measured?.Used ?? 0;
            UiText.Set(
                wizard.StatusLabel,
                $"Only {used} steady taps, {OffsetCalibration.MinimumTaps} needed. Press SPACE to try again.");
            return;
        }

        UiLayout.SetDisabled(wizard.Apply, false);
        string steadiness = result.SpreadMs > ShakySpreadMs
            ? "Unsteady, so try again if you can"
            : "Steady";
        string strays = result.Rejected > 0 ? $", {result.Rejected} stray left out" : "";
        UiText.Set(
            wizard.StatusLabel,
            $"{steadiness}: {result.Used} taps{strays}, good to about {result.UncertaintyMs:0.0} ms. " +
            "APPLY it, or run again to check.");
    }

    private void Apply(ref OffsetWizard wizard)
    {
        if (wizard.Run.Measure() is not { } result || result.Used < OffsetCalibration.MinimumTaps)
            return;

        SetOffset(ref wizard, (long)Math.Round(result.OffsetMs));
        UiText.Set(wizard.StatusLabel, "Applied. Run again: your taps should now sit on the white line.");
    }

    private void SetOffset(ref OffsetWizard wizard, long offsetMs)
    {
        _settings.Set(offsetMs);
        ShowOffset(ref wizard);
        Plot(ref wizard);
    }

    private void ShowOffset(ref OffsetWizard wizard) =>
        UiText.Set(wizard.OffsetLabel, $"GLOBAL OFFSET   {_settings.OffsetMs:+0;-0;0} ms");

    /// <summary>Puts every tap where it would score under the saved offset, and the result above them.</summary>
    private void Plot(ref OffsetWizard wizard)
    {
        int mark = 0;
        foreach (double tap in wizard.Run.Taps)
            Place(wizard.TapMarks[mark++], tap);
        for (; mark < wizard.TapMarks.Length; mark++)
            UiLayout.SetHidden(wizard.TapMarks[mark], true);

        if (wizard.Run.Measure() is { } result)
        {
            Place(wizard.ResultMark, result.OffsetMs);
            UiText.Set(wizard.ResultLabel, $"{result.OffsetMs:+0;-0;0} ms");
        }
        else
        {
            UiLayout.SetHidden(wizard.ResultMark, true);
            UiText.Set(wizard.ResultLabel, "");
        }
    }

    private void Place(Entity mark, double offsetMs)
    {
        float error = Math.Clamp(
            (float)(offsetMs - _settings.OffsetMs),
            -OffsetWizardScene.PlotHalfRangeMs,
            OffsetWizardScene.PlotHalfRangeMs);
        mark.GetComponent<Position>().Value = new Vector2(error * OffsetWizardScene.PlotPixelsPerMs, 0f);
        mark.AddTag<Moved>();
        UiLayout.SetHidden(mark, false);
    }

    private static bool TakePress(Entity button)
    {
        if (button.IsNull || !button.Tags.Has<Pressed>())
            return false;

        button.RemoveTag<Pressed>();
        return true;
    }
}
