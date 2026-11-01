using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Timeline;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tools.Animator;

/// <summary>The animator's clip as a shared timeline; its markers are the keys.</summary>
public sealed class AnimatorTimelineHost(Entity session) : ITimelineHost
{
    private AnimatorRuntime Runtime => session.GetComponent<AnimatorDocument>().Runtime;

    public double NowMs => Runtime.CurrentTime * 1000.0;

    public double DurationMs => Runtime.CurrentDuration * 1000.0;

    public double StepMs =>
        (AnimatorTimelineModel.Create(session)?.SnapStep ?? AnimatorShortcutSystem.DefaultStep) * 1000.0;

    public double StepOriginMs => 0;

    public bool Blocked => session.GetComponent<AnimatorNewClipDialog>().Open;

    public bool TryGetArea(out Rect2 panel, out Rect2 ruler)
    {
        AnimatorTimelineModel? model = AnimatorTimelineModel.Create(session);
        panel = model?.Panel ?? default;
        ruler = model?.Ruler ?? default;
        return model is not null;
    }

    public IEnumerable<double> MarkerTimesMs()
    {
        XafClip? clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        if (clip is null)
            yield break;

        foreach (XafChannel channel in clip.Channels)
        {
            foreach (XafKeyframe key in channel.Keyframes)
                yield return key.Time * 1000.0;
        }
    }

    public void SeekMs(double ms)
    {
        float seconds = (float)(ms / 1000.0);
        AnimatorRuntime runtime = Runtime;
        runtime.Seek(seconds);
        runtime.SetPaused(true);
        session.GetComponent<AnimatorSelection>().HasKey = false;
        AnimatorSession.SetStatus(session, $"{seconds:0.00}s");
    }

    public void TogglePlay() =>
        AnimatorCommands.Run(session, new ToolAction(ToolActionKind.TogglePlay));

    public void NoMarker(int direction) =>
        AnimatorSession.SetStatus(session, direction < 0 ? "NO EARLIER KEY" : "NO LATER KEY");
}
