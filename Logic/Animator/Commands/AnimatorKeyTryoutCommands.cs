using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Keys;
using Xanadu.Core.Timeline;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>Starts, re-eases, replays and ends the quick test after keying (see <see cref="AnimatorKeyTryout"/>).</summary>
public static class AnimatorKeyTryoutCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<TryoutEase>((s, e) => Ease(s, e.Ease));
        session.On<TryoutReplay>((s, _) => Replay(s));
        session.On<TryoutClose>((s, _) => Close(s));
    }

    /// <summary>The eases the bar offers, left to right.</summary>
    public static readonly EasePreset[] Offered =
    [
        EasePreset.Step, EasePreset.Linear, EasePreset.Ease, EasePreset.EaseIn, EasePreset.EaseOut,
        EasePreset.BackOut, EasePreset.ElasticOut, EasePreset.BounceOut,
    ];

    /// <summary>
    /// After the selection was keyed at the playhead: plays the move from the
    /// previous key into it. Does nothing when there is no earlier key, or when
    /// other parts are posed but unkeyed, since replaying would throw that pose away.
    /// </summary>
    public static void Start(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (AnimatorModes.Of(session).Rest || AnimatorSession.CurrentClip(document) is not { } clip)
            return;
        if (AnimatorUnkeyed.Count(session) > 0)
            return;

        var targets = Targets(session);
        float to = document.Runtime.CurrentTime;
        float? from = null;
        foreach (KeyChannel channel in clip.Channels)
        {
            if (!targets.Contains((channel.Target, channel.TargetId)))
                continue;
            foreach (Keyframe key in channel.Keys)
            {
                if (key.Time < to - KeyChannel.TimeTolerance && (from is null || key.Time > from))
                    from = key.Time;
            }
        }
        if (from is null || targets.Count == 0)
            return;

        ref AnimatorKeyTryout tryout = ref session.GetComponent<AnimatorKeyTryout>();
        tryout = new AnimatorKeyTryout
        {
            Active = true,
            From = from.Value,
            To = to,
            Clip = clip.Id,
            Targets = targets,
            Picked = Current(clip, targets, to),
        };
        Replay(session);
    }

    /// <summary>Eases the move into the new key with <paramref name="preset"/>, as one undo step, and plays it again.</summary>
    public static void Ease(Entity session, EasePreset preset)
    {
        AnimatorKeyTryout tryout = session.GetComponent<AnimatorKeyTryout>();
        if (!tryout.Active)
            return;

        AnimatorHistoryCommands.Edit(session, () =>
        {
            if (AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>()) is not { } clip)
                return;
            foreach (KeyChannel channel in clip.Channels)
            {
                if (!tryout.Targets.Contains((channel.Target, channel.TargetId)) || !channel.IsRigField)
                    continue;
                if (Previous(channel, tryout.To) is { } key)
                    RigEasing.Apply(clip, channel.Target, channel.TargetId, channel.Property, key.Time, preset);
            }
        });
        session.GetComponent<AnimatorKeyTryout>().Picked = preset;
        AnimatorSession.SetStatus(session, $"EASE {preset.ToString().ToUpperInvariant()} INTO THE KEY");
        Replay(session);
    }

    /// <summary>Plays the move from the previous key again.</summary>
    public static void Replay(Entity session)
    {
        ref AnimatorKeyTryout tryout = ref session.GetComponent<AnimatorKeyTryout>();
        if (!tryout.Active)
            return;

        tryout.Playing = true;
        tryout.Elapsed = 0f;
        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        runtime.SetPaused(true);
        runtime.Seek(tryout.From);
        tryout.Shown = runtime.CurrentTime;
    }

    /// <summary>
    /// Stops a replay on the new key, leaving the bar up: every edit and grab
    /// calls this first, so nothing is ever posed or keyed between the two keys.
    /// </summary>
    public static void Settle(Entity session)
    {
        if (!session.HasComponent<AnimatorKeyTryout>())
            return;
        ref AnimatorKeyTryout tryout = ref session.GetComponent<AnimatorKeyTryout>();
        if (!tryout.Active || !tryout.Playing)
            return;

        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        runtime.Seek(tryout.To);
        tryout.Shown = runtime.CurrentTime;
        tryout.Playing = false;
    }

    /// <summary>Puts the playhead back on the new key and hides the bar.</summary>
    public static void Close(Entity session)
    {
        ref AnimatorKeyTryout tryout = ref session.GetComponent<AnimatorKeyTryout>();
        if (!tryout.Active)
            return;

        if (tryout.Playing)
            session.GetComponent<AnimatorDocument>().Runtime.Seek(tryout.To);
        tryout.Active = false;
        tryout.Playing = false;
    }

    /// <summary>The selected bones, or else the selected part.</summary>
    public static List<(RigChannelTarget, string)> Targets(Entity session)
    {
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        var targets = new List<(RigChannelTarget, string)>();
        foreach (int bone in selection.Bones())
        {
            if (bone >= 0 && bone < package.Skeleton.Bones.Count)
                targets.Add((RigChannelTarget.Bone, package.Skeleton.Bones[bone].Id));
        }
        if (targets.Count == 0 && selection.Attachment >= 0 && selection.Attachment < package.Attachments.Count)
            targets.Add((RigChannelTarget.Attachment, package.Attachments[selection.Attachment].Id));
        return targets;
    }

    private static Keyframe? Previous(KeyChannel channel, float to)
    {
        Keyframe? best = null;
        foreach (Keyframe key in channel.Keys)
        {
            if (key.Time < to - KeyChannel.TimeTolerance && (best is null || key.Time > best.Time))
                best = key;
        }
        return best;
    }

    /// <summary>The ease the move into the new key has now, when every keyed channel agrees on one.</summary>
    private static EasePreset? Current(RigClip clip, List<(RigChannelTarget, string)> targets, float to)
    {
        EasePreset? found = null;
        foreach (KeyChannel channel in clip.Channels)
        {
            if (!targets.Contains((channel.Target, channel.TargetId)) || !channel.IsRigField)
                continue;
            if (Previous(channel, to) is not { } key)
                continue;
            EasePreset? preset = RigEasing.Read(clip, channel.Target, channel.TargetId, channel.Property, key.Time);
            if (preset is null || (found is not null && found != preset))
                return null;
            found = preset;
        }
        return found;
    }
}
