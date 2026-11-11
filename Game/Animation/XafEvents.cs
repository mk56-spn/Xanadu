using System;
using System.Collections.Generic;

namespace Xanadu.Animation;

/// <summary>
/// A named moment in a clip, such as the frame a kick lands or a foot touches
/// down. Playback reports each one it passes, so the game can act on the
/// animation's own timing rather than a guess in code.
/// </summary>
public sealed class XafEvent
{
    public const int MaxNameLength = 64;

    public float Time;
    public string Name = "";
}

/// <summary>An event playback passed this frame, and the clip it is in.</summary>
public readonly record struct XafFiredEvent(string Clip, string Name, float Time);

/// <summary>Adding, moving, renaming and finding events, and which ones a stretch of playback passes.</summary>
public static class XafEventAuthoring
{
    public const string DefaultName = "hit";

    /// <summary>Adds an event at a time, kept in time order, and returns its index; -1 outside the clip.</summary>
    public static int Add(XafClip clip, float time, string name = DefaultName)
    {
        if (!float.IsFinite(time) || time < 0f || time > clip.Duration)
            return -1;

        var added = new XafEvent { Time = time, Name = Clean(name) };
        clip.Events.Add(added);
        Sort(clip);
        return clip.Events.IndexOf(added);
    }

    public static bool Remove(XafClip clip, int index)
    {
        if (index < 0 || index >= clip.Events.Count)
            return false;
        clip.Events.RemoveAt(index);
        return true;
    }

    /// <summary>Moves an event, held inside the clip, and returns where it now sits in the list.</summary>
    public static int Move(XafClip clip, int index, float time)
    {
        if (index < 0 || index >= clip.Events.Count || !float.IsFinite(time))
            return index;

        XafEvent moved = clip.Events[index];
        moved.Time = Math.Clamp(time, 0f, clip.Duration);
        Sort(clip);
        return clip.Events.IndexOf(moved);
    }

    public static bool Rename(XafClip clip, int index, string name)
    {
        string clean = Clean(name);
        if (index < 0 || index >= clip.Events.Count || clean.Length == 0)
            return false;
        clip.Events[index].Name = clean;
        return true;
    }

    /// <summary>
    /// The events playback passes moving from <paramref name="from"/> to
    /// <paramref name="to"/>: those after the first time up to and including
    /// the second. A looping clip that wrapped also passes the end of its loop
    /// and the start again; <paramref name="started"/> counts an event at the
    /// very start, for a clip that has just begun.
    /// </summary>
    public static void Passed(XafClip clip, float from, float to, bool wrapped, bool started, List<XafFiredEvent> fired)
    {
        if (clip.Events.Count == 0)
            return;

        if (!wrapped)
        {
            Collect(clip, from, to, started, fired);
            return;
        }

        Collect(clip, from, clip.LoopEnd, started, fired);
        Collect(clip, clip.LoopStart, to, includeStart: true, fired);
    }

    private static void Collect(XafClip clip, float after, float upTo, bool includeStart, List<XafFiredEvent> fired)
    {
        foreach (XafEvent passed in clip.Events)
        {
            bool inside = includeStart
                ? passed.Time >= after - XafTracks.TimeTolerance
                : passed.Time > after + XafTracks.TimeTolerance;
            if (inside && passed.Time <= upTo + XafTracks.TimeTolerance)
                fired.Add(new XafFiredEvent(clip.Id, passed.Name, passed.Time));
        }
    }

    public static void Validate(XafClip clip, List<string> issues)
    {
        float previous = float.NegativeInfinity;
        foreach (XafEvent item in clip.Events)
        {
            if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > XafEvent.MaxNameLength)
                issues.Add($"Clip '{clip.Id}' has an event with a bad name.");
            if (!(item.Time >= 0f && item.Time <= clip.Duration))
                issues.Add($"Clip '{clip.Id}' has an event outside its duration.");
            if (item.Time < previous)
                issues.Add($"Clip '{clip.Id}' has events out of order.");
            previous = item.Time;
        }
    }

    private static void Sort(XafClip clip) => clip.Events.Sort((a, b) => a.Time.CompareTo(b.Time));

    private static string Clean(string name)
    {
        string trimmed = name.Trim().ToLowerInvariant().Replace(' ', '_');
        return trimmed.Length > XafEvent.MaxNameLength ? trimmed[..XafEvent.MaxNameLength] : trimmed;
    }
}
