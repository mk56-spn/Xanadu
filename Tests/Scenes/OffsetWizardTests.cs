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

namespace Xanadu.Tests.Scenes;

/// <summary>The offset wizard's run: track playback, tap averaging, save and reset.</summary>
public sealed class OffsetWizardTests
{
    private const double BeatInterval = 0.5;
    private const double LeadIn = 1.0;

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

    [Test]
    public void TheWizardStartsWithInstructionsAndTheGlobalOffset()
    {
        Assert.That(Status(), Is.EqualTo(
            "Press START, then tap SPACE on every click."));
        Assert.That(Offset(), Is.EqualTo("GLOBAL OFFSET   0 ms"));
    }

    [Test]
    public void StartingPlaysTheClickTrack()
    {
        Press(Wizard().Start);

        Assert.That(_audio.PlayedPath, Is.EqualTo(OffsetWizardSystem.TrackPath));
        Assert.That(_audio.Calls, Is.EqualTo(new[] { true }), "play calls");
        Assert.That(Status(), Is.EqualTo("Listening..."));
        Assert.That(Wizard().Listening, Is.True);
    }

    [Test]
    public void ABeatTapIsRecordedAgainstTheNearestClick()
    {
        Press(Wizard().Start);

        Tap(LeadIn + BeatInterval);
        Assert.That(Status(), Does.Contain("1 / 8 clicks"));

        Tap(LeadIn + BeatInterval * 2 + 0.02);
        Assert.That(Status(), Does.Contain("2 / 8 clicks"));
        Assert.That(Status(), Does.Contain("average +10 ms"));
    }

    [Test]
    public void EightClicksFinishTheRunAndSaveWritesTheAverage()
    {
        Press(Wizard().Start);

        for (int beat = 0; beat < 8; beat++)
            Tap(LeadIn + beat * BeatInterval + 0.02);

        Assert.That(Wizard().Listening, Is.False);
        Assert.That(Status(), Does.StartWith("Average +20 ms"));
        Assert.That(Status(), Does.Contain("Press SAVE"));

        Press(Wizard().Save);

        Assert.That(_settings.OffsetMs, Is.EqualTo(20));
        Assert.That(Status(), Is.EqualTo("Saved global offset +20 ms."));
        Assert.That(Offset(), Is.EqualTo("GLOBAL OFFSET   +20 ms"));
    }

    [Test]
    public void ResetClearsTheOffset()
    {
        _settings.Set(120);
        Frame();

        Press(Wizard().Reset);

        Assert.That(_settings.OffsetMs, Is.Zero);
        Assert.That(Status(), Is.EqualTo("Global offset reset to 0 ms."));
        Assert.That(Offset(), Is.EqualTo("GLOBAL OFFSET   0 ms"));
    }

    [Test]
    public void AMissingTrackIsReported()
    {
        _audio.CanPlay = false;

        Press(Wizard().Start);

        Assert.That(Status(), Is.EqualTo("Calibration track is missing."));
        Assert.That(Wizard().Listening, Is.False);
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
