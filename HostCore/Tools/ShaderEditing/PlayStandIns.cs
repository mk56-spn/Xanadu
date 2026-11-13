using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xanadu.Ecs;
using Xanadu.Rendering;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The run a shader sees (<see cref="ShaderPlay"/>, <c>xanadu.play</c>) while
/// it is edited, with no game behind it: the pointer on the preview is the
/// player, the play keys fake what happens (F a hit, Space a FLAWLESS+, J a
/// miss, S D K L a dash that way), the timeline is the song with a note on
/// every beat (a <see cref="ShaderChart"/> zig-zagging across the preview; a
/// faked hit or miss judges the nearest one), and what can't be faked that way
/// (standing, dead, the ground mode, Contact's bars) is a slider in the values
/// column, shown once the code reads it. The only writer of
/// <see cref="ShaderPlay"/> and <see cref="ShaderChart"/> in the tools.
/// </summary>
internal sealed class PlayStandIns
{
    /// <summary>A slider the values column shows for a play value the code reads.</summary>
    public readonly record struct Slider(string Name, int Channels, float Min, float Max, Vector4 Default, Regex Read);

    private static readonly Slider[] Sliders =
    [
        new("player.grounded", 1, 0, 1, new Vector4(1, 0, 0, 0), new Regex(@"\bplayer\s*\.\s*(grounded|inAir)\b")),
        new("player.holding", 1, 0, 1, Vector4.Zero, new Regex(@"\bplayer\s*\.\s*(holding|skidding)\b")),
        new("player.dead", 1, 0, 1, Vector4.Zero, new Regex(@"\bplayer\s*\.\s*dead\b")),
        new("contact.bars", 3, 0, 5, new Vector4(2, 4, 5, 0), new Regex(@"\bcontact\s*\.")),
    ];

    /// <summary>How long a faked dash counts as dashing.</summary>
    private const float DashSeconds = 0.3f;

    private static readonly Vector2[] Directions = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1)];

    private int _hits, _misses, _combo, _maxCombo;
    private Vector2? _last;
    private Vector2 _velocity;
    private double _lastProgress;
    private float _seconds, _beats;
    private Vector2 _chartSize;

    /// <summary>How many notes the fake chart has: one a beat.</summary>
    private const int ChartBeats = 64;

    /// <summary>How the lab fakes an event, for the timeline's rows that wait on one.</summary>
    public static string HowToFake(int happened) => happened switch
    {
        0 => "F or Space in the preview",
        1 or 9 => "J in the preview",
        8 => "F, Space or J in the preview",
        2 => "Space in the preview",
        3 => "F in the preview",
        10 or 100 => "S D K L in the preview",
        12 => "the timeline going round",
        _ => "not faked in the editor",
    };

    /// <summary>The sliders the code needs: one per play value it reads that the pointer and keys can't stand in for.</summary>
    public static List<Slider> Needed(string code)
    {
        var needed = new List<Slider>();
        foreach (Slider slider in Sliders)
        {
            if (slider.Read.IsMatch(code))
                needed.Add(slider);
        }

        return needed;
    }

    /// <summary>A play key went down while the preview has the keys (numbered as <see cref="ShaderKeys"/>).</summary>
    public void Pressed(int key)
    {
        if (key < ShaderKeys.FirstAction)
        {
            ShaderPlay.Mark(ShaderEvent.Dash);
            ShaderPlay.LastMove = Directions[key];
            return;
        }

        ShaderPlay.Mark(ShaderEvent.Judged);
        JudgeNearest(key == ShaderKeys.FirstAction + 2 ? 7 : key == ShaderKeys.FirstAction + 1 ? 1 : 2);
        if (key == ShaderKeys.FirstAction + 2)
        {
            _misses++;
            if (_combo > 0)
                ShaderPlay.Mark(ShaderEvent.ComboBroken);
            _combo = 0;
            ShaderPlay.Mark(ShaderEvent.Miss);
            ShaderPlay.LastJudgement = 7;
            return;
        }

        bool plus = key == ShaderKeys.FirstAction + 1;
        _hits++;
        _combo++;
        _maxCombo = Math.Max(_maxCombo, _combo);
        ShaderPlay.Mark(ShaderEvent.Hit);
        ShaderPlay.Mark(plus ? ShaderEvent.FlawlessPlus : ShaderEvent.Flawless);
        ShaderPlay.LastJudgement = plus ? 1 : 2;
    }

    /// <summary>Every frame: the player from the pointer, the song from the timeline, the rest from the sliders.</summary>
    /// <param name="progress">How far round the timeline is, 0..1.</param>
    public void Update(float delta, Vector2 pointer, Rect2 preview, double progress, float seconds, float beats, EditorTuning tuning)
    {
        // The pointer's speed, eased so a still frame between moves doesn't drop it to 0.
        if (_last is { } last && delta > 0)
            _velocity = Vector2.Lerp(_velocity, (pointer - last) / delta, Math.Min(1f, delta * 12f));
        _last = pointer;

        if (progress < _lastProgress - 0.5)
            ShaderPlay.Mark(ShaderEvent.SongStart);
        // A fresh chart each time round, and when the preview changes size.
        if (progress < _lastProgress - 0.5 || preview.Size != _chartSize)
            LoadChart(preview.Size);
        _lastProgress = progress;
        _seconds = seconds;
        _beats = beats;

        ShaderPlay.Position = pointer;
        ShaderPlay.Velocity = _velocity;
        ShaderPlay.PlayfieldCorner = preview.Position;
        ShaderPlay.PlayfieldSize = preview.Size;
        ShaderPlay.PixelsPerUnit = 1;
        ShaderPlay.Dashing = ShaderPlay.Since(ShaderEvent.Dash) < DashSeconds;
        ShaderPlay.Grounded = tuning.StandIn("player.grounded", 1).X > 0.5f;
        ShaderPlay.GroundHold = tuning.StandIn("player.holding", 0).X > 0.5f;
        ShaderPlay.Dead = tuning.StandIn("player.dead", 0).X > 0.5f;
        Vector4 bars = tuning.StandIn("contact.bars", 0);
        ShaderPlay.ContactBars = new Vector3(MathF.Round(bars.X), MathF.Round(bars.Y), MathF.Round(bars.Z));

        int judged = _hits + _misses;
        ShaderPlay.Accuracy = judged == 0 ? 1 : _hits / (float)judged;
        ShaderPlay.Combo = _combo;
        ShaderPlay.MaxCombo = _maxCombo;
        ShaderPlay.Score = Math.Min(1_000_000, _hits * 10_000);
        ShaderPlay.SongProgress = (float)Math.Clamp(progress, 0, 1);
        ShaderPlay.SongSeconds = seconds;
        ShaderPlay.Beat = beats;
        ShaderPlay.Bpm = (float)ClipTimeline.LabBpm;
        // A note on every beat.
        ShaderPlay.UntilNote = (MathF.Ceiling(beats) - beats) * 60f / (float)ClipTimeline.LabBpm;
    }

    /// <summary>A note on every beat, zig-zagging across the preview: its units are the preview's pixels.</summary>
    private void LoadChart(Vector2 size)
    {
        _chartSize = size;
        float secondsPerBeat = 60f / (float)ClipTimeline.LabBpm;
        var chart = new ChartNote[ChartBeats];
        for (int i = 0; i < chart.Length; i++)
        {
            float across = (i % 8 + 0.5f) / 8f;
            float down = i % 2 == 0 ? 0.35f : 0.65f;
            Vector4 colour = (i % 3) switch
            {
                0 => new Vector4(1f, 0.35f, 0.55f, 1f),
                1 => new Vector4(1f, 0.8f, 0.3f, 1f),
                _ => new Vector4(0.45f, 0.85f, 0.4f, 1f),
            };
            chart[i] = new ChartNote
            {
                Place = new Vector4(size.X * across, size.Y * down, 0, 0),
                Colour = colour,
                Timing = new Vector4(i * secondsPerBeat, 0, i % 3, i),
            };
        }

        ShaderChart.Load(chart);
    }

    /// <summary>A faked hit or miss judges the note on the nearest beat, if it still waits.</summary>
    private void JudgeNearest(float judgement)
    {
        int index = (int)MathF.Round(_beats);
        if (index < 0 || index >= ShaderChart.Count || ShaderChart.Notes[index].Result.W != ShaderChart.Waiting)
            return;

        ChartNote note = ShaderChart.Notes[index];
        bool missed = judgement == 7;
        ShaderChart.Judge(
            index, _seconds, judgement, (_seconds - note.Timing.X) * 1000f,
            missed ? ShaderChart.Missed : ShaderChart.Hit, new Vector2(note.Place.X, note.Place.Y));
    }
}
