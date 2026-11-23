using System;
using System.Linq;
using Xanadu.Core.Keys;

namespace Xanadu.Model.Animation;

/// <summary>Clip-level edits for the animator tool: add, copy, remove, rename, length and looping.</summary>
public static class RigClipAuthoring
{
    public const float MinDuration = 0.1f;
    public const float DefaultDuration = 1f;

    /// <summary><paramref name="baseId"/>, or the first of <c>baseId-2</c>, <c>baseId-3</c>... not yet used.</summary>
    public static string UniqueId(RigPackage package, string baseId)
    {
        string root = string.IsNullOrWhiteSpace(baseId) ? "clip" : baseId.Trim();
        string id = root;
        for (int n = 2; package.Clips.Exists(clip => clip.Id == id); n++)
            id = $"{root}-{n}";
        return id;
    }

    public static RigClip Add(RigPackage package, string baseId = "clip")
    {
        var clip = new RigClip
        {
            Id = UniqueId(package, baseId),
            Duration = DefaultDuration,
        };
        package.Clips.Add(clip);
        return clip;
    }

    /// <summary>A deep copy placed right after its source.</summary>
    public static RigClip Duplicate(RigPackage package, RigClip source)
    {
        RigClip copy = RigPackageCopy.Clone(source);
        copy.Id = UniqueId(package, source.Id);
        int index = package.Clips.IndexOf(source);
        package.Clips.Insert(index < 0 ? package.Clips.Count : index + 1, copy);
        return copy;
    }

    /// <summary>Removes a clip. The last remaining clip cannot be removed.</summary>
    public static bool Delete(RigPackage package, RigClip clip)
    {
        if (package.Clips.Count <= 1)
            return false;

        if (!package.Clips.Remove(clip))
            return false;

        RigTransitions.RemoveClip(package, clip.Id);
        return true;
    }

    /// <summary>Renames a clip to a trimmed, non-empty id no other clip uses.</summary>
    public static bool Rename(RigPackage package, RigClip clip, string id)
    {
        string trimmed = (id ?? "").Trim();
        if (trimmed.Length == 0)
            return false;
        if (trimmed == clip.Id)
            return true;
        if (package.Clips.Exists(other => other != clip && other.Id == trimmed))
            return false;

        RigTransitions.RenameClip(package, clip.Id, trimmed);
        clip.Id = trimmed;
        return true;
    }

    public static float LastKeyTime(RigClip clip)
    {
        float last = 0f;
        foreach (KeyChannel channel in clip.Channels)
        {
            foreach (Keyframe key in channel.Keys)
                last = Math.Max(last, key.Time);
        }
        return last;
    }

    /// <summary>
    /// Sets the length, never shorter than the minimum or than the last key,
    /// so shrinking cannot cut keys off. A loop that ran to the end keeps
    /// running to the new end. Returns the length actually set.
    /// </summary>
    public static float SetDuration(RigClip clip, float seconds)
    {
        float duration = Math.Max(Math.Max(MinDuration, LastKeyTime(clip)), seconds);
        duration = MathF.Round(duration, 3);

        bool loopsToEnd = clip.Loop && Math.Abs(clip.LoopEnd - clip.Duration) < 0.0001f;
        clip.Duration = duration;
        if (clip.Loop)
        {
            if (loopsToEnd || clip.LoopEnd > duration)
                clip.LoopEnd = duration;
            clip.LoopStart = Math.Min(clip.LoopStart, Math.Max(0f, duration - MinDuration));
        }

        return duration;
    }

    /// <summary>Moves the loop's start, keeping at least the minimum length before its end.</summary>
    public static float SetLoopStart(RigClip clip, float seconds)
    {
        float latest = Math.Max(0f, Math.Min(clip.Duration, clip.LoopEnd) - MinDuration);
        clip.LoopStart = Math.Clamp(seconds, 0f, latest);
        return clip.LoopStart;
    }

    /// <summary>Moves the loop's end, keeping at least the minimum length after its start.</summary>
    public static float SetLoopEnd(RigClip clip, float seconds)
    {
        float earliest = Math.Min(clip.Duration, clip.LoopStart + MinDuration);
        clip.LoopEnd = Math.Clamp(seconds, earliest, clip.Duration);
        return clip.LoopEnd;
    }

    /// <summary>Turns looping on or off. Turning it on loops the whole clip unless a range is set.</summary>
    public static void SetLoop(RigClip clip, bool loop)
    {
        clip.Loop = loop;
        if (loop && clip.LoopEnd - clip.LoopStart <= 0.0001f)
        {
            clip.LoopStart = 0f;
            clip.LoopEnd = clip.Duration;
        }
    }
}
