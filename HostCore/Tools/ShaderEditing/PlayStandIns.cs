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
/// every beat, and what can't be faked that way (standing, dead, the ground
/// mode, Contact's bars) is a slider in the values column, shown once the code
/// reads it. The only writer of <see cref="ShaderPlay"/> in the tools.
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

    /// <summary>How the lab fakes an event, for the timeline's rows that wait on one.</summary>
    public static string HowToFake(int happened) => happened switch
    {
        0 => "F or Space in the preview",
        1 or 8 or 9 => "J in the preview",
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
        _lastProgress = progress;

        ShaderPlay.Position = pointer;
        ShaderPlay.Velocity = _velocity;
        ShaderPlay.PlayfieldCorner = preview.Position;
        ShaderPlay.PlayfieldSize = preview.Size;
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
}
