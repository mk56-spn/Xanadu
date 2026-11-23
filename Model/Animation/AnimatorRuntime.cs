using System;
using System.Collections.Generic;
using Xanadu.Core.Maths;

namespace Xanadu.Model.Animation;

/// <summary>Runtime state for one character animator instance.</summary>
public sealed partial class AnimatorRuntime
{
    private readonly Dictionary<string, RigClip> _clips =
        new(StringComparer.Ordinal);

    private RigClip? _current;
    private RigClip? _next;
    private float _currentTime;
    private float _nextTime;
    private float _fadeDuration;
    private float _fadeElapsed;
    private RigFadeCurve _fadeCurve;

    /// <summary>
    /// The pose on screen when a fade was cut short by another, faded out in
    /// place of a clip so the new fade starts from what was shown, not a pop.
    /// </summary>
    private RigPose? _frozen;
    private readonly List<AdditiveLayer> _additives = new();
    private readonly List<RigFiredEvent> _fired = new();

    // A clip that has just begun also reports an event sitting on its first instant.
    private bool _currentStarted;
    private bool _nextStarted;

    public AnimatorRuntime(RigPackage package)
    {
        Package = package;
        foreach (RigClip clip in package.Clips)
            _clips[clip.Id] = clip;
        Pose = RigPose.For(package);
    }

    public RigPackage Package { get; }

    /// <summary>
    /// The events the last <see cref="Update"/> passed, in the clip playing and
    /// in the clip fading in; cleared by the next update. Seeking passes none.
    /// </summary>
    public IReadOnlyList<RigFiredEvent> Fired => _fired;
    public RigPose Pose { get; private set; }
    public string CurrentClip => _current?.Id ?? "";
    public string NextClip => _next?.Id ?? "";
    public bool IsCrossfading => _next is not null;
    public bool Paused { get; private set; }

    public float CurrentTime => _currentTime;
    public float CurrentDuration => _current?.Duration ?? 0f;

    public float FadeDuration => _next is null ? 0f : _fadeDuration;
    public float FadeElapsed => _next is null ? 0f : _fadeElapsed;

    /// <summary>The fading-in clip's share of the pose, after its curve; 0 when not crossfading.</summary>
    public float FadeWeight => _next is null
        ? 0f
        : _fadeDuration <= 0f
            ? 1f
            : RigTransitions.Weight(_fadeCurve, _fadeElapsed / _fadeDuration);

    public IReadOnlyList<AdditiveLayer> Additives => _additives;

    /// <summary>
    /// Fades into a clip over the given time, whatever the transition table
    /// says; the animator uses it to cut between clips it is editing.
    /// </summary>
    public bool Play(
        string clipId,
        float fadeSeconds = 0.15f,
        RigFadeCurve curve = RigFadeCurve.Linear)
    {
        if (!_clips.TryGetValue(clipId, out RigClip? clip))
            return false;

        if (_current is null)
        {
            _current = clip;
            _currentTime = 0f;
            _currentStarted = true;
            _next = null;
            _frozen = null;
            Pose = RigSampler.Sample(Package, clip, 0f);
            return true;
        }

        if (_next is null ? _current.Id == clipId : _next.Id == clipId)
            return true;

        if (Inertial)
        {
            SwitchInertially(clip, 0f, fadeSeconds);
            return true;
        }

        StartFade(clip, fadeSeconds, curve);
        return true;
    }

    /// <summary>
    /// Moves to a clip the way the package's transition table says: with the
    /// rule's fade and curve, or not at all when the table does not allow it.
    /// The clip being faded into counts as the one playing, so a hand-over cut
    /// short is judged from where it was heading.
    /// </summary>
    public bool Transition(string clipId)
    {
        if (!_clips.ContainsKey(clipId))
            return false;
        if (_current is null)
            return Play(clipId, 0f);

        string from = _next?.Id ?? _current.Id;
        if (from == clipId)
            return true;
        if (!RigTransitions.Allows(Package, from, clipId))
            return false;

        (float fade, RigFadeCurve curve) = RigTransitions.Blend(Package, from, clipId);
        // A hand-over the package names no fade for settles at the inertial default.
        if (Inertial && RigTransitions.Find(Package, from, clipId) is null)
            fade = InertialSeconds;
        return Play(clipId, fade, curve);
    }

    public void SetPaused(bool paused)
    {
        Paused = paused;
    }

    public void Restart()
    {
        if (_current is null)
            return;

        if (Inertial)
        {
            SwitchInertially(_current, 0f, InertialSeconds);
            return;
        }

        _currentTime = 0f;
        _currentStarted = true;
        _next = null;
        _frozen = null;
        _nextTime = 0f;
        _fadeDuration = 0f;
        _fadeElapsed = 0f;
        Pose = RigSampler.Sample(Package, _current, 0f);
    }

    public void Seek(float time)
    {
        if (_current is null)
            return;

        _currentTime = Math.Clamp(time, 0f, _current.Duration);
        _currentStarted = false;
        ForgetShown();
        _next = null;
        _frozen = null;
        _nextTime = 0f;
        _fadeDuration = 0f;
        _fadeElapsed = 0f;
        Pose = RigSampler.Sample(Package, _current, _currentTime);
    }

    /// <summary>
    /// Layers a clip over whatever plays, or sets the weight of one already
    /// layered (its time and blend kept). A new layer starts at
    /// <paramref name="startTime"/> into its clip.
    /// </summary>
    public void AddLayer(
        string clipId,
        float weight,
        RigLayerBlend blend = RigLayerBlend.Additive,
        float startTime = 0f)
    {
        if (!_clips.TryGetValue(clipId, out RigClip? clip))
            return;

        for (int i = 0; i < _additives.Count; i++)
        {
            if (_additives[i].Clip.Id == clipId)
            {
                _additives[i] = _additives[i] with { Weight = weight };
                return;
            }
        }

        _additives.Add(new AdditiveLayer(clip, weight, Math.Max(0f, startTime), blend));
    }

    /// <summary>
    /// Layers a clip held at <paramref name="time"/> into it, or moves one
    /// already layered there, keeping its place among the layers: for a pose
    /// that is set each frame rather than played, such as bracing for a landing.
    /// </summary>
    public void HoldLayer(string clipId, float weight, float time, RigLayerBlend blend)
    {
        if (!_clips.TryGetValue(clipId, out RigClip? clip))
            return;

        for (int i = 0; i < _additives.Count; i++)
        {
            if (_additives[i].Clip.Id == clipId)
            {
                _additives[i] = _additives[i] with { Weight = weight, Time = time, Blend = blend };
                return;
            }
        }

        _additives.Add(new AdditiveLayer(clip, weight, Math.Max(0f, time), blend));
    }

    /// <summary>The layer playing this clip, if one is.</summary>
    public AdditiveLayer? Layer(string clipId)
    {
        foreach (AdditiveLayer layer in _additives)
        {
            if (layer.Clip.Id == clipId)
                return layer;
        }

        return null;
    }

    public void RemoveLayer(string clipId) =>
        _additives.RemoveAll(layer => layer.Clip.Id == clipId);

    public void Update(float delta)
    {
        _fired.Clear();
        if (Paused || delta <= 0f || _current is null)
            return;

        float currentFrom = _currentTime;
        _currentTime = ClampTime(_current, _currentTime + delta);
        Pass(_current, currentFrom, _currentTime, delta, _currentStarted);
        _currentStarted = false;
        if (_next is not null)
        {
            float nextFrom = _nextTime;
            _nextTime = ClampTime(_next, _nextTime + delta);
            Pass(_next, nextFrom, _nextTime, delta, _nextStarted);
            _nextStarted = false;
        }

        for (int i = 0; i < _additives.Count; i++)
        {
            AdditiveLayer layer = _additives[i];
            _additives[i] = layer with { Time = layer.Time + delta };
        }

        _fadeElapsed += delta;
        if (_next is not null && _fadeElapsed >= _fadeDuration)
            FinishFade();

        RigPose currentPose = _next is not null && _frozen is not null
            ? _frozen
            : RigSampler.Sample(Package, _current, _currentTime);
        RigPose basePose = currentPose;
        if (Inertial)
        {
            StepGap(delta);
            ApplyGap(basePose);
            RememberShown(basePose, delta);
        }

        if (_next is not null)
        {
            RigPose nextPose = RigSampler.Sample(
                Package,
                _next,
                _nextTime);
            basePose = RigBlend.Crossfade(currentPose, nextPose, FadeWeight);
        }

        foreach (AdditiveLayer layer in _additives)
        {
            if (layer.Blend == RigLayerBlend.Override)
            {
                RigPose over = RigSampler.SampleOver(Package, layer.Clip, layer.Time, basePose);
                basePose = RigBlend.Crossfade(basePose, over, layer.Weight);
                continue;
            }

            RigPose additivePose = RigSampler.Sample(
                Package,
                layer.Clip,
                layer.Time,
                additive: true);
            basePose = RigBlend.Add(basePose, additivePose, layer.Weight);
        }

        Pose = basePose;
    }

    /// <summary>Notes the events a clip passed going from one time to another; a loop that came round wrapped.</summary>
    private void Pass(RigClip clip, float from, float to, float delta, bool started)
    {
        bool wrapped = clip.Loop && to < from - RigTracks.TimeTolerance && delta > 0f;
        RigEventAuthoring.Passed(clip, from, to, wrapped, started, _fired);
    }

    private static float ClampTime(RigClip? clip, float time) =>
        clip is null ? time : WrapTime(clip, time);

    /// <summary>A time in a clip after playing on: wrapped into the loop range, or held at the end.</summary>
    public static float WrapTime(RigClip clip, float time)
    {
        if (clip.Loop)
        {
            float length = Math.Max(0.0001f, clip.LoopEnd - clip.LoopStart);
            return clip.LoopStart +
                   Mathf.PosMod(time - clip.LoopStart, length);
        }

        return Math.Clamp(time, 0f, clip.Duration);
    }

    private void StartFade(RigClip clip, float fadeSeconds, RigFadeCurve curve)
    {
        if (_next is not null)
        {
            // Cut short: what is on screen fades out, and the clip that was
            // fading in carries on underneath it as the one being left.
            _frozen = Pose.Clone();
            _current = _next;
            _currentTime = _nextTime;
            _currentStarted = _nextStarted;
        }
        else
        {
            _frozen = null;
        }

        _next = clip;
        _nextTime = 0f;
        _nextStarted = true;
        _fadeDuration = Math.Max(0f, fadeSeconds);
        _fadeElapsed = 0f;
        _fadeCurve = curve;
        if (_fadeDuration <= 0f)
            FinishFade();
    }

    private void FinishFade()
    {
        if (_next is null)
            return;

        _current = _next;
        _currentTime = _nextTime;
        _currentStarted = _nextStarted;
        _next = null;
        _frozen = null;
        _fadeDuration = 0f;
        _fadeElapsed = 0f;
    }

    public readonly record struct AdditiveLayer(
        RigClip Clip,
        float Weight,
        float Time,
        RigLayerBlend Blend = RigLayerBlend.Additive);
}
