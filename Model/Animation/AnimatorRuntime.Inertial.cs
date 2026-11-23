namespace Xanadu.Model.Animation;

/// <summary>
/// Inertial blending: instead of crossfading two clips, a hand-over plays the
/// new clip at once and carries the gap between what was on screen and the new
/// clip's pose, with how fast that gap was changing, as an offset that settles
/// to nothing on a critically damped spring. Nothing freezes or slides at a
/// constant rate, so the switch keeps her motion going and the seam can't be
/// seen; only one clip is sampled. A hand-over cut short just starts a new
/// offset from what is shown, so they chain without pops.
/// </summary>
public sealed partial class AnimatorRuntime
{
    /// <summary>How long a hand-over takes to settle when the package names no fade for it, in seconds.</summary>
    public const float InertialSeconds = 0.22f;

    /// <summary>The step the new clip's speed is measured over at a hand-over, in seconds.</summary>
    private const float VelocityStep = 1f / 60f;

    /// <summary>
    /// Hand-overs blend inertially rather than crossfade. The game turns it on;
    /// the animator keeps crossfades, which it shows and edits as such.
    /// </summary>
    public bool Inertial { get; init; }

    private RigTransform[] _gapBones = [];
    private RigTransform[] _gapBoneSpeeds = [];
    private RigTransform[] _gapAttachments = [];
    private RigTransform[] _gapAttachmentSpeeds = [];
    private float _gapElapsed;
    private float _gapDuration;

    // The base pose (clips and gap, before layers) on the last two updates, for its speed at a hand-over.
    private RigTransform[] _shownBones = [];
    private RigTransform[] _shownAttachments = [];
    private RigTransform[] _shownBonesBefore = [];
    private RigTransform[] _shownAttachmentsBefore = [];
    private float _shownDelta;
    private bool _shownValid;

    /// <summary>Whether a hand-over's gap is still settling.</summary>
    public bool IsSettling => _gapDuration > 0f;

    /// <summary>
    /// Switches straight to <paramref name="clip"/> from <paramref name="time"/>
    /// into it, the gap from what is shown settling over <paramref name="seconds"/>.
    /// </summary>
    private void SwitchInertially(RigClip clip, float time, float seconds)
    {
        RigPose target = RigSampler.Sample(Package, clip, time);
        if (_shownValid && seconds > 0f)
        {
            RigPose ahead = RigSampler.Sample(Package, clip, WrapTime(clip, time + VelocityStep));
            float delta = Math.Max(_shownDelta, 0.0001f);
            Gap(_shownBones, _shownBonesBefore, target.Bones, ahead.Bones, delta, ref _gapBones, ref _gapBoneSpeeds);
            Gap(_shownAttachments, _shownAttachmentsBefore, target.Attachments, ahead.Attachments, delta,
                ref _gapAttachments, ref _gapAttachmentSpeeds);
            _gapElapsed = 0f;
            _gapDuration = seconds;
        }
        else
        {
            _gapDuration = 0f;
        }

        _current = clip;
        _currentTime = time;
        _currentStarted = true;
        _next = null;
        _frozen = null;
        _nextTime = 0f;
        _fadeDuration = 0f;
        _fadeElapsed = 0f;
        ApplyGap(target);
        Pose = target;
    }

    /// <summary>The gap between what was shown and the new clip, and how fast it was changing, per channel.</summary>
    private void Gap(
        RigTransform[] shown,
        RigTransform[] shownBefore,
        RigTransform[] target,
        RigTransform[] targetAhead,
        float shownDelta,
        ref RigTransform[] gap,
        ref RigTransform[] speed)
    {
        int count = Math.Min(shown.Length, target.Length);
        if (gap.Length != target.Length)
        {
            gap = new RigTransform[target.Length];
            speed = new RigTransform[target.Length];
        }

        for (int i = 0; i < target.Length; i++)
        {
            if (i >= count)
            {
                gap[i] = default;
                speed[i] = default;
                continue;
            }

            gap[i] = Difference(shown[i], target[i]);
            RigTransform shownSpeed = Scaled(Difference(shown[i], shownBefore[i]), 1f / shownDelta);
            RigTransform targetSpeed = Scaled(Difference(targetAhead[i], target[i]), 1f / VelocityStep);
            speed[i] = Difference(shownSpeed, targetSpeed, wrap: false);
        }
    }

    /// <summary>Adds what is left of the gap to a freshly sampled pose.</summary>
    private void ApplyGap(RigPose pose)
    {
        if (_gapDuration <= 0f)
            return;

        // Critically damped: (x0 + (v0 + k x0) t) e^(-k t), all but gone by the duration.
        float k = 9f / _gapDuration;
        float t = _gapElapsed;
        float fall = MathF.Exp(-k * t);
        Settle(pose.Bones, _gapBones, _gapBoneSpeeds, k, t, fall);
        Settle(pose.Attachments, _gapAttachments, _gapAttachmentSpeeds, k, t, fall);
    }

    private static void Settle(RigTransform[] pose, RigTransform[] gap, RigTransform[] speed, float k, float t, float fall)
    {
        int count = Math.Min(pose.Length, gap.Length);
        for (int i = 0; i < count; i++)
        {
            RigTransform x0 = gap[i];
            RigTransform v0 = speed[i];
            pose[i].Translation += (x0.Translation + (v0.Translation + k * x0.Translation) * t) * fall;
            pose[i].Rotation += (x0.Rotation + (v0.Rotation + k * x0.Rotation) * t) * fall;
            pose[i].Scale += (x0.Scale + (v0.Scale + k * x0.Scale) * t) * fall;
            pose[i].Depth += (x0.Depth + (v0.Depth + k * x0.Depth) * t) * fall;
        }
    }

    /// <summary>Moves the gap on by <paramref name="delta"/>; it is dropped once settled.</summary>
    private void StepGap(float delta)
    {
        if (_gapDuration <= 0f)
            return;

        _gapElapsed += delta;
        if (_gapElapsed >= _gapDuration)
            _gapDuration = 0f;
    }

    /// <summary>Keeps the base pose just shown, so the next hand-over knows where it was and how fast it moved.</summary>
    private void RememberShown(RigPose basePose, float delta)
    {
        (_shownBonesBefore, _shownBones) = (_shownBones, _shownBonesBefore);
        (_shownAttachmentsBefore, _shownAttachments) = (_shownAttachments, _shownAttachmentsBefore);
        bool sized = _shownBones.Length == basePose.Bones.Length && _shownAttachments.Length == basePose.Attachments.Length;
        if (!sized)
        {
            _shownBones = new RigTransform[basePose.Bones.Length];
            _shownAttachments = new RigTransform[basePose.Attachments.Length];
        }

        Array.Copy(basePose.Bones, _shownBones, basePose.Bones.Length);
        Array.Copy(basePose.Attachments, _shownAttachments, basePose.Attachments.Length);
        if (!_shownValid || !sized || _shownBonesBefore.Length != _shownBones.Length ||
            _shownAttachmentsBefore.Length != _shownAttachments.Length)
        {
            // No earlier pose to measure against: count it as still.
            _shownBonesBefore = (RigTransform[])_shownBones.Clone();
            _shownAttachmentsBefore = (RigTransform[])_shownAttachments.Clone();
        }

        _shownDelta = delta;
        _shownValid = true;
    }

    /// <summary>Forgets the shown pose and any gap, as a seek does: the next hand-over starts clean.</summary>
    private void ForgetShown()
    {
        _shownValid = false;
        _gapDuration = 0f;
    }

    /// <summary><paramref name="a"/> less <paramref name="b"/>, channel by channel; turns go the short way round.</summary>
    private static RigTransform Difference(RigTransform a, RigTransform b, bool wrap = true)
    {
        float turn = a.Rotation - b.Rotation;
        if (wrap)
            turn = MathF.IEEERemainder(turn, MathF.Tau);

        return new RigTransform
        {
            Translation = a.Translation - b.Translation,
            Rotation = turn,
            Scale = a.Scale - b.Scale,
            Depth = a.Depth - b.Depth,
        };
    }

    private static RigTransform Scaled(RigTransform value, float by) => new()
    {
        Translation = value.Translation * by,
        Rotation = value.Rotation * by,
        Scale = value.Scale * by,
        Depth = value.Depth * by,
    };
}
