using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Keyboard shortcuts. The actions buttons perform come from
/// <see cref="AnimatorBindings"/> and run through
/// <see cref="AnimatorCommands.Run"/>, exactly as a pressed button does; this
/// system only adds the keys no button covers: Left/Right step the playhead one
/// snap step, Up/Down jump to the previous/next key, and Home/End go to the
/// clip's start/end.
/// </summary>
public sealed class AnimatorShortcutSystem(IKeyboardState keyboard)
    : QuerySystem<AnimatorDocument>
{
    /// <summary>Step used when the timeline is not laid out, as in a headless test.</summary>
    public const float DefaultStep = 1f / 30f;

    /// <summary>Playhead moves that have no button of their own.</summary>
    private enum Shortcut
    {
        StepBack,
        StepForward,
        PreviousKey,
        NextKey,
        Start,
        End,
    }

    private readonly HashSet<Shortcut> _held = new();
    private readonly HashSet<Shortcut> _down = new();
    private readonly HashSet<int> _heldBindings = new();
    private readonly HashSet<int> _downBindings = new();

    protected override void OnUpdate()
    {
        _down.Clear();
        _downBindings.Clear();

        // Typing in a name field or having the NEW CLIP dialog open must not
        // trigger shortcuts such as Delete or Space.
        bool typing = Query.Store.Query<TextField>().AllTags(Tags.Get<TextFieldFocused>()).Count > 0;
        bool modal = false;
        foreach (Entity session in Query.Store.Query<AnimatorNewClipDialog>().ToEntityList())
        {
            if (!session.GetComponent<AnimatorNewClipDialog>().Open)
                continue;

            modal = true;
            break;
        }

        bool blocked = typing || modal;
        if (!blocked)
            ReadKeys();

        var pressed = new List<Shortcut>();
        foreach (Shortcut shortcut in _down)
        {
            if (!_held.Contains(shortcut))
                pressed.Add(shortcut);
        }

        var actions = new List<ToolAction>();
        foreach (int index in _downBindings)
        {
            if (!_heldBindings.Contains(index))
                actions.Add(AnimatorBindings.All[index].Action);
        }

        // While blocked everything counts as held, so letting go of a key in
        // the field does not fire it once the field loses focus.
        if (blocked)
        {
            _held.UnionWith(Enum.GetValues<Shortcut>());
            for (int i = 0; i < AnimatorBindings.All.Length; i++)
                _heldBindings.Add(i);
        }
        else
        {
            _held.Clear();
            _held.UnionWith(_down);
            _heldBindings.Clear();
            _heldBindings.UnionWith(_downBindings);
        }

        if (pressed.Count == 0 && actions.Count == 0)
            return;

        foreach (Entity session in Query.ToEntityList())
        {
            foreach (Shortcut shortcut in pressed)
                Run(session, shortcut);
            foreach (ToolAction action in actions)
                AnimatorCommands.Run(session, action);
        }
    }

    private void ReadKeys()
    {
        bool control = keyboard.IsPressed(Key.Ctrl);
        bool shift = keyboard.IsPressed(Key.Shift);
        Down(Shortcut.StepBack, keyboard.IsPressed(Key.Left));
        Down(Shortcut.StepForward, keyboard.IsPressed(Key.Right));
        Down(Shortcut.PreviousKey, keyboard.IsPressed(Key.Up));
        Down(Shortcut.NextKey, keyboard.IsPressed(Key.Down));
        Down(Shortcut.Start, keyboard.IsPressed(Key.Home));
        Down(Shortcut.End, keyboard.IsPressed(Key.End));

        // A chord counts when its modifiers match exactly, so plain O (onion)
        // and Ctrl+O (load) are different bindings.
        for (int i = 0; i < AnimatorBindings.All.Length; i++)
        {
            AnimatorBindings.Binding binding = AnimatorBindings.All[i];
            if (binding.Ctrl == control &&
                binding.Shift == shift &&
                keyboard.IsPressed(binding.Key))
            {
                _downBindings.Add(i);
            }
        }
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
