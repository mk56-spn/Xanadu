using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xanadu.Core.Maths;
using Xanadu.Core.Rendering;

namespace Xanadu.Core.Tweens;

/// <summary>A thing tweens play on: a range in a <see cref="TweenPool"/>. <see cref="None"/> is no target.</summary>
public readonly record struct TweenTarget(int Index)
{
    public static TweenTarget None => new(-1);

    public bool IsNone => Index < 0;
}

/// <summary>
/// Every target's tweens, in one flat array the GPU reads as it is: a target is
/// a range in it (<c>tweenTargets</c>), its tweens sorted by priority then start,
/// which is the order colours fold in. The CPU samples a target here
/// (<see cref="Pose"/>, <see cref="Colour"/>); the GPU samples the same bytes
/// by the same rules (<c>xanadu/tween.slang</c>), so whichever side needs the
/// answer works it out, and nothing is stepped frame by frame.
/// <para>
/// Movement stacks: any number of offsets, turns and scales may overlap, and
/// all of them apply. Colour layers by priority. Named layers (<see cref="Tween.Key"/>)
/// are faded or replaced by name; unnamed ones only ever add.
/// </para>
/// </summary>
public sealed class TweenPool
{
    /// <summary>The pool everything on screen shares, published to shaders as <c>tweens</c>.</summary>
    public static TweenPool Shared { get; } = new(publish: true);

    private const int FirstCapacity = 4;

    private Tween[] _tweens = new Tween[256];
    private int _end;
    private Slot[] _slots = new Slot[64];
    private int _slotCount;
    private readonly Stack<int> _freeSlots = new();
    private readonly Dictionary<int, Stack<int>> _freeRanges = new();
    private int _published = -1;

    private struct Slot
    {
        public int Start, Count, Capacity;
        public bool Live;
    }

    /// <summary>One target as the GPU reads it: <c>TweenRange</c> in xanadu/tween.slang.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct GpuRange
    {
        public uint Start, Count, Pad0, Pad1;
    }

    public TweenPool() : this(publish: false)
    {
    }

    private TweenPool(bool publish)
    {
        if (Unsafe.SizeOf<Tween>() != Tween.Size)
            throw new InvalidOperationException($"Tween is {Unsafe.SizeOf<Tween>()} bytes, not {Tween.Size}.");
        if (publish)
            Publish();
    }

    /// <summary>Moves on every change, so a reader (the GPU upload) knows to copy.</summary>
    private int Version { get; set; }

    public TweenTarget Create()
    {
        int index = _freeSlots.Count > 0 ? _freeSlots.Pop() : _slotCount++;
        if (index >= _slots.Length)
            Array.Resize(ref _slots, _slots.Length * 2);
        _slots[index] = new Slot { Start = Take(FirstCapacity), Capacity = FirstCapacity, Live = true };
        Version++;
        return new TweenTarget(index);
    }

    public void Release(TweenTarget target)
    {
        if (!IsLive(target))
            return;
        ref Slot slot = ref _slots[target.Index];
        Give(slot.Start, slot.Capacity);
        slot = default;
        _freeSlots.Push(target.Index);
        Version++;
    }

    private bool IsLive(TweenTarget target) =>
        target.Index >= 0 && target.Index < _slotCount && _slots[target.Index].Live;

    /// <summary>The target's tweens, by priority then start.</summary>
    public ReadOnlySpan<Tween> Of(TweenTarget target)
    {
        if (!IsLive(target))
            return ReadOnlySpan<Tween>.Empty;
        Slot slot = _slots[target.Index];
        return _tweens.AsSpan(slot.Start, slot.Count);
    }

    /// <summary>
    /// Adds a tween. A named one replaces the target's layer of that name; an
    /// unnamed one stacks on whatever is already playing.
    /// </summary>
    public void Play(TweenTarget target, in Tween tween)
    {
        if (!IsLive(target))
            return;
        if (tween.Key != 0)
            RemoveWhere(target, tween.Key);
        Prune(target);
        Insert(target, tween);
    }

    /// <summary>
    /// Takes the named layer <paramref name="layer"/>.Key from the weight it is at
    /// now to <paramref name="weight"/>, starting now, so turning round mid-way
    /// never jumps. The length of <paramref name="layer"/>'s clip is the time for
    /// a whole 0 → 1; a shorter way takes its share of it. Its value, clock, ease,
    /// shape and priority come from <paramref name="layer"/>. Already heading there:
    /// nothing changes.
    /// </summary>
    public void Fade(TweenTarget target, in Tween layer, float weight)
    {
        if (!IsLive(target) || layer.Key == 0)
            return;

        float now = layer.Clip.Now;
        float from = 0f;
        foreach (Tween playing in Of(target))
        {
            if (playing.Key != layer.Key)
                continue;
            if (playing.Settled == weight && playing.Loop == TweenLoop.None && playing.Value == layer.Value)
                return;
            from = playing.Weight(now);
        }

        if (from == weight && weight == 0f)
        {
            RemoveWhere(target, layer.Key);
            return;
        }

        Play(target, layer with
        {
            Clip = layer.Clip with { Start = now, Length = layer.Clip.Length * Math.Abs(weight - from) },
            WeightFrom = from,
            WeightTo = weight,
            Loop = TweenLoop.None,
        });
    }

    /// <summary>The weight the named layer applies now; 0 when it isn't playing.</summary>
    public float Weight(TweenTarget target, ushort key)
    {
        float weight = 0f;
        foreach (Tween playing in Of(target))
        {
            if (playing.Key == key)
                weight = playing.Weight();
        }
        return weight;
    }

    /// <summary>Where the named layer is heading: its end weight, or null when it isn't playing.</summary>
    public float? Heading(TweenTarget target, ushort key)
    {
        float? heading = null;
        foreach (Tween playing in Of(target))
        {
            if (playing.Key == key)
                heading = playing.Settled;
        }
        return heading;
    }

    /// <summary>
    /// Holds the named layer at <paramref name="value"/>, at full weight, from now
    /// on: for a pose worked out from something else each frame (a row's place
    /// along a carousel). Nothing changes when it already holds that value, and a
    /// zero value drops the layer.
    /// </summary>
    public void Hold(TweenTarget target, ushort key, TweenChannel channel, Vector4 value)
    {
        if (!IsLive(target) || key == 0)
            return;
        bool none = channel == TweenChannel.Scale ? value == new Vector4(1f, 1f, 1f, 1f) : value == Vector4.Zero;
        foreach (Tween playing in Of(target))
        {
            if (playing.Key == key && playing.Channel == channel && playing.Clip.Length == 0f &&
                playing.WeightTo == 1f && playing.Value == value)
            {
                return;
            }
        }

        if (none && channel != TweenChannel.Colour)
        {
            RemoveWhere(target, key);
            return;
        }

        Play(target, Tween.Of(channel, value, Clip.Seconds(float.NegativeInfinity, 0f), 1f, 1f).Named(key));
    }

    /// <summary>Changes what the named layer applies, keeping its timing: a hover colour that changed while shown.</summary>
    public void SetValue(TweenTarget target, ushort key, Vector4 value)
    {
        if (!IsLive(target))
            return;
        Slot slot = _slots[target.Index];
        for (int i = slot.Start; i < slot.Start + slot.Count; i++)
        {
            if (_tweens[i].Key == key && _tweens[i].Value != value)
            {
                _tweens[i].Value = value;
                Version++;
            }
        }
    }

    /// <summary>Drops the named layer at once.</summary>
    public void Stop(TweenTarget target, ushort key)
    {
        if (IsLive(target))
            RemoveWhere(target, key);
    }

    /// <summary>Drops every tween of the target at once.</summary>
    public void Clear(TweenTarget target)
    {
        if (!IsLive(target) || _slots[target.Index].Count == 0)
            return;
        _slots[target.Index].Count = 0;
        Version++;
    }

    /// <summary>The target's movement now: offsets and turns summed, scales and opacities multiplied.</summary>
    public TweenPose Pose(TweenTarget target)
    {
        Vector2 offset = Vector2.Zero, scale = Vector2.One;
        float rotation = 0f, opacity = 1f;
        foreach (Tween tween in Of(target))
        {
            if (tween.Channel is TweenChannel.Colour or TweenChannel.Number)
                continue;
            float w = tween.Weight();
            switch (tween.Channel)
            {
                case TweenChannel.Offset:
                    offset += new Vector2(tween.Value.X, tween.Value.Y) * w;
                    break;
                case TweenChannel.Rotation:
                    rotation += tween.Value.X * w;
                    break;
                case TweenChannel.Scale:
                    scale *= Vector2.One + (new Vector2(tween.Value.X, tween.Value.Y) - Vector2.One) * w;
                    break;
                case TweenChannel.Opacity:
                    opacity *= 1f + (tween.Value.X - 1f) * w;
                    break;
            }
        }
        return new TweenPose(offset, rotation, scale, opacity);
    }

    /// <summary>
    /// The target's colour now over <paramref name="under"/>: each live colour
    /// tween, lowest priority first (then earliest), fades what is beneath it
    /// towards its own colour by its weight.
    /// </summary>
    public Color Colour(TweenTarget target, Color under)
    {
        Vector4 c = new(under.R, under.G, under.B, under.A);
        foreach (Tween tween in Of(target))
        {
            if (tween.Channel != TweenChannel.Colour)
                continue;
            c = Vector4.Lerp(c, tween.Value, tween.Weight());
        }
        return new Color(c.X, c.Y, c.Z, c.W);
    }

    /// <summary>
    /// Drops what can never show again, on the seconds clock only (it never runs
    /// back): runs that ended at weight 0, and colours under a finished full
    /// colour. Beat-clock tweens follow the music, which scrubs, so they always stay.
    /// </summary>
    private void Prune(TweenTarget target)
    {
        if (!IsLive(target))
            return;
        float now = ClipClocks.Seconds;
        ref Slot slot = ref _slots[target.Index];
        Span<Tween> tweens = _tweens.AsSpan(slot.Start, slot.Count);

        int coveredBelow = -1;
        for (int i = tweens.Length - 1; i >= 0; i--)
        {
            Tween t = tweens[i];
            if (t.Channel == TweenChannel.Colour && Done(t, now) && t.Settled == 1f)
            {
                coveredBelow = i;
                break;
            }
        }

        int kept = 0;
        for (int i = 0; i < tweens.Length; i++)
        {
            Tween t = tweens[i];
            // A number's weight is its value, so 0 is still something.
            bool gone = (Done(t, now) && t.Settled == 0f && t.Channel != TweenChannel.Number) ||
                        (i < coveredBelow && t.Channel == TweenChannel.Colour);
            if (!gone)
                tweens[kept++] = t;
        }

        if (kept != slot.Count)
        {
            slot.Count = kept;
            Version++;
        }

        static bool Done(in Tween t, float now) =>
            t.Clip.Clock == ClipClock.Seconds && t.SettledAt(now);
    }

    private void RemoveWhere(TweenTarget target, ushort key)
    {
        ref Slot slot = ref _slots[target.Index];
        int kept = 0;
        for (int i = slot.Start; i < slot.Start + slot.Count; i++)
        {
            if (_tweens[i].Key != key)
                _tweens[slot.Start + kept++] = _tweens[i];
        }
        if (kept != slot.Count)
        {
            slot.Count = kept;
            Version++;
        }
    }

    private void Insert(TweenTarget target, in Tween tween)
    {
        ref Slot slot = ref _slots[target.Index];
        if (slot.Count == slot.Capacity)
        {
            int capacity = slot.Capacity * 2;
            int start = Take(capacity);
            Array.Copy(_tweens, slot.Start, _tweens, start, slot.Count);
            Give(slot.Start, slot.Capacity);
            slot.Start = start;
            slot.Capacity = capacity;
        }

        // After everything it doesn't outrank: priority, then start, then arrival.
        int at = slot.Count;
        while (at > 0)
        {
            Tween before = _tweens[slot.Start + at - 1];
            if (before.Priority < tween.Priority || (before.Priority == tween.Priority && before.Clip.Start <= tween.Clip.Start))
                break;
            _tweens[slot.Start + at] = before;
            at--;
        }

        _tweens[slot.Start + at] = tween;
        slot.Count++;
        Version++;
    }

    private int Take(int capacity)
    {
        if (_freeRanges.TryGetValue(capacity, out Stack<int>? free) && free.Count > 0)
            return free.Pop();
        int start = _end;
        _end += capacity;
        if (_end > _tweens.Length)
            Array.Resize(ref _tweens, Math.Max(_end, _tweens.Length * 2));
        return start;
    }

    private void Give(int start, int capacity)
    {
        if (!_freeRanges.TryGetValue(capacity, out Stack<int>? free))
            _freeRanges[capacity] = free = new Stack<int>();
        free.Push(start);
    }

    /// <summary>
    /// Declares this pool's shader globals (xanadu/tween.slang): the tweens and the
    /// targets' ranges. No clocks: the shader reads its own (<c>clock</c>, <c>clock.beats</c>).
    /// </summary>
    private void Publish()
    {
        ShaderGlobals.Declare("tweens", Tween.Size, global =>
        {
            if (_published == Version)
                return;
            global.Write(_end == 0 ? new Tween[1] : _tweens.AsSpan(0, _end));
            _published = Version;
            WriteRanges();
        });
        ShaderGlobals.Declare("tweenTargets", 16, _ => { });
    }

    private void WriteRanges()
    {
        ShaderGlobal? ranges = ShaderGlobals.Find("tweenTargets");
        if (ranges is null)
            return;
        var gpu = new GpuRange[Math.Max(1, _slotCount)];
        for (int i = 0; i < _slotCount; i++)
            gpu[i] = new GpuRange { Start = (uint)_slots[i].Start, Count = (uint)_slots[i].Count };
        ranges.Write<GpuRange>(gpu);
    }
}
