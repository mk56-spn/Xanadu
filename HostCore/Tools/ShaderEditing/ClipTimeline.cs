using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Timeline;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Timeline;
using Xanadu.Slang;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The clips in the code (<see cref="SlangClips"/>) on a shared timeline under
/// the preview: one row each, named after its local, its span drawn as a band
/// shaped by its easing, repeated round its loop, with its keys as diamonds and
/// a staggered clip's later copies fading out after it. It owns the clock the
/// preview's shader reads: it plays round the clips' loop, a press on the ruler
/// scrubs it (and pauses), the button left of the ruler plays and pauses, and a
/// press on a row's name puts the caret on that clip in the code. Presses and
/// drags are read by the shared <see cref="TimelineInputSystem"/>.
/// </summary>
internal sealed class ClipTimeline : ITimelineHost, ITimelineInput, IDisposable
{
    /// <summary>The beats the lab counts for <c>clock.beats</c> until the editor has a song.</summary>
    public const double LabBpm = 120;

    private const float RulerHeight = 22;
    private const float RowHeight = 18;
    private const float LabelWidth = 120;
    private const float BarWidth = 6;

    /// <summary>Rows it always has room for, and the most it grows to before the rest scroll.</summary>
    public const int MinRows = 6, MaxRows = 12;

    /// <summary>Rows shown at once: one per clip, from <see cref="MinRows"/> up to <see cref="MaxRows"/>; more scroll.</summary>
    public int Rows => Math.Clamp(_clips.Count, MinRows, MaxRows);

    /// <summary>The height the timeline wants.</summary>
    public const float MinHeight = RulerHeight + MinRows * RowHeight + 6;

    /// <summary>The height it wants for the clips it has: every one shown, up to <see cref="MaxRows"/>.</summary>
    public float Height => RulerHeight + Rows * RowHeight + 6;

    /// <summary>The longest common loop worked out before the window falls back to the longest loop alone.</summary>
    private const double MostWindow = 16;

    /// <summary>Copies of a staggered clip drawn after the first.</summary>
    private const int StaggerCopies = 4;

    private static readonly Color[] LaneColours =
    [
        Palette.Gold, Palette.Pink, Palette.Cyan, Palette.Lime, Palette.Violet, Palette.Rose,
    ];

    private static readonly Color Back = new("#0e1428");
    private static readonly Color RulerBack = new("#111830");

    private readonly Entity _entity;
    private readonly object _play = new();
    private List<SlangClip> _clips = [];
    private Rect2 _area;
    private int _first;
    private bool _playing = true;
    private double _seconds;

    /// <summary>How long the timeline is before it plays round again, in seconds; 0 when there is nothing on it.</summary>
    private double _window;

    /// <summary>How much of it shows at once: one turn of the longest loop, following the playhead.</summary>
    private double _view;

    /// <summary>Where on the timeline each play key was last pressed (<see cref="ShaderKeys"/>), for the clips that play from it.</summary>
    private readonly double?[] _pressedAt = new double?[ShaderKeys.Count];
    private readonly float[] _since = [.. new float[ShaderKeys.Count].Select(_ => ShaderKeys.Never)];

    /// <summary>The same for each play event (<see cref="ShaderPlay"/>), for the clips that play from one.</summary>
    private readonly double?[] _happenedAt = new double?[ShaderPlay.EventCount];
    private readonly float[] _eventSince = [.. new float[ShaderPlay.EventCount].Select(_ => ShaderKeys.Never)];

    /// <summary>The default key for each of <see cref="SlangClips.PlayKeys"/>, for the rows.</summary>
    private static readonly string[] KeyLabels = ["S", "D", "K", "L", "F", "Space", "J", "a direction key", "an action key"];

    /// <summary>What a clip on a key or event waits for, said on its row until it first plays.</summary>
    private static string WaitingFor(int pressed) =>
        pressed >= SlangClips.FirstEvent
            ? $"plays on each {SlangClips.EventName(pressed - SlangClips.FirstEvent)} ({PlayStandIns.HowToFake(pressed - SlangClips.FirstEvent)})"
            : $"plays when {KeyLabels[pressed]} is pressed (click the preview first)";

    public ClipTimeline(EntityStore entities)
    {
        _entity = entities.CreateEntity(new TimelineView(1f), new TimelineHost(this));
    }

    /// <summary>Whether a press on the ruler moves the shader's clock: false for a preview that keeps its own.</summary>
    public bool Scrubs { get; set; } = true;

    /// <summary>A row's name was pressed: the caret goes to its clip (0-based line and column).</summary>
    public event Action<int, int>? Jump;

    /// <summary>A press on the timeline ended; the code takes its focus back.</summary>
    public event Action? Released;

    /// <summary>The clock the shader reads: seconds and beats.</summary>
    public (float Seconds, float Beats) Clock => ((float)_seconds, (float)(_seconds * LabBpm / 60));

    /// <summary>How far round the timeline the clock is, 0..1; 0 when it doesn't go round.</summary>
    public double Progress => _window > 0 ? _seconds / _window : 0;

    public Rect2 Area
    {
        get => _area;
        set => _area = value;
    }

    /// <summary>The clips from the code as it is now; the window is worked out again from them.</summary>
    public void Use(List<SlangClip> clips)
    {
        _clips = clips;
        _first = Math.Clamp(_first, 0, Math.Max(0, clips.Count - Rows));
        (_window, _view) = Window(clips);
        if (_window > 0)
            _seconds = Wrap(_seconds);
    }

    /// <summary>Moves the clock on while playing; a preview that keeps its own time hands it in instead.</summary>
    public void Advance(float delta, float? ownTime = null)
    {
        double before = _seconds;
        if (ownTime is { } own)
            _seconds = _window > 0 ? Wrap(own) : own;
        else if (_playing)
            _seconds = _window > 0 ? Wrap(_seconds + delta) : _seconds + delta;

        // Going round starts a fresh turn: last turn's presses are not this one's.
        bool wrapped = _seconds < before && (_playing || ownTime is not null);
        for (int key = 0; key < ShaderKeys.Count; key++)
        {
            float since = ShaderKeys.Since(key);
            if (since < _since[key])
                _pressedAt[key] = _seconds - since;
            else if (wrapped)
                _pressedAt[key] = null;
            _since[key] = since;
        }

        for (int happened = 0; happened < ShaderPlay.EventCount; happened++)
        {
            float since = ShaderPlay.Since((ShaderEvent)happened);
            if (since < _eventSince[happened])
                _happenedAt[happened] = _seconds - since;
            else if (wrapped)
                _happenedAt[happened] = null;
            _eventSince[happened] = since;
        }
    }

    /// <summary>Where on the timeline a key clip's key was last pressed; for "any", the latest of its keys.</summary>
    private double? PressedAt(int key)
    {
        if (key >= SlangClips.FirstEvent)
        {
            double? last = null;
            foreach (int part in SlangClips.EventParts(key - SlangClips.FirstEvent))
            {
                if (part < ShaderPlay.EventCount && _happenedAt[part] is { } at && (last is null || at > last))
                    last = at;
            }

            return last;
        }

        if (key < ShaderKeys.Count)
            return _pressedAt[key];
        (int from, int to) = key == 7 ? (0, ShaderKeys.FirstAction) : (ShaderKeys.FirstAction, ShaderKeys.Count);
        double? latest = null;
        for (int k = from; k < to; k++)
        {
            if (_pressedAt[k] is { } at && (latest is null || at > latest))
                latest = at;
        }

        return latest;
    }

    private double Wrap(double seconds) => _window > 0 ? seconds - _window * Math.Floor(seconds / _window) : seconds;

    /// <summary>Seconds one unit of a clip's clock is.</summary>
    private static double Unit(SlangClip clip) => clip.Beats ? 60 / LabBpm : 1;

    /// <summary>
    /// How long to play before going round: the loops' common length when it is
    /// short enough (else the longest loop), and long enough for every clip on a
    /// clock that doesn't loop to finish with a little after. And how much shows
    /// at once: the longest loop, or that finish when it is longer.
    /// </summary>
    private static (double Wrap, double View) Window(List<SlangClip> clips)
    {
        long common = 0;
        double longest = 0, once = 0;
        foreach (SlangClip clip in clips)
        {
            // A clip on a key's press plays whenever the key goes down, not on the song's time.
            if (clip.OnKey)
                continue;
            if (clip.Loop > 0 && double.IsFinite(clip.Loop))
            {
                double period = clip.Loop * Unit(clip);
                longest = Math.Max(longest, period);
                long ms = Math.Max(1, (long)Math.Round(period * 1000));
                common = common == 0 ? ms : Lcm(common, ms);
            }
            else if (clip.Fixed)
                once = Math.Max(once, (clip.End + clip.StaggerStep * StaggerCopies) * Unit(clip) + 0.5);
        }

        double loops = common == 0 ? 0 : common / 1000.0 <= MostWindow ? common / 1000.0 : longest;
        return (Math.Max(loops, once), Math.Max(longest, once));
    }

    private static long Lcm(long a, long b)
    {
        long x = a, y = b;
        while (y != 0)
            (x, y) = (y, x % y);
        long lcm = a / x * b;
        return lcm > MostWindow * 1000 * 4 ? long.MaxValue / 4 : lcm;
    }

    private Rect2 Ruler => new(_area.Position.X + LabelWidth, _area.Position.Y, _area.Size.X - LabelWidth - BarWidth - 4, RulerHeight);

    private Rect2 PlayButton => new(_area.Position.X + 6, _area.Position.Y + 3, LabelWidth - 12, RulerHeight - 6);

    /// <summary>The turn of the view the playhead is in.</summary>
    private TimelineAxis Axis
    {
        get
        {
            double start = _view > 0 ? Math.Floor(_seconds / _view) * _view : 0;
            return new TimelineAxis(Ruler, start * 1000, (start + Math.Max(_view, 0.001)) * 1000);
        }
    }

    private TimelineLanes Lanes()
    {
        var body = new Rect2(Ruler.Position.X, _area.Position.Y + RulerHeight + 2, Ruler.Size.X, Rows * RowHeight);
        var labels = new Rect2(_area.Position.X, body.Position.Y, LabelWidth, body.Size.Y);
        var bar = new Rect2(body.End.X + 2, body.Position.Y, BarWidth, body.Size.Y);
        return new TimelineLanes(body, labels, RowHeight, _clips.Count, _first, Rows, bar, bar.Grow(4), 12);
    }

    // ITimelineHost

    public double NowMs => _seconds * 1000;

    public double DurationMs => _window * 1000;

    public double StepMs => 100;

    public double StepOriginMs => 0;

    public bool Blocked => false;

    public bool TryGetArea(out Rect2 panel, out Rect2 ruler)
    {
        panel = _area;
        ruler = Ruler;
        return _area.Size.X > 0;
    }

    public IEnumerable<double> MarkerTimesMs()
    {
        foreach (SlangClip clip in _clips)
        {
            if (clip.Fixed && !clip.OnKey)
                yield return clip.Start * Unit(clip) * 1000;
        }
    }

    public void SeekMs(double ms) => _seconds = Wrap(ms / 1000);

    public void TogglePlay() => _playing = !_playing;

    public void NoMarker(int direction)
    {
    }

    // ITimelineInput

    public TimelineScene? Scene()
    {
        if (_area.Size.X <= 0)
            return null;

        TimelineAxis axis = Axis;
        var scene = new TimelineScene
        {
            Panel = _area,
            Axis = axis,
            ScrubBand = Scrubs && _window > 0 ? Ruler : default,
            DurationMs = _window * 1000,
            SnapScrub = false,
            Lanes = Lanes(),
        };
        if (Scrubs)
        {
            // The play button: a fixed handle left of the ruler.
            Rect2 button = PlayButton;
            scene.Items.Add(TimelineItem.Marker(_play, 0, axis.TimeAt(button.GetCenter().X), area: button) with
            {
                Reach = float.MaxValue,
                Role = TimelineItemRole.Handle,
            });
        }

        return scene;
    }

    public void GestureStarted(TimelineGestureKind kind)
    {
        if (kind == TimelineGestureKind.Scrub)
            _playing = false;
    }

    public void GestureEnded(TimelineGestureKind kind) => Released?.Invoke();

    public void ScrollRows(int first) => _first = Math.Clamp(first, 0, Math.Max(0, _clips.Count - Rows));

    /// <summary>Scrubbing stays in the turn shown, so the ruler holds still under the pointer.</summary>
    public void Scrub(double ms)
    {
        TimelineAxis axis = Axis;
        _seconds = Wrap(Math.Clamp(ms, axis.StartMs, axis.EndMs - 1) / 1000);
    }

    public void LabelPressed(int lane, Vector2 point)
    {
        if (lane >= 0 && lane < _clips.Count)
            Jump?.Invoke(_clips[lane].Line, _clips[lane].Column);
    }

    public void LanePressed(int lane, double ms, bool additive)
    {
    }

    public void ItemPressed(in TimelineItem item, TimelinePick pick, TimelineGrabPart part)
    {
        if (item.Ref == _play)
            _playing = !_playing;
    }

    public void ItemMoved(in TimelineItem item, double fromMs, double toMs)
    {
    }

    public void ItemReleased(in TimelineItem item, bool remove)
    {
    }

    public void BoxSelected(IReadOnlyList<object> selection)
    {
    }

    // Drawing

    public void Draw(IRectCanvas canvas, Handle item)
    {
        if (_area.Size.X <= 0)
            return;

        var painter = new TimelinePainter(canvas, item);
        canvas.DrawRect(item, _area, Back);
        Rect2 ruler = Ruler;
        canvas.DrawRect(item, new Rect2(_area.Position, new Vector2(_area.Size.X, RulerHeight)), RulerBack);

        if (Scrubs)
        {
            Rect2 button = PlayButton;
            canvas.DrawRect(item, button, _playing ? Palette.PrimaryMuted : Palette.Primary);
            canvas.DrawTextStyled(item, button, _playing ? "pause" : "play", Palette.TextBright, 12);
        }
        else
        {
            canvas.DrawTextStyledLeft(item, new Rect2(_area.Position.X + 8, _area.Position.Y, LabelWidth, RulerHeight), "CLIPS", Palette.TextDim, 12);
        }

        if (_clips.Count == 0)
        {
            canvas.DrawTextStyledLeft(item, new Rect2(ruler.Position.X + 8, _area.Position.Y + RulerHeight + 6, ruler.Size.X, RowHeight),
                "No clips in the code yet. Make one: Clip pop = clock.looped(2).clip(0.2, 0.6);", Palette.TextMuted, 13);
            return;
        }

        TimelineAxis axis = Axis;
        List<TimelineTick> ticks = TimelineTicks.Seconds(axis, 56, 10, "s");
        painter.RulerTicks(axis, ruler, ticks, TimelineRulerLook.TwoLevel(
            new TimelineTickLook(10, Palette.BorderStrong), new TimelineTickLook(4, Palette.BorderSubtle)));
        painter.Labels(axis, ruler, ticks, Palette.TextMuted);

        TimelineLanes lanes = Lanes();
        painter.GridLines(axis, lanes.Body.Position.Y, lanes.Body.End.Y, ticks, Palette.GridLine);
        for (int lane = lanes.First; lane < Math.Min(_clips.Count, lanes.First + Rows); lane++)
        {
            SlangClip clip = _clips[lane];
            Color colour = LaneColours[lane % LaneColours.Length];
            Rect2 row = lanes.Row(lane);
            row = new Rect2(row.Position.X, row.Position.Y - lanes.ScrollY, row.Size.X, row.Size.Y);
            var label = new Rect2(_area.Position.X + 8, row.Position.Y, LabelWidth - 12, RowHeight);
            canvas.DrawTextMono(item, label, clip.Fixed ? clip.Name : clip.Name + " ?", clip.Fixed ? colour : Palette.TextDim, 12, TextAlign.Left);
            if (!clip.Fixed)
            {
                canvas.DrawTextStyledLeft(item, new Rect2(row.Position.X + 6, row.Position.Y, row.Size.X, RowHeight),
                    "not fixed in the code, so not drawn", Palette.TextDim, 12);
                continue;
            }

            if (!clip.OnKey)
            {
                DrawClip(painter, canvas, item, axis, row, clip, colour, 0);
                continue;
            }

            // A key clip: from the key's last press here, or a note of which key it waits for.
            if (PressedAt(clip.Pressed) is { } pressed && axis.Shows(pressed * 1000, 1e9))
            {
                float x = Math.Clamp(axis.X(pressed * 1000), axis.Left, axis.Right);
                canvas.DrawRect(item, new Rect2(x - 1, row.Position.Y, 2, row.Size.Y), Palette.TextBright);
                DrawClip(painter, canvas, item, axis, row, clip, colour, pressed * 1000);
            }
            else
            {
                canvas.DrawTextStyledLeft(item, new Rect2(row.Position.X + 6, row.Position.Y, row.Size.X, RowHeight),
                    WaitingFor(clip.Pressed), Palette.TextDim, 12);
            }
        }

        if (lanes.HasBar)
        {
            VerticalSlider slider = lanes.Slider;
            canvas.DrawRect(item, lanes.Bar, Palette.SurfaceInset);
            canvas.DrawRect(item, new Rect2(lanes.Bar.Position.X, slider.ThumbTop(lanes.Value), lanes.Bar.Size.X, slider.ThumbLength), Palette.BorderStrong);
        }

        painter.Playhead(axis, NowMs, ruler.Position.Y + 4, lanes.Body.End.Y, Palette.TextBright);
    }

    /// <summary>One clip's band on its row: at each turn of its loop in the window, then its staggered copies fading.</summary>
    /// <param name="originMs">Where its clock reads 0 on the timeline: 0, or a key's press.</param>
    private void DrawClip(TimelinePainter painter, IRectCanvas canvas, Handle item, TimelineAxis axis, Rect2 row, SlangClip clip, Color colour, double originMs)
    {
        double unit = Unit(clip) * 1000;
        double period = clip.Loop > 0 && double.IsFinite(clip.Loop) ? clip.Loop * unit : 0;
        double reach = (clip.Span + clip.StaggerStep * StaggerCopiesOf(clip)) * unit;
        int firstTurn = period > 0 ? (int)Math.Floor((axis.StartMs - originMs - clip.Start * unit - reach) / period) : 0;
        int lastTurn = period > 0 ? (int)Math.Ceiling((axis.EndMs - originMs) / period) : 0;
        // A key's clock starts at its press: nothing before it.
        if (clip.OnKey)
            firstTurn = Math.Max(0, firstTurn);
        Rect2 band = new(row.Position.X, row.Position.Y + 2, row.Size.X, row.Size.Y - 4);
        for (int copy = StaggerCopiesOf(clip); copy >= 0; copy--)
        {
            Color shade = colour with { A = copy == 0 ? 0.85f : 0.45f / copy };
            double shift = copy * clip.StaggerStep * unit;
            for (int turn = firstTurn; turn <= lastTurn; turn++)
            {
                double from = originMs + clip.Start * unit + shift + turn * period;
                double to = from + clip.Span * unit;
                painter.Span(axis, band, from, to, clip.ProgressAt, shade);
                if (copy != 0)
                    continue;

                // Its start, and each key where the clip's progress reaches it.
                canvas.DrawRect(item, new Rect2(Math.Clamp(axis.X(from), axis.Left, axis.Right), band.Position.Y, 2, band.Size.Y), colour);
                foreach (double key in clip.Keys)
                {
                    double at = from + KeyFraction(clip, key) * clip.Length * unit;
                    if (axis.Shows(at))
                        painter.Key(new Vector2(axis.X(at), band.GetCenter().Y), 4, Palette.TextBright, EasePreset.Linear);
                }
            }
        }
    }

    private static int StaggerCopiesOf(SlangClip clip) => clip.StaggerStep > 0 ? StaggerCopies : 0;

    /// <summary>The fraction of one pass at which the clip's eased progress first reaches <paramref name="key"/>.</summary>
    private static double KeyFraction(SlangClip clip, double key)
    {
        const int Steps = 200;
        float previous = SlangClips.Ease(clip.Easing, clip.Reversed ? 1 : 0);
        for (int i = 1; i <= Steps; i++)
        {
            float t = (float)i / Steps;
            float now = SlangClips.Ease(clip.Easing, clip.Reversed ? 1 - t : t);
            if ((previous - key) * (now - key) <= 0)
                return t;
            previous = now;
        }

        return 1;
    }

    public void Dispose()
    {
        if (!_entity.IsNull)
            _entity.DeleteEntity();
    }
}
