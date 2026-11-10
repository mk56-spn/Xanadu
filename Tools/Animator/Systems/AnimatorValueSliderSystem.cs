using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Keeps every <see cref="AnimatorValueSlider"/> on the value it is bound to
/// and applies the user's moves. A drag is one undo step; a pose value is
/// keyed once, on release. A click or right-click reset is an ordinary edit.
/// </summary>
public sealed class AnimatorValueSliderSystem : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private struct State
    {
        public float Shown;
        public bool Dragging;
    }

    private readonly Dictionary<int, State> _states = new();

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            foreach (Entity track in Query.Store.Query<AnimatorValueSlider, UiSlider>().ToEntityList())
                Update(session, track);
        }
    }

    private void Update(Entity session, Entity track)
    {
        AnimatorValueSlider binding = track.GetComponent<AnimatorValueSlider>();
        ref UiSlider slider = ref track.GetComponent<UiSlider>();
        _states.TryGetValue(track.Id, out State state);

        bool held = track.Tags.Has<Pressed>();
        bool available = TryGet(session, binding.Value, out _);
        bool moved = !Mathf.IsEqualApprox(slider.Value, state.Shown);

        AnimatorSliderValue kind = binding.Value;
        float amount = slider.Value;
        if (moved && available)
        {
            if (!Undoable(kind))
            {
                Set(session, kind, amount);
            }
            else if (held)
            {
                if (!state.Dragging)
                {
                    AnimatorHistoryCommands.BeginGesture(session);
                    state.Dragging = true;
                }
                AnimatorHistoryCommands.DuringGesture(session, () => Set(session, kind, amount));
            }
            else
            {
                AnimatorHistoryCommands.Edit(session, () =>
                {
                    Set(session, kind, amount);
                    Finish(session, kind);
                });
            }
        }

        if (state.Dragging && !held)
        {
            state.Dragging = false;
            AnimatorHistoryCommands.EndGesture(session, () => Finish(session, kind));
        }

        available = TryGet(session, kind, out float current);
        slider.Value = available ? Mathf.Clamp(current, slider.Min, slider.Max) : slider.Default;
        state.Shown = slider.Value;
        _states[track.Id] = state;

        // The slider system writes the readout while there is a value; a dash says there is none.
        if (available)
        {
            slider.Label = binding.Readout;
        }
        else
        {
            slider.Label = default;
            if (!binding.Readout.IsNull && binding.Readout.HasComponent<Text>())
                UiText.Set(binding.Readout, "–");
        }
    }

    /// <summary>Brush settings are the tool's own, not the file's, so they skip the history.</summary>
    private static bool Undoable(AnimatorSliderValue kind) =>
        kind is not (AnimatorSliderValue.BrushSize or AnimatorSliderValue.BrushStrength);

    /// <summary>What a finished change does last: depth is keyed, as dragging a joint is.</summary>
    private static void Finish(Entity session, AnimatorSliderValue kind)
    {
        if (kind == AnimatorSliderValue.Depth && !session.GetComponent<AnimatorModes>().Rest)
            AnimatorEditing.KeyEdit(session);
    }

    private static void Set(Entity session, AnimatorSliderValue kind, float value)
    {
        switch (kind)
        {
            case AnimatorSliderValue.Depth:
                AnimatorEditing.SetDepth(session, value);
                break;
            case AnimatorSliderValue.LimitMin:
                AnimatorEditing.SetLimit(session, max: false, Mathf.DegToRad(value));
                break;
            case AnimatorSliderValue.LimitMax:
                AnimatorEditing.SetLimit(session, max: true, Mathf.DegToRad(value));
                break;
            case AnimatorSliderValue.BrushSize:
                AnimatorWeightCommands.SetRadius(session, value);
                break;
            case AnimatorSliderValue.BrushStrength:
                AnimatorWeightCommands.SetStrength(session, value);
                break;
            case AnimatorSliderValue.MeshBlend:
                AnimatorArtCommands.SetBlend(session, value);
                break;
            case AnimatorSliderValue.EffectOpacity:
                AnimatorEffectCommands.SetOpacity(session, value / 100f);
                break;
            case AnimatorSliderValue.TargetRadiusX:
                AnimatorTargetCommands.SetShape(session, "rx", value);
                break;
            case AnimatorSliderValue.TargetRadiusY:
                AnimatorTargetCommands.SetShape(session, "ry", value);
                break;
            case AnimatorSliderValue.TargetTilt:
                AnimatorTargetCommands.SetShape(session, "tilt", Mathf.DegToRad(value));
                break;
            case AnimatorSliderValue.TargetStart:
                AnimatorTargetCommands.SetShape(session, "start", Mathf.DegToRad(value));
                break;
            case AnimatorSliderValue.TargetSweep:
                AnimatorTargetCommands.SetShape(session, "sweep", Mathf.DegToRad(value));
                break;
            case AnimatorSliderValue.TransitionFade:
                AnimatorFlowCommands.SetFade(session, value);
                break;
        }
    }

    /// <summary>The bound value in the slider's units, if what it applies to is selected.</summary>
    public static bool TryGet(Entity session, AnimatorSliderValue kind, out float value)
    {
        value = 0f;
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        XafPackage package = document.Package;
        int bone = selection.Bone;
        bool hasBone = bone >= 0 && bone < package.Skeleton.Bones.Count;

        switch (kind)
        {
            case AnimatorSliderValue.Depth:
                if (!hasBone || session.GetComponent<AnimatorModes>().Rest || bone >= document.Runtime.Pose.Bones.Length)
                    return false;
                value = document.Runtime.Pose.Bones[bone].Depth;
                return true;
            case AnimatorSliderValue.LimitMin or AnimatorSliderValue.LimitMax:
                if (!hasBone || !package.Skeleton.Bones[bone].Limited)
                    return false;
                value = Mathf.RadToDeg(kind == AnimatorSliderValue.LimitMin
                    ? package.Skeleton.Bones[bone].LimitMin
                    : package.Skeleton.Bones[bone].LimitMax);
                return true;
            case AnimatorSliderValue.BrushSize:
                value = session.GetComponent<AnimatorWeightPaint>().Radius;
                return true;
            case AnimatorSliderValue.BrushStrength:
                value = session.GetComponent<AnimatorWeightPaint>().Strength;
                return true;
            case AnimatorSliderValue.MeshBlend:
                return AnimatorArtCommands.TryGetBlend(session, out value);
            case AnimatorSliderValue.EffectOpacity:
                int effect = AnimatorEffectCommands.Selected(session);
                if (effect < 0 || effect >= document.Runtime.Pose.Effects.Length)
                    return false;
                value = document.Runtime.Pose.Effects[effect].Opacity * 100f;
                return true;
            case AnimatorSliderValue.TransitionFade:
                if (AnimatorFlowCommands.Rule(session) is not { } rule)
                    return false;
                value = rule.Fade;
                return true;
        }

        if (AnimatorTargetCommands.Selected(session) is not { } target)
            return false;
        value = kind switch
        {
            AnimatorSliderValue.TargetRadiusX => target.RadiusX,
            AnimatorSliderValue.TargetRadiusY => target.RadiusY,
            AnimatorSliderValue.TargetTilt => Mathf.RadToDeg(Mathf.AngleDifference(0f, target.Tilt)),
            AnimatorSliderValue.TargetStart => Mathf.RadToDeg(Mathf.AngleDifference(0f, target.StartAngle)),
            _ => Mathf.RadToDeg(target.Sweep),
        };
        return true;
    }
}
