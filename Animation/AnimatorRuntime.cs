using System;
using System.Collections.Generic;

namespace Writer.Animation;

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

    public IReadOnlyList<AdditiveLayer> Additives => _additives;

    public bool Play(string clipId, float fadeSeconds = 0.15f)
    {
        if (!_clips.TryGetValue(clipId, out XafClip? clip))
            return false;

        if (_current is null)
        {
            _current = clip;
            _currentTime = 0f;
            _next = null;
            Pose = XafSampler.Sample(Package, clip, 0f);
            return true;
        }

        if (_current.Id == clipId)
            return true;

        _next = clip;
        _nextTime = 0f;
        _fadeDuration = Math.Max(0f, fadeSeconds);
        _fadeElapsed = 0f;
        if (_fadeDuration <= 0f)
            FinishFade();
        return true;
    }

    public void SetPaused(bool paused)
    {
        Paused = paused;
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
        if (Paused || delta <= 0f)
            return;

        _currentTime += delta;
        if (_next is not null)
            _nextTime += delta;

        for (int i = 0; i < _additives.Count; i++)
        {
            AdditiveLayer layer = _additives[i];
            _additives[i] = layer with { Time = layer.Time + delta };
        }

        _fadeElapsed += delta;
        if (_next is not null && _fadeElapsed >= _fadeDuration)
            FinishFade();

        XafPose currentPose = XafSampler.Sample(
            Package,
            _current!,
            _currentTime);
        XafPose basePose = currentPose;

        if (_next is not null)
        {
            float weight = _fadeDuration <= 0f
                ? 1f
                : Math.Clamp(_fadeElapsed / _fadeDuration, 0f, 1f);
            XafPose nextPose = XafSampler.Sample(
                Package,
                _next,
                _nextTime);
            basePose = XafBlend.Crossfade(currentPose, nextPose, weight);
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

    private void FinishFade()
    {
        if (_next is null)
            return;

        _current = _next;
        _currentTime = _nextTime;
        _next = null;
        _fadeDuration = 0f;
        _fadeElapsed = 0f;
    }

    public readonly record struct AdditiveLayer(
        XafClip Clip,
        float Weight,
        float Time);
}
