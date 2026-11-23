using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Animation;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Runs every animator value field (<see cref="UiValueField"/> with an
/// <see cref="AnimatorValueBinding"/>): keeps it on the rig's value while the
/// user isn't editing it, and turns the user's edits into undo steps. A scrub
/// is one step and keys once, when it ends; a typed or reset value is one edit.
/// A field with nothing to show (no bone selected, say) shows its defaults.
/// </summary>
public sealed class AnimatorValueFieldSystem : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private sealed class State
    {
        public float[] Shown = [];
        public bool Dragging;
    }

    private readonly Dictionary<int, State> _states = new();
    private readonly float[] _read = new float[4];

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            foreach (Entity field in Query.Store.Query<AnimatorValueBinding, UiValueField>().ToEntityList())
                Update(session, field);
        }
    }

    private void Update(Entity session, Entity entity)
    {
        AnimatorValueBinding binding = entity.GetComponent<AnimatorValueBinding>();
        ref UiValueField field = ref entity.GetComponent<UiValueField>();

        // A field met for the first time (hidden since the tool opened) has
        // nothing to compare with: it only takes the rig's value. Counting its
        // default as an edit would write the default into the rig.
        if (!_states.TryGetValue(entity.Id, out State? state))
            _states[entity.Id] = state = new State { Shown = (float[])field.Values.Clone() };
        else
            Apply(session, binding, ref field, state);

        field.Mark = AnimatorKeyMarks.For(session, binding.Kind);
        if (field.Live || Typing(field))
            return;

        int count = field.Values.Length;
        Array.Clear(_read);
        if (AnimatorValueFields.TryGet(session, binding, _read))
        {
            for (int i = 0; i < count; i++)
                field.Values[i] = field.Shape.Clamp(_read[i]);
        }
        else
        {
            Array.Copy(field.Defaults, field.Values, count);
        }
        Array.Copy(field.Values, state.Shown, Math.Min(count, state.Shown.Length));
    }

    /// <summary>The user changed the field since it was last shown: write it into the rig.</summary>
    private static void Apply(Entity session, AnimatorValueBinding binding, ref UiValueField field, State state)
    {
        bool changed = false;
        for (int i = 0; i < field.Values.Length && i < state.Shown.Length; i++)
            changed |= !Mathf.IsEqualApprox(field.Values[i], state.Shown[i]);

        float[] values = field.Values;
        bool available = AnimatorValueFields.TryGet(session, binding, new float[4]);
        if (changed && available)
        {
            if (!AnimatorValueFields.Undoable(binding))
                AnimatorValueFields.Write(session, binding, values);
            else if (field.Live)
            {
                if (!state.Dragging)
                {
                    AnimatorHistoryCommands.BeginGesture(session);
                    state.Dragging = true;
                }
                AnimatorHistoryCommands.DuringGesture(session, () => AnimatorValueFields.Write(session, binding, values));
            }
            else if (state.Dragging)
            {
                AnimatorHistoryCommands.DuringGesture(session, () => AnimatorValueFields.Write(session, binding, values));
            }
            else
            {
                AnimatorHistoryCommands.Edit(session, () =>
                {
                    AnimatorValueFields.Write(session, binding, values);
                    AnimatorValueFields.Finish(session, binding);
                });
            }
        }

        if (state.Dragging && !field.Live)
        {
            state.Dragging = false;
            AnimatorHistoryCommands.EndGesture(session, () => AnimatorValueFields.Finish(session, binding));
        }
        Array.Copy(field.Values, state.Shown, Math.Min(field.Values.Length, state.Shown.Length));
    }

    /// <summary>A slot is being typed in: leave the field alone until it lets go.</summary>
    private static bool Typing(in UiValueField field)
    {
        foreach (Entity slot in field.Slots)
        {
            if (!slot.IsNull && slot.Tags.Has<TextFieldFocused>())
                return true;
        }
        return false;
    }
}

/// <summary>
/// Reading and writing what each animator value field is bound to. Writes are
/// plain changes: <see cref="AnimatorValueFieldSystem"/> wraps them in the
/// history, and <see cref="Finish"/> keys or reports once an edit is done.
/// </summary>
public static class AnimatorValueFields
{
    /// <summary>Fills <paramref name="values"/> with the bound value; false when it has nothing to show.</summary>
    public static bool TryGet(Entity session, AnimatorValueBinding binding, float[] values)
    {
        switch (binding.Kind)
        {
            case AnimatorFieldKind.Slider:
                return SliderValue((AnimatorSliderValue)binding.A, session, out values[0]);
            case AnimatorFieldKind.Position or AnimatorFieldKind.Rotation or AnimatorFieldKind.Scale:
                if (!AnimatorTransformCommands.TryTransform(session, out RigTransform transform))
                    return false;
                if (binding.Kind == AnimatorFieldKind.Rotation)
                    values[0] = float.RadiansToDegrees(Mathf.AngleDifference(0f, transform.Rotation));
                else
                {
                    Vector2 v = binding.Kind == AnimatorFieldKind.Position ? transform.Translation : transform.Scale;
                    values[0] = v.X;
                    values[1] = v.Y;
                }
                return true;
            case AnimatorFieldKind.Layer:
                bool has = AnimatorAttachmentCommands.TryGetLayer(session, out int layer);
                values[0] = layer;
                return has;
            case AnimatorFieldKind.Chain:
                return AnimatorChainCommands.TryGet(session, (AnimatorChainCommands.Setting)binding.A, out values[0]) &&
                       (binding.B < 0 || AnimatorChainCommands.TryGet(session, (AnimatorChainCommands.Setting)binding.B, out values[1]));
            case AnimatorFieldKind.Cloth:
                return AnimatorClothCommands.TryGet(session, (AnimatorClothCommands.Setting)binding.A, out values[0]) &&
                       (binding.B < 0 || AnimatorClothCommands.TryGet(session, (AnimatorClothCommands.Setting)binding.B, out values[1]));
            case AnimatorFieldKind.PartColour:
                int part = session.GetComponent<AnimatorSelection>().Attachment;
                RigPackage package = session.GetComponent<AnimatorDocument>().Package;
                if (part < 0 || part >= package.Attachments.Count)
                    return false;
                Color tint = package.Attachments[part].SelfModulate;
                (values[0], values[1], values[2], values[3]) = (tint.R, tint.G, tint.B, tint.A);
                return true;
            case AnimatorFieldKind.EventTime:
                int index = AnimatorEventCommands.Selected(session, out RigClip? clip);
                if (index < 0 || clip is null)
                    return false;
                values[0] = clip.Events[index].Time;
                return true;
        }
        return false;
    }

    /// <summary>Writes the field's values into what it is bound to, without keying or reporting.</summary>
    public static void Write(Entity session, AnimatorValueBinding binding, float[] values)
    {
        switch (binding.Kind)
        {
            case AnimatorFieldKind.Slider:
                SetSlider(session, (AnimatorSliderValue)binding.A, values[0]);
                break;
            case AnimatorFieldKind.Position:
                AnimatorTransformCommands.Quiet(session, t => t with { Translation = new Vector2(values[0], values[1]) }, posesFixed: false);
                break;
            case AnimatorFieldKind.Rotation:
                AnimatorTransformCommands.Quiet(session, t => t with { Rotation = float.DegreesToRadians(values[0]) });
                break;
            case AnimatorFieldKind.Scale:
                var scale = new Vector2(Math.Clamp(values[0], 0.01f, 20f), Math.Clamp(values[1], 0.01f, 20f));
                AnimatorTransformCommands.Quiet(session, t => t with { Scale = scale });
                break;
            case AnimatorFieldKind.Layer:
                AnimatorAttachmentCommands.SetLayer(session, (int)MathF.Round(values[0]));
                break;
            case AnimatorFieldKind.Chain:
                AnimatorChainCommands.Set(session, (AnimatorChainCommands.Setting)binding.A, values[0]);
                if (binding.B >= 0)
                    AnimatorChainCommands.Set(session, (AnimatorChainCommands.Setting)binding.B, values[1]);
                break;
            case AnimatorFieldKind.Cloth:
                AnimatorClothCommands.Set(session, (AnimatorClothCommands.Setting)binding.A, values[0]);
                if (binding.B >= 0)
                    AnimatorClothCommands.Set(session, (AnimatorClothCommands.Setting)binding.B, values[1]);
                break;
            case AnimatorFieldKind.PartColour:
                int part = session.GetComponent<AnimatorSelection>().Attachment;
                RigPackage package = session.GetComponent<AnimatorDocument>().Package;
                if (part >= 0 && part < package.Attachments.Count)
                    package.Attachments[part].SelfModulate = new Color(values[0], values[1], values[2], values[3]);
                break;
            case AnimatorFieldKind.EventTime:
                AnimatorEventCommands.Retime(session, values[0]);
                break;
        }
    }

    /// <summary>Brush settings are the tool's own, not the file's, so they skip the history.</summary>
    public static bool Undoable(AnimatorValueBinding binding) =>
        !(binding.Kind == AnimatorFieldKind.Slider &&
          (AnimatorSliderValue)binding.A is AnimatorSliderValue.BrushSize or AnimatorSliderValue.BrushStrength);

    /// <summary>What a finished edit does last: a pose value is keyed, as dragging a joint is.</summary>
    public static void Finish(Entity session, AnimatorValueBinding binding)
    {
        switch (binding.Kind)
        {
            case AnimatorFieldKind.Slider when (AnimatorSliderValue)binding.A == AnimatorSliderValue.Depth &&
                                               !session.GetComponent<AnimatorModes>().Rest:
                AnimatorEditing.KeyEdit(session);
                break;
            case AnimatorFieldKind.Position:
                AnimatorTransformCommands.FinishDrag(session, "POSITION SET");
                break;
            case AnimatorFieldKind.Rotation:
                AnimatorTransformCommands.FinishDrag(session, "ROTATION SET");
                break;
            case AnimatorFieldKind.Scale:
                AnimatorTransformCommands.FinishDrag(session, "SCALE SET");
                break;
        }
    }

    private static void SetSlider(Entity session, AnimatorSliderValue kind, float value)
    {
        switch (kind)
        {
            case AnimatorSliderValue.Depth:
                AnimatorEditing.SetDepth(session, value);
                break;
            case AnimatorSliderValue.LimitMin:
                AnimatorEditing.SetLimit(session, max: false, float.DegreesToRadians(value));
                break;
            case AnimatorSliderValue.LimitMax:
                AnimatorEditing.SetLimit(session, max: true, float.DegreesToRadians(value));
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
                AnimatorTargetCommands.SetShape(session, "tilt", float.DegreesToRadians(value));
                break;
            case AnimatorSliderValue.TargetStart:
                AnimatorTargetCommands.SetShape(session, "start", float.DegreesToRadians(value));
                break;
            case AnimatorSliderValue.TargetSweep:
                AnimatorTargetCommands.SetShape(session, "sweep", float.DegreesToRadians(value));
                break;
            case AnimatorSliderValue.TransitionFade:
                AnimatorFlowCommands.SetFade(session, value);
                break;
        }
    }

    /// <summary>The bound value in the slider's units, if what it applies to is selected.</summary>
    private static bool SliderValue(AnimatorSliderValue kind, Entity session, out float value)
    {
        value = 0f;
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        RigPackage package = document.Package;
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
                value = float.RadiansToDegrees(kind == AnimatorSliderValue.LimitMin
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
                if (effect < 0 || effect >= document.Runtime.Pose.AttachmentStates.Length)
                    return false;
                value = AnimatorEffectCommands.Opacity(document, effect) * 100f;
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
            AnimatorSliderValue.TargetTilt => float.RadiansToDegrees(Mathf.AngleDifference(0f, target.Tilt)),
            AnimatorSliderValue.TargetStart => float.RadiansToDegrees(Mathf.AngleDifference(0f, target.StartAngle)),
            _ => float.RadiansToDegrees(target.Sweep),
        };
        return true;
    }
}
