using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Animation;
using Writer.Ecs;
using Writer.Systems.Input;
using Writer.Tools.Animator.Commands;

namespace Writer.Tools.Animator.Systems;

/// <summary>
/// Keyboard shortcuts. Ctrl+Z undo, Ctrl+Y or Ctrl+Shift+Z redo, Ctrl+S save,
/// Delete removes the selected key, Ctrl+C / Ctrl+V copy and paste the pose.
/// Space plays or pauses; Left/Right step the playhead one snap step, Up/Down
/// jump to the previous/next key, and Home/End go to the clip's start/end.
/// </summary>
public sealed class AnimatorShortcutSystem(IKeyboardState keyboard)
    : QuerySystem<AnimatorDocument>
{
    /// <summary>Step used when the timeline is not laid out, as in a headless test.</summary>
    public const float DefaultStep = 1f / 30f;

    private enum Shortcut
    {
        Undo,
        Redo,
        Save,
        Delete,
        Copy,
        Paste,
        PlayPause,
        StepBack,
        StepForward,
        PreviousKey,
        NextKey,
        Start,
        End,
    }

    private readonly HashSet<Shortcut> _held = new();
    private readonly HashSet<Shortcut> _down = new();

    protected override void OnUpdate()
    {
        _down.Clear();

        // Typing in a name field must not trigger shortcuts such as Delete or Space.
        bool typing = Query.Store.Query<TextField>().AllTags(Tags.Get<TextFieldFocused>()).Count > 0;
        if (!typing)
            ReadKeys();

        var pressed = new List<Shortcut>();
        foreach (Shortcut shortcut in _down)
        {
            if (!_held.Contains(shortcut))
                pressed.Add(shortcut);
        }

        // While typing everything counts as held, so letting go of a key in
        // the field does not fire it once the field loses focus.
        if (typing)
            _held.UnionWith(Enum.GetValues<Shortcut>());
        else
        {
            _held.Clear();
            _held.UnionWith(_down);
        }

        if (pressed.Count == 0)
            return;

        foreach (Entity session in Query.ToEntityList())
        {
            foreach (Shortcut shortcut in pressed)
                Run(session, shortcut);
        }
    }

    private void ReadKeys()
    {
        bool control = keyboard.IsPressed(Key.Ctrl);
        bool shift = keyboard.IsPressed(Key.Shift);
        Down(Shortcut.Undo, control && !shift && keyboard.IsPressed(Key.Z));
        Down(Shortcut.Redo, control && (keyboard.IsPressed(Key.Y) || (shift && keyboard.IsPressed(Key.Z))));
        Down(Shortcut.Save, control && keyboard.IsPressed(Key.S));
        Down(Shortcut.Delete, keyboard.IsPressed(Key.Delete));
        Down(Shortcut.Copy, control && keyboard.IsPressed(Key.C));
        Down(Shortcut.Paste, control && keyboard.IsPressed(Key.V));
        Down(Shortcut.PlayPause, keyboard.IsPressed(Key.Space));
        Down(Shortcut.StepBack, keyboard.IsPressed(Key.Left));
        Down(Shortcut.StepForward, keyboard.IsPressed(Key.Right));
        Down(Shortcut.PreviousKey, keyboard.IsPressed(Key.Up));
        Down(Shortcut.NextKey, keyboard.IsPressed(Key.Down));
        Down(Shortcut.Start, keyboard.IsPressed(Key.Home));
        Down(Shortcut.End, keyboard.IsPressed(Key.End));
    }

    private void Down(Shortcut shortcut, bool down)
    {
        if (down)
            _down.Add(shortcut);
    }

    private static void Run(Entity session, Shortcut shortcut)
    {
        switch (shortcut)
        {
            case Shortcut.Undo:
                AnimatorHistoryCommands.Undo(session);
                break;
            case Shortcut.Redo:
                AnimatorHistoryCommands.Redo(session);
                break;
            case Shortcut.Save:
                AnimatorFileCommands.Save(session);
                break;
            case Shortcut.Delete:
                AnimatorKeyCommands.DeleteSelected(session);
                break;
            case Shortcut.Copy:
                AnimatorPoseCommands.CopyPose(session);
                break;
            case Shortcut.Paste:
                AnimatorPoseCommands.PastePose(session);
                break;
            case Shortcut.PlayPause:
                PlayPause(session);
                break;
            case Shortcut.StepBack:
                Step(session, -1);
                break;
            case Shortcut.StepForward:
                Step(session, 1);
                break;
            case Shortcut.PreviousKey:
                JumpToKey(session, -1);
                break;
            case Shortcut.NextKey:
                JumpToKey(session, 1);
                break;
            case Shortcut.Start:
                SeekTo(session, 0f);
                break;
            case Shortcut.End:
                SeekTo(session, session.GetComponent<AnimatorDocument>().Runtime.CurrentDuration);
                break;
        }
    }

    private static void PlayPause(Entity session)
    {
        AnimatorPlayback playback = session.GetComponent<AnimatorDocument>().Playback;
        if (playback.IsPlaying)
        {
            playback.Stop();
            AnimatorSession.SetStatus(session, "STOPPED");
        }
        else
        {
            playback.Play();
            AnimatorSession.SetStatus(session, "PLAYING");
        }
    }

    /// <summary>Moves one timeline snap step, landing on the step grid.</summary>
    private static void Step(Entity session, int direction)
    {
        double step = AnimatorTimelineModel.Create(session)?.SnapStep ?? DefaultStep;
        float time = session.GetComponent<AnimatorDocument>().Runtime.CurrentTime;
        double index = Math.Round(time / step) + direction;
        SeekTo(session, (float)(index * step));
    }

    /// <summary>Jumps to the nearest key before or after the playhead, on any track.</summary>
    private static void JumpToKey(Entity session, int direction)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        XafClip? clip = AnimatorSession.CurrentClip(document);
        if (clip is null)
            return;

        float now = document.Runtime.CurrentTime;
        float? best = null;
        foreach (XafChannel channel in clip.Channels)
        {
            foreach (XafKeyframe key in channel.Keyframes)
            {
                float offset = (key.Time - now) * direction;
                if (offset <= XafTracks.TimeTolerance)
                    continue;
                if (best is null || offset < (best.Value - now) * direction)
                    best = key.Time;
            }
        }

        if (best is null)
        {
            AnimatorSession.SetStatus(session, direction < 0 ? "NO EARLIER KEY" : "NO LATER KEY");
            return;
        }
        SeekTo(session, best.Value);
    }

    private static void SeekTo(Entity session, float time)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        float clamped = Math.Clamp(time, 0f, document.Runtime.CurrentDuration);
        document.Runtime.Seek(clamped);
        document.Runtime.SetPaused(true);
        session.GetComponent<AnimatorSelection>().HasKey = false;
        AnimatorSession.SetStatus(session, $"{clamped:0.00}s");
    }
}
