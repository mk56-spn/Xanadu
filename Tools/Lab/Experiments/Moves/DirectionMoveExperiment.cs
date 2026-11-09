using System;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Lab.Experiments.Moves;

/// <summary>
/// A trial of direction keys: notes no longer say where to go, only how hard.
/// A dash note teleports, a momentum note pushes, a hit note just scores; the
/// player picks the way with S D K L and fires the note with F, Space or J.
/// The notes sit in the field along a route; the next one's place says which way.
/// </summary>
public sealed class DirectionMoveExperiment : ILabExperiment
{
    private static readonly int[] TempoSteps = [80, 100, 120, 140];
    private static readonly string[] FrictionNames = ["OFF", "LOW", "HIGH"];

    private int _tempoIndex = 1;
    private int _friction = 1;
    private bool _typed;
    private bool _auto = Array.IndexOf(OS.GetCmdlineUserArgs(), "--auto") >= 0;
    private bool _guides = true;

    private LabStepper _tempoStepper = null!, _frictionStepper = null!, _actionStepper = null!, _inputStepper = null!, _guideStepper = null!;

    public string Name => "MOVE";

    public string[] Notes =>
    [
        "Notes carry strength only; you pick the way.",
        "The next note sits where the right way takes",
        "you: read it and go. S left D right K up L down.",
        "F Space J fire the note on its beat. Hold the",
        "direction, or press it just after the action.",
        "Cyan dash: teleport. Violet momentum: push.",
        "Grey hit: no movement. Pips are strength.",
        "Guides: an arrow and a dotted path show the",
        "way the route went. A hint only, not enforced.",
    ];

    public float ControlsHeight => UiMetrics.Control.Md * 5 + UiTheme.ContainerSpacing * 5;

    public float BeatMs => 60_000f / TempoSteps[_tempoIndex];

    /// <summary>Ground deceleration, as a share of the strongest one.</summary>
    public float Friction => _friction / 2f;

    /// <summary>Whether each action key fires only its own note kind: F dash, Space hit, J momentum.</summary>
    public bool Typed => _typed;

    /// <summary>Whether the field plays itself, the way the route went.</summary>
    public bool Auto => _auto;

    /// <summary>Whether notes show the way the route went: a hint, never enforced.</summary>
    public bool Guides => _guides;

    public Node2D CreateStage(Rect2 area) => new DirectionMoveStage(area, this);

    public void BuildPanel(UiNode panel)
    {
        _tempoStepper = new LabStepper(panel);
        _frictionStepper = new LabStepper(panel);
        _actionStepper = new LabStepper(panel);
        _inputStepper = new LabStepper(panel);
        _guideStepper = new LabStepper(panel);
        Refresh();
    }

    internal void Poll()
    {
        if (_tempoStepper is null)
            return;

        _tempoIndex = _tempoStepper.Clamped(_tempoIndex, TempoSteps.Length);
        _friction = _frictionStepper.Clamped(_friction, FrictionNames.Length);
        if (_actionStepper.Step() != 0)
            _typed = !_typed;
        if (_inputStepper.Step() != 0)
            _auto = !_auto;
        if (_guideStepper.Step() != 0)
            _guides = !_guides;
        Refresh();
    }

    private void Refresh()
    {
        _tempoStepper.SetText($"TEMPO {TempoSteps[_tempoIndex]} BPM");
        _frictionStepper.SetText($"FRICTION {FrictionNames[_friction]}");
        _actionStepper.SetText(_typed ? "ACTIONS TYPED" : "ACTIONS ANY");
        _inputStepper.SetText(_auto ? "INPUT AUTO" : "INPUT KEYS");
        _guideStepper.SetText(_guides ? "GUIDES ON" : "GUIDES OFF");
    }
}
