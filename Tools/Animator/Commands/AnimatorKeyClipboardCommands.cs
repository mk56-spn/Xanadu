using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Copying, pasting and selecting all key sets. The clipboard keeps channel
/// values and curves, so a paste works at another time and in another clip.
/// </summary>
public static class AnimatorKeyClipboardCommands
{
    /// <summary>Copies every key set in the selection, relative to its earliest time.</summary>
    public static void Copy(Entity session)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        XafClip? clip = CurrentClip(session);
        List<AnimatorKeyRef> keys = selection.Keys();
        if (clip is null || keys.Count == 0)
        {
            AnimatorSession.SetStatus(session, "SELECT KEYS TO COPY");
            return;
        }

        float anchor = float.MaxValue;
        foreach (AnimatorKeyRef key in keys)
            anchor = Math.Min(anchor, key.Time);

        var samples = new List<AnimatorKeySample>();
        foreach (AnimatorKeyRef key in keys)
            Collect(clip, key, anchor, samples);

        if (samples.Count == 0)
        {
            AnimatorSession.SetStatus(session, "NOTHING TO COPY");
            return;
        }

        ref AnimatorKeyClipboard clipboard = ref session.GetComponent<AnimatorKeyClipboard>();
        clipboard.Keys = samples;
        clipboard.Refs = keys;
        clipboard.Anchor = anchor;
        clipboard.Length = 0f;
        foreach (AnimatorKeySample sample in samples)
            clipboard.Length = Math.Max(clipboard.Length, sample.Offset);

        AnimatorSession.SetStatus(session, $"COPIED {keys.Count} KEYS");
    }

    /// <summary>Pastes the copied key sets at the playhead, moved back if they would overrun the clip.</summary>
    public static void Paste(Entity session)
    {
        AnimatorKeyClipboard clipboard = session.GetComponent<AnimatorKeyClipboard>();
        XafClip? clip = CurrentClip(session);
        if (clip is null || clipboard.Keys is not { Count: > 0 } samples)
        {
            AnimatorSession.SetStatus(session, "COPY KEYS FIRST");
            return;
        }

        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        float start = Math.Clamp(
            runtime.CurrentTime,
            0f,
            Math.Max(0f, clip.Duration - clipboard.Length));
        var pasted = new HashSet<(XafChannelTarget Target, string Id, XafChannelProperty? Property, float Time)>();

        AnimatorHistoryCommands.Edit(session, () =>
        {
            foreach (AnimatorKeySample sample in samples)
            {
                if (!TargetExists(session, sample))
                    continue;

                float time = start + sample.Offset;
                if (time > clip.Duration + XafTracks.TimeTolerance)
                    continue;

                XafAuthoring.SetKeyCurve(
                    clip,
                    sample.Target,
                    sample.Id,
                    sample.Property,
                    time,
                    sample.Value,
                    sample.Interpolation,
                    sample.TangentIn,
                    sample.TangentOut);
                AnimatorKeyRef? source = SourceOf(clipboard, sample);
                pasted.Add((
                    sample.Target,
                    sample.Id,
                    source?.Property,
                    time));
            }
        });

        if (pasted.Count == 0)
        {
            AnimatorSession.SetStatus(session, "NOTHING PASTED");
            return;
        }

        var refs = new List<AnimatorKeyRef>();
        foreach ((XafChannelTarget target, string id, XafChannelProperty? property, float time) in pasted)
            refs.Add(new AnimatorKeyRef(target, id, time, property));
        refs.Sort((a, b) => a.Time.CompareTo(b.Time));
        AnimatorKeyCommands.SelectKeys(session, refs);
        AnimatorSession.SetStatus(session, $"PASTED {refs.Count} KEYS AT {start:0.00}s");
    }

    /// <summary>Selects every key set of the current clip, so bulk moves and copies have a start.</summary>
    public static void SelectAll(Entity session)
    {
        XafClip? clip = CurrentClip(session);
        if (clip is null)
            return;

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        IReadOnlySet<XafTrack> expanded =
            session.GetComponent<AnimatorTracks>().Expanded ?? new HashSet<XafTrack>();
        var keys = new List<AnimatorKeyRef>();
        foreach (AnimatorTrackRow row in AnimatorTrackRows.Build(
                     document.Package, clip, null, expanded))
        {
            foreach (float time in AnimatorTrackRows.KeyTimes(clip, row))
                keys.Add(AnimatorTrackRows.Ref(row, time));
        }

        if (keys.Count == 0)
        {
            AnimatorSession.SetStatus(session, "NO KEYS TO SELECT");
            return;
        }

        keys.Sort((a, b) => a.Time.CompareTo(b.Time));
        AnimatorKeyCommands.SelectKeys(session, keys);
        AnimatorSession.SetStatus(session, $"{keys.Count} KEYS SELECTED");
    }

    private static void Collect(
        XafClip clip,
        AnimatorKeyRef key,
        float anchor,
        List<AnimatorKeySample> samples)
    {
        foreach (XafChannel channel in clip.Channels)
        {
            if (channel.Target != key.Target || channel.TargetId != key.Id)
                continue;
            if (key.Property is { } property && channel.Property != property)
                continue;

            foreach (XafKeyframe frame in channel.Keyframes)
            {
                if (Math.Abs(frame.Time - key.Time) > XafTracks.TimeTolerance)
                    continue;

                samples.Add(new AnimatorKeySample(
                    key.Target,
                    key.Id,
                    channel.Property,
                    frame.Time - anchor,
                    frame.Value,
                    frame.Interpolation,
                    frame.TangentIn,
                    frame.TangentOut));
            }
        }
    }

    /// <summary>The copied row a sample came from, so a paste can restore channel-level selection.</summary>
    private static AnimatorKeyRef? SourceOf(AnimatorKeyClipboard clipboard, AnimatorKeySample sample)
    {
        if (clipboard.Refs is null)
            return null;

        foreach (AnimatorKeyRef reference in clipboard.Refs)
        {
            if (reference.Target != sample.Target || reference.Id != sample.Id)
                continue;
            if (reference.Property is { } property && property != sample.Property)
                continue;
            if (Math.Abs(reference.Time - clipboard.Anchor - sample.Offset) <= XafTracks.TimeTolerance)
                return reference;
        }
        return null;
    }

    private static bool TargetExists(Entity session, AnimatorKeySample sample)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        return sample.Target == XafChannelTarget.Bone
            ? package.Skeleton.Bones.Exists(bone => bone.Id == sample.Id)
            : package.Attachments.Exists(attachment => attachment.Group == sample.Id);
    }

    private static XafClip? CurrentClip(Entity session) =>
        AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
}
