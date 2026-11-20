using System;
using System.Collections.Generic;
using Xanadu.Logic.Audio;
using Xanadu.Logic.Screens;
using Xanadu.Model.Settings;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Logic.Calibration;

/// <summary>The calibration screen's entrances. Nudge takes the step in ms (±1); taps come from the keyboard.</summary>
public enum OffsetWizardEntrance
{
    Start,
    Apply,
    Back,
    Nudge,
    Reset,
}

/// <summary>Where a calibration run is, for the layout to put into words.</summary>
public enum OffsetWizardPhase
{
    Ready,
    MissingTrack,
    CountIn,
    Tapping,
    TooFewTaps,
    Measured,
    Applied,
}

/// <summary>
/// The global offset calibration: plays the click track, takes SPACE taps with the
/// clock play judges with, measures the offset that centres them and applies it on
/// request. Taps are read from the keyboard here, not bound by a layout, so their
/// timing never waits on the UI.
/// </summary>
public sealed class OffsetWizardLogic(
    ITrackAudio audio,
    OffsetSettings settings,
    IKeyboardState? keyboard,
    IScreenNavigator navigator) : IScreenLogic<OffsetWizardEntrance>
{
    /// <summary>Spread above which a run is too unsteady to rely on, in milliseconds.</summary>
    public const double ShakySpreadMs = 30d;

    private bool _wasSpace;
    private bool _sawPlayback;

    public ScreenContract<OffsetWizardEntrance> Contract { get; } = new ScreenContract<OffsetWizardEntrance>()
        .Mandatory(OffsetWizardEntrance.Start)
        .Mandatory(OffsetWizardEntrance.Apply)
        .Mandatory(OffsetWizardEntrance.Back)
        .Optional(OffsetWizardEntrance.Nudge)
        .Optional(OffsetWizardEntrance.Reset);

    public OffsetWizardPhase Phase { get; private set; } = OffsetWizardPhase.Ready;

    public bool Listening { get; private set; }

    /// <summary>The run so far: its taps, and <see cref="OffsetCalibration.Measure"/>.</summary>
    public OffsetCalibration Run { get; private set; } = new();

    public IEnumerable<double> Taps => Run.Taps;

    public OffsetMeasurement? Measurement => Run.Measure();

    /// <summary>Whether the run measured enough steady taps to apply.</summary>
    public bool CanApply => !Listening && Measurement is { } m && m.Used >= OffsetCalibration.MinimumTaps;

    public long OffsetMs => settings.OffsetMs;

    public void Do(OffsetWizardEntrance entrance, int arg)
    {
        switch (entrance)
        {
            case OffsetWizardEntrance.Start:
                Start();
                break;
            case OffsetWizardEntrance.Apply:
                Apply();
                break;
            case OffsetWizardEntrance.Back:
                navigator.Back();
                break;
            case OffsetWizardEntrance.Nudge:
                settings.Set(settings.OffsetMs + Math.Sign(arg));
                break;
            case OffsetWizardEntrance.Reset:
                settings.Set(0);
                break;
        }
    }

    public void Update()
    {
        if (keyboard is not null)
        {
            bool space = keyboard.IsPressed(Key.Space);
            if (space && !_wasSpace)
                TapOrStart();
            _wasSpace = space;
        }

        if (!Listening)
            return;

        if (audio.IsPlaying)
        {
            _sawPlayback = true;
            if (Run.TapCount == 0 && OffsetCalibration.InCountIn(audio.PositionSeconds))
                Phase = OffsetWizardPhase.CountIn;
        }
        else if (_sawPlayback)
        {
            Finish();
        }
    }

    /// <summary>Leaving by any route must not leave the clicks playing.</summary>
    public void Close()
    {
        if (Listening)
            audio.Stop();
        Listening = false;
    }

    private void TapOrStart()
    {
        if (!Listening)
        {
            Start();
            return;
        }

        if (!Run.Tap(audio.PositionSeconds))
            return;

        Phase = OffsetWizardPhase.Tapping;
        if (Run.IsComplete)
            Finish();
    }

    private void Start()
    {
        if (Listening)
            return;

        if (!audio.Play(OffsetCalibration.TrackPath, 1f))
        {
            Phase = OffsetWizardPhase.MissingTrack;
            return;
        }

        Run = new OffsetCalibration();
        Listening = true;
        _sawPlayback = false;
        Phase = OffsetWizardPhase.CountIn;
    }

    private void Finish()
    {
        Listening = false;
        audio.Stop();
        Phase = CanApply ? OffsetWizardPhase.Measured : OffsetWizardPhase.TooFewTaps;
    }

    private void Apply()
    {
        if (!CanApply || Measurement is not { } result)
            return;

        settings.Set((long)Math.Round(result.OffsetMs));
        Phase = OffsetWizardPhase.Applied;
    }
}
