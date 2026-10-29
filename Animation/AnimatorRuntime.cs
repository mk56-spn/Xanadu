using Godot;
using System;
using System.Collections.Generic;

namespace Xanadu.Animation;

/// <summary>Runtime state for one character animator instance.</summary>
public sealed class AnimatorRuntime
{
    private readonly Dictionary<string, XafClip> _clips =
        new(StringComparer.Ordinal);

    private XafClip? _current;
    private XafClip? _next;
    private float _currentTime;
    private float _nextTime;
    private float _fadeDuration;
    private float _fadeElapsed;
    private XafFadeCurve _fadeCurve;

    /// <summary>
    /// The pose on screen when a fade was cut short by another, faded out in
    /// place of a clip so the new fade starts from what was shown, not a pop.
    /// </summary>
    private XafPose? _frozen;
    private readonly List<AdditiveLayer> _additives = new();

    public AnimatorRuntime(XafPackage package)
    {
        Package = package;
        foreach (XafClip clip in package.Clips)
            _clips[clip.Id] = clip;
        Pose = new XafPose(
            package.Skeleton.Bones.Count,
            package.Attachments.Count);
    }

    public XafPackage Package { get; }
    public XafPose Pose { get; private set; }
    public string CurrentClip => _current?.Id ?? "";
    public string NextClip => _next?.Id ?? "";
    public bool IsCrossfading => _next is not null;
    public bool Paused { get; private set; }

    public float CurrentTime => _currentTime;
    public float CurrentDuration => _current?.Duration ?? 0f;

    /// <summary>Seconds into the clip fading in; 0 when not crossfading.</summary>
    public float NextTime => _next is null ? 0f : _nextTime;
    public float FadeDuration => _next is null ? 0f : _fadeDuration;
    public float FadeElapsed => _next is null ? 0f : _fadeElapsed;
    public XafFadeCurve FadeCurve => _fadeCurve;

    /// <summary>The fading-in clip's share of the pose, after its curve; 0 when not crossfading.</summary>
    public float FadeWeight => _next is null
        ? 0f
        : _fadeDuration <= 0f
            ? 1f
            : XafTransitions.Weight(_fadeCurve, _fadeElapsed / _fadeDuration);

    public IReadOnlyList<AdditiveLayer> Additives => _additives;

    /// <summary>
    /// Fades into a clip over the given time, whatever the transition table
    /// says; the animator uses it to cut between clips it is editing.
    /// </summary>
    public bool Play(
        string clipId,
        float fadeSeconds = 0.15f,
        XafFadeCurve curve = XafFadeCurve.Linear)
    {
        if (!_clips.TryGetValue(clipId, out XafClip? clip))
            return false;

        if (_current is null)
        {
            _current = clip;
            _currentTime = 0f;
            _next = null;
            _frozen = null;
            Pose = XafSampler.Sample(Package, clip, 0f);
            return true;
        }

        if (_next is null ? _current.Id == clipId : _next.Id == clipId)
            return true;

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
        if (!XafTransitions.Allows(Package, from, clipId))
            return false;

        (float fade, XafFadeCurve curve) = XafTransitions.Blend(Package, from, clipId);
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

        _currentTime = 0f;
        _next = null;
        _frozen = null;
        _nextTime = 0f;
        _fadeDuration = 0f;
        _fadeElapsed = 0f;
        Pose = XafSampler.Sample(Package, _current, 0f);
    }

    public void Seek(float time)
    {
        if (_current is null)
            return;

        _currentTime = Math.Clamp(time, 0f, _current.Duration);
        _next = null;
        _frozen = null;
        _nextTime = 0f;
        _fadeDuration = 0f;
        _fadeElapsed = 0f;
        Pose = XafSampler.Sample(Package, _current, _currentTime);
    }

    public void AddLayer(string clipId, float weight)
    {
        if (!_clips.TryGetValue(clipId, out XafClip? clip))
            return;

        for (int i = 0; i < _additives.Count; i++)
        {
            if (_additives[i].Clip.Id == clipId)
            {
                _additives[i] = new AdditiveLayer(
                    clip,
                    weight,
                    _additives[i].Time);
                return;
            }
        }

        _additives.Add(new AdditiveLayer(clip, weight, 0f));
    }

    public void RemoveLayer(string clipId) =>
        _additives.RemoveAll(layer => layer.Clip.Id == clipId);

    public void Update(float delta)
    {
        if (Paused || delta <= 0f || _current is null)
            return;

        _currentTime = ClampTime(_current, _currentTime + delta);
        if (_next is not null)
            _nextTime = ClampTime(_next, _nextTime + delta);

        for (int i = 0; i < _additives.Count; i++)
        {
            AdditiveLayer layer = _additives[i];
            _additives[i] = layer with { Time = layer.Time + delta };
        }

        _fadeElapsed += delta;
        if (_next is not null && _fadeElapsed >= _fadeDuration)
            FinishFade();

        XafPose currentPose = _next is not null && _frozen is not null
            ? _frozen
            : XafSampler.Sample(Package, _current, _currentTime);
        XafPose basePose = currentPose;

        if (_next is not null)
        {
            XafPose nextPose = XafSampler.Sample(
                Package,
                _next,
                _nextTime);
            basePose = XafBlend.Crossfade(currentPose, nextPose, FadeWeight);
        }

        foreach (AdditiveLayer layer in _additives)
        {
            XafPose additivePose = XafSampler.Sample(
                Package,
                layer.Clip,
                layer.Time,
                additive: true);
            basePose = XafBlend.Add(basePose, additivePose, layer.Weight);
        }

        Pose = basePose;
    }

    private static float ClampTime(XafClip? clip, float time) =>
        clip is null ? time : WrapTime(clip, time);

    /// <summary>A time in a clip after playing on: wrapped into the loop range, or held at the end.</summary>
    public static float WrapTime(XafClip clip, float time)
    {
        if (clip.Loop)
        {
            float length = Math.Max(0.0001f, clip.LoopEnd - clip.LoopStart);
            return clip.LoopStart +
                   Mathf.PosMod(time - clip.LoopStart, length);
        }

        return Math.Clamp(time, 0f, clip.Duration);
    }

    private void StartFade(XafClip clip, float fadeSeconds, XafFadeCurve curve)
    {
        if (_next is not null)
        {
            // Cut short: what is on screen fades out, and the clip that was
            // fading in carries on underneath it as the one being left.
            _frozen = Pose.Clone();
            _current = _next;
            _currentTime = _nextTime;
        }
        else
        {
            _frozen = null;
        }

        _next = clip;
        _nextTime = 0f;
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
        _next = null;
        _frozen = null;
        _fadeDuration = 0f;
        _fadeElapsed = 0f;
    }

    public readonly record struct AdditiveLayer(
        XafClip Clip,
        float Weight,
        float Time);
}
