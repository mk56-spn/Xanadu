using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Input;
using Xanadu.Systems.Layout;
using Xanadu.Systems.Scenes;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Scenes;

/// <summary>The offset wizard's run: track playback, tap averaging, save and reset.</summary>
public sealed class OffsetWizardTests
{
    private FakeAudio _audio = new();
    private FakeKeyboard _keyboard = new();
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private OffsetSettings _settings = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _audio = new FakeAudio();
        _keyboard = new FakeKeyboard();
        _settings = new OffsetSettings(persist: false);
        _root = new SystemRoot(_store);
        UiBuilder.DefaultVisuals = new FakeVisualRidFactory();

        Entity root = _store.CreateEntity();
        OffsetWizardScene.Build(_store, root);

        _root.Add(new ContainerLayoutSystem());
        _root.Add(new OffsetWizardSystem(
            _store,
            _audio,
            _settings,
            _keyboard));
        Frame();
    }

    [TearDown]
    public void TearDown() => UiBuilder.DefaultVisuals = null;

    private static double ClickAt(int beat) =>
        OffsetCalibration.FirstClickSeconds + beat * OffsetCalibration.BeatSeconds;

    /// <summary>A whole run with every counted tap this many milliseconds after its click.</summary>
    private void TapThrough(double lateMs, int taps = OffsetCalibration.MeasuredBeats)
    {
        for (int i = 0; i < taps; i++)
            Tap(ClickAt(OffsetCalibration.CountInBeats + i) + lateMs / 1000d);
    }

    private static float MarkX(Entity mark) => mark.GetComponent<Position>().Value.X;

    [Test]
    public void TheScreenOpensWithItsInstructionAndTheSavedOffset()
    {
        Assert.That(Status(), Is.EqualTo("Press SPACE or START when you are ready."));
        Assert.That(Offset(), Is.EqualTo("GLOBAL OFFSET   0 ms"));
        Assert.That(Wizard().Apply.Tags.Has<UiDisabled>(), Is.True, "nothing to apply yet");
    }

    [Test]
    public void StartingPlaysTheClickTrack()
    {
        Press(Wizard().Start);

        Assert.That(_audio.PlayedPath, Is.EqualTo(OffsetCalibration.TrackPath));
        Assert.That(Wizard().Listening, Is.True);
    }

    [Test]
    public void SpaceStartsARunToo()
    {
        Tap(0);

        Assert.That(_audio.PlayedPath, Is.EqualTo(OffsetCalibration.TrackPath));
        Assert.That(Wizard().Run.TapCount, Is.Zero, "the press that starts it is not a tap");
    }

    [Test]
    public void AFullRunFinishesItselfAndMeasures()
    {
        Press(Wizard().Start);
        TapThrough(30d);

        Assert.That(Wizard().Listening, Is.False);
        Assert.That(Text(Wizard().ResultLabel), Is.EqualTo("+50 ms"), "30 late plus the 20 charts have baked in");
        Assert.That(Status(), Does.StartWith("Steady: 32 taps"));
        Assert.That(Wizard().Apply.Tags.Has<UiDisabled>(), Is.False);
        Assert.That(_settings.OffsetMs, Is.Zero, "not saved until asked");
    }

    [Test]
    public void ApplyingSavesTheOffsetAndMovesTheTapsOntoTheCentre()
    {
        Press(Wizard().Start);
        TapThrough(30d);
        float before = MarkX(Wizard().TapMarks[0]);
        Assert.That(before, Is.EqualTo(50f * OffsetWizardScene.PlotPixelsPerMs).Within(0.5f));

        Press(Wizard().Apply);

        Assert.That(_settings.OffsetMs, Is.EqualTo(50));
        Assert.That(Offset(), Is.EqualTo("GLOBAL OFFSET   +50 ms"));
        Assert.That(MarkX(Wizard().TapMarks[0]), Is.Zero.Within(0.5f));
    }

    [Test]
    public void TooFewTapsCannotBeApplied()
    {
        Press(Wizard().Start);
        TapThrough(30d, taps: OffsetCalibration.MinimumTaps - 1);
        _audio.Stop();
        Frame();

        Assert.That(Wizard().Listening, Is.False);
        Assert.That(Status(), Does.StartWith("Only 15 steady taps"));
        Assert.That(Wizard().Apply.Tags.Has<UiDisabled>(), Is.True);

        Press(Wizard().Apply);
        Assert.That(_settings.OffsetMs, Is.Zero);
    }

    [Test]
    public void AnUnsteadyRunSaysSo()
    {
        Press(Wizard().Start);
        for (int i = 0; i < OffsetCalibration.MeasuredBeats; i++)
            Tap(ClickAt(OffsetCalibration.CountInBeats + i) + (i % 2 == 0 ? -0.045d : 0.045d));

        Assert.That(Status(), Does.StartWith("Unsteady"));
    }

    [Test]
    public void TheStepperNudgesTheOffsetOneMillisecondAtATime()
    {
        Press(Wizard().NudgeUp);
        Press(Wizard().NudgeUp);
        Press(Wizard().NudgeDown);

        Assert.That(_settings.OffsetMs, Is.EqualTo(1));
        Assert.That(Offset(), Is.EqualTo("GLOBAL OFFSET   +1 ms"));
    }

    [Test]
    public void ResetClearsTheOffsetAndKeepsTheResultOnScreen()
    {
        Press(Wizard().Start);
        TapThrough(30d);
        Press(Wizard().Apply);

        Press(Wizard().Reset);

        Assert.That(_settings.OffsetMs, Is.Zero);
        Assert.That(Text(Wizard().ResultLabel), Is.EqualTo("+50 ms"));
    }

    [Test]
    public void RunningAgainClearsTheOldTaps()
    {
        Press(Wizard().Start);
        TapThrough(30d);

        Press(Wizard().Start);

        Assert.That(Wizard().Run.TapCount, Is.Zero);
        Assert.That(Text(Wizard().ResultLabel), Is.Empty);
        Assert.That(Wizard().Apply.Tags.Has<UiDisabled>(), Is.True);
    }

    [Test]
    public void AMissingTrackIsReported()
    {
        _audio.CanPlay = false;

        Press(Wizard().Start);

        Assert.That(Status(), Is.EqualTo("Click track is missing."));
        Assert.That(Wizard().Listening, Is.False);
    }

    [Test]
    public void LeavingTheScreenStopsTheClicks()
    {
        Press(Wizard().Start);
        Assert.That(_audio.IsPlaying, Is.True);

        foreach (Entity entity in _store.Query<OffsetWizard>().ToEntityList())
            entity.DeleteEntity();
        Frame();

        Assert.That(_audio.IsPlaying, Is.False);
    }

    private void Tap(double position)
    {
        _audio.Position = (float)position;
        _keyboard.Space = true;
        Frame();
        _keyboard.Space = false;
        Frame();
    }

    private void Press(Entity button)
    {
        button.AddTag<Pressed>();
        Frame();
    }

    private OffsetWizard Wizard() =>
        _store.Query<OffsetWizard>().ToEntityList()[0].GetComponent<OffsetWizard>();

    private string Status() => Text(Wizard().StatusLabel);

    private string Offset() => Text(Wizard().OffsetLabel);

    private string Text(Entity entity) => entity.GetComponent<Text>().Value;

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private sealed class FakeAudio : ITrackAudio
    {
        public string? PlayedPath { get; private set; }
        public bool CanPlay { get; set; } = true;
        public float Position { get; set; }
        public bool IsPlaying { get; private set; }
        public float PositionSeconds => Position;
        public float LengthSeconds => 5f;

        public readonly List<bool> Calls = new();

        public bool Play(string audioPath, float volume)
        {
            PlayedPath = audioPath;
            Calls.Add(CanPlay);
            IsPlaying = CanPlay;
            return CanPlay;
        }

        public bool Load(string audioPath, float volume) => CanPlay;
        public bool Resume() => CanPlay;
        public void Pause() => IsPlaying = false;
        public void Seek(float seconds) => Position = seconds;
        public void SetVolume(float volume) { }
        public void Stop() => IsPlaying = false;
        public void Dispose() { }
    }

    private sealed class FakeKeyboard : IKeyboardState
    {
        public bool Space { get; set; }
        public bool Escape { get; set; }

        public bool IsPressed(Key key) => key switch
        {
            Key.Space => Space,
            Key.Escape => Escape,
            _ => false,
        };
    }
}
