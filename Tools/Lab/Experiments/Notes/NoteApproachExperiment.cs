using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Lab.Experiments.Triangles;
using Xanadu.Theme;

namespace Xanadu.Lab.Experiments.Notes;

/// <summary>How a note arrives at its spot.</summary>
public enum NoteApproachStyle
{
    /// <summary>The game's current look: the note fades in while a ring shrinks onto it.</summary>
    Ring,

    /// <summary>A target with a clock hand appears; the note flies in on a wave and lands on it.</summary>
    Diva,

    /// <summary>Diva, plus the game's guide line between targets with a spark running it to each beat.</summary>
    DivaLine,

    /// <summary>A slot opens at the spot; the note falls straight down its column into it.</summary>
    Drop,
}

/// <summary>
/// A prototype of how notes approach, on a looping chart of hit notes. Each tab
/// is one style; they share the steppers: how long a note shows before its beat,
/// the tempo, and whether notes hit themselves or wait for a key.
/// </summary>
public sealed class NoteApproachExperiment : ILabExperiment
{
    private static readonly int[] ApproachSteps = [600, 900, 1200, 1500, 1800, 2400];
    private static readonly int[] TempoSteps = [100, 120, 150, 180,300];

    private readonly NoteApproachStyle _style;
    private int _approachIndex = TriangleBench.StartIndex("--approach=", Array.ConvertAll(ApproachSteps, s => (float)s), 2);
    private int _tempoIndex = TriangleBench.StartIndex("--bpm=", Array.ConvertAll(TempoSteps, s => (float)s), 2);
    private bool _keys = Array.IndexOf(OS.GetCmdlineUserArgs(), "--keys") >= 0;
    private bool _chords = Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-chords") < 0;

    private UiNode _approachLabel, _tempoLabel, _inputLabel, _chordLabel;
    private Entity _approachDown, _approachUp, _tempoDown, _tempoUp, _inputDown, _inputUp, _chordDown, _chordUp;
    private bool _approachDownHeld, _approachUpHeld, _tempoDownHeld, _tempoUpHeld, _inputDownHeld, _inputUpHeld,
        _chordDownHeld, _chordUpHeld;

    public NoteApproachExperiment(NoteApproachStyle style) => _style = style;

    public string Name => _style switch
    {
        NoteApproachStyle.Diva => "DIVA",
        NoteApproachStyle.DivaLine => "DIVA+",
        NoteApproachStyle.Drop => "DROP",
        _ => "RING",
    };

    public string[] Notes => _style switch
    {
        NoteApproachStyle.Diva =>
        [
            "A target pops in on the spot, a clock hand",
            "sweeping round it to the beat. The note flies",
            "in from off to the side on a wave and lands.",
            "KEYS: Space Z X J K, one press per note.",
        ],
        NoteApproachStyle.DivaLine =>
        [
            "Diva targets and flying notes, joined by the",
            "game's guide line in hit order. A spark leaves",
            "each target as it is hit and runs the line,",
            "landing on the next target on its beat.",
            "KEYS: Space Z X J K, one press per note.",
        ],
        NoteApproachStyle.Drop =>
        [
            "A slot opens on the spot with a faint column",
            "above it. The note falls straight down at a",
            "constant speed, like a lane, into the slot.",
            "KEYS: Space Z X J K, one press per note.",
        ],
        _ =>
        [
            "The game's notes today: fade in while the",
            "approach ring shrinks onto them, no glow.",
            "The game shows them 600 ms; this starts at 1200.",
            "KEYS: Space Z X J K, one press per note.",
        ],
    };

    public float ControlsHeight => UiMetrics.Control.Md * 4 + UiTheme.ContainerSpacing * 4;

    public NoteApproachStyle Style => _style;

    /// <summary>How long a note shows before its beat.</summary>
    public float ApproachMs => ApproachSteps[_approachIndex];

    public float BeatMs => 60_000f / TempoSteps[_tempoIndex];

    /// <summary>Whether notes wait for a key instead of hitting themselves on the beat.</summary>
    public bool Keys => _keys;

    /// <summary>Whether the chart writes chords, or keeps one note per beat.</summary>
    public bool Chords => _chords;

    public Node2D CreateStage(Rect2 area) => new NoteApproachStage(area, this);

    public void BuildPanel(UiNode panel)
    {
        _approachLabel = TriangleBench.Stepper(panel, "", out _approachDown, out _approachUp);
        _tempoLabel = TriangleBench.Stepper(panel, "", out _tempoDown, out _tempoUp);
        _inputLabel = TriangleBench.Stepper(panel, "", out _inputDown, out _inputUp);
        _chordLabel = TriangleBench.Stepper(panel, "", out _chordDown, out _chordUp);
        Refresh();
    }

    internal void Poll()
    {
        if (TriangleBench.Pressed(_approachDown, ref _approachDownHeld))
            _approachIndex = Math.Max(0, _approachIndex - 1);
        if (TriangleBench.Pressed(_approachUp, ref _approachUpHeld))
            _approachIndex = Math.Min(ApproachSteps.Length - 1, _approachIndex + 1);
        if (TriangleBench.Pressed(_tempoDown, ref _tempoDownHeld))
            _tempoIndex = Math.Max(0, _tempoIndex - 1);
        if (TriangleBench.Pressed(_tempoUp, ref _tempoUpHeld))
            _tempoIndex = Math.Min(TempoSteps.Length - 1, _tempoIndex + 1);
        if (TriangleBench.Pressed(_inputDown, ref _inputDownHeld) | TriangleBench.Pressed(_inputUp, ref _inputUpHeld))
            _keys = !_keys;
        if (TriangleBench.Pressed(_chordDown, ref _chordDownHeld) | TriangleBench.Pressed(_chordUp, ref _chordUpHeld))
            _chords = !_chords;
        Refresh();
    }

    private void Refresh()
    {
        _approachLabel.SetText($"APPROACH {ApproachMs:0} MS");
        _tempoLabel.SetText($"TEMPO {TempoSteps[_tempoIndex]} BPM");
        _inputLabel.SetText(_keys ? "INPUT KEYS" : "INPUT AUTO");
        _chordLabel.SetText(_chords ? "CHORDS ON" : "CHORDS OFF");
    }
}
