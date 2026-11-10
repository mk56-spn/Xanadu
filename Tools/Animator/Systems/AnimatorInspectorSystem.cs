using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// The floating transform inspector over the preview. It shows the selected
/// bone's or attachment's position, rotation (in degrees) and scale, applies
/// typed values through <see cref="AnimatorTransformCommands"/>, and flags a
/// pose that no key holds so scrubbing cannot silently throw it away.
/// </summary>
public sealed class AnimatorInspectorSystem : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private enum Field
    {
        PosX,
        PosY,
        Rotation,
        ScaleX,
        ScaleY,
        Layer,
    }

    private static readonly Color NormalTitle = Palette.Accent;
    private static readonly Color UnkeyedTitle = Palette.Warning;

    private readonly bool[] _wasFocused = new bool[6];

    /// <summary>What each slider was last set to from the selection: anything else is the user moving it.</summary>
    private readonly float[] _sliderShown = new float[2];

    /// <summary>Whether each slider is mid-drag, with a gesture open for its one undo step.</summary>
    private readonly bool[] _sliderDragging = new bool[2];
    private int _editBone = -1;
    private int _editAttachment = -1;
    private int _editEffect = -1;
    private int _editExtras;
    private bool _editRest;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Update(session);
    }

    private void Update(Entity session)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        if (ui.InspectorTitle.IsNull)
            return;

        Sync(session, ui, ui.InspectPosX, Field.PosX);
        Sync(session, ui, ui.InspectPosY, Field.PosY);
        Sync(session, ui, ui.InspectRotation, Field.Rotation);
        Sync(session, ui, ui.InspectScaleX, Field.ScaleX);
        Sync(session, ui, ui.InspectScaleY, Field.ScaleY);
        Sync(session, ui, ui.InspectLayer, Field.Layer);
        SyncSlider(session, ui.InspectRotationSlider, 0);
        SyncSlider(session, ui.InspectScaleSlider, 1);
        Title(session, ui.InspectorTitle, ui.InspectorSubtitle);
    }

    private void Sync(Entity session, AnimatorUi ui, Entity field, Field kind)
    {
        if (field.IsNull || !field.HasComponent<TextField>())
            return;

        bool focused = field.Tags.Has<TextFieldFocused>();
        ref TextField text = ref field.GetComponent<TextField>();
        int index = (int)kind;

        if (focused && !_wasFocused[index])
            RememberTarget(session);
        else if (!focused && _wasFocused[index])
            Commit(session, kind, text.Value);

        if (!focused)
            text.Value = Format(session, kind);

        _wasFocused[index] = focused;
    }

    /// <summary>
    /// Applies a slider the user moved (slot 0 turns, slot 1 scales), then puts it
    /// back on the selection's value. A drag is one undo step and keys once, on
    /// release; a click or right-click reset is an ordinary edit.
    /// </summary>
    private void SyncSlider(Entity session, Entity track, int slot)
    {
        if (track.IsNull || !track.HasComponent<UiSlider>())
            return;

        ref UiSlider slider = ref track.GetComponent<UiSlider>();
        bool held = track.Tags.Has<Pressed>();
        bool moved = !Mathf.IsEqualApprox(slider.Value, _sliderShown[slot]);
        bool rotation = slot == 0;
        string label = rotation ? "ROTATION SET" : "SCALE SET";

        if (moved && TryTransform(session, out _))
        {
            if (held)
            {
                if (!_sliderDragging[slot])
                {
                    AnimatorHistoryCommands.BeginGesture(session);
                    _sliderDragging[slot] = true;
                }

                if (rotation)
                    AnimatorTransformCommands.DragRotation(session, Mathf.DegToRad(slider.Value));
                else
                    AnimatorTransformCommands.DragScale(session, slider.Value);
            }
            else if (rotation)
            {
                AnimatorTransformCommands.SetRotation(session, Mathf.DegToRad(slider.Value));
            }
            else
            {
                AnimatorTransformCommands.SetScaleProportional(session, slider.Value);
            }
        }

        if (_sliderDragging[slot] && !held)
        {
            _sliderDragging[slot] = false;
            AnimatorHistoryCommands.EndGesture(session,
                () => AnimatorTransformCommands.FinishDrag(session, label));
        }

        float shown = TryTransform(session, out XafTransform transform)
            ? rotation
                ? Mathf.RadToDeg(Mathf.AngleDifference(0f, transform.Rotation))
                : transform.Scale.X
            : rotation ? 0f : 1f;
        slider.Value = Mathf.Clamp(shown, slider.Min, slider.Max);
        _sliderShown[slot] = slider.Value;
    }

    private void RememberTarget(Entity session)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        _editBone = selection.Bone;
        _editAttachment = selection.Attachment;
        _editEffect = selection.Effect;
        _editExtras = selection.ExtraBones?.Count ?? 0;
        _editRest = session.GetComponent<AnimatorModes>().Rest;
    }

    /// <summary>Applies a typed value, unless the selection changed while the field was focused.</summary>
    private void Commit(Entity session, Field kind, string typed)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        bool targetChanged = selection.Bone != _editBone ||
                             selection.Attachment != _editAttachment ||
                             selection.Effect != _editEffect ||
                             (selection.ExtraBones?.Count ?? 0) != _editExtras ||
                             session.GetComponent<AnimatorModes>().Rest != _editRest;
        if (targetChanged)
            return;

        if (!float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            AnimatorSession.SetStatus(session, "NOT A NUMBER");
            return;
        }

        if (kind == Field.Layer)
        {
            AnimatorAttachmentCommands.SetLayer(session, (int)Mathf.Round(value));
            return;
        }

        Vector2 translation = Translation(session);
        Vector2 scale = Scale(session);
        switch (kind)
        {
            case Field.PosX:
                AnimatorTransformCommands.SetTranslation(
                    session, new Vector2(value, translation.Y));
                break;
            case Field.PosY:
                AnimatorTransformCommands.SetTranslation(
                    session, new Vector2(translation.X, value));
                break;
            case Field.Rotation:
                AnimatorTransformCommands.SetRotation(session, Mathf.DegToRad(value));
                break;
            case Field.ScaleX:
                AnimatorTransformCommands.SetScale(session, new Vector2(value, scale.Y));
                break;
            case Field.ScaleY:
                AnimatorTransformCommands.SetScale(session, new Vector2(scale.X, value));
                break;
        }
    }

    private static string Format(Entity session, Field kind)
    {
        if (kind == Field.Layer)
            return AnimatorAttachmentCommands.TryGetLayer(session, out int layer) ? layer.ToString(CultureInfo.InvariantCulture) : "";

        if (!TryTransform(session, out XafTransform transform))
            return "";

        return kind switch
        {
            Field.Rotation => Number(Mathf.RadToDeg(transform.Rotation)),
            Field.ScaleX => Number(transform.Scale.X),
            Field.ScaleY => Number(transform.Scale.Y),
            Field.PosX => Number(transform.Translation.X),
            _ => Number(transform.Translation.Y),
        };
    }

    /// <summary>Trims trailing zeros; a value rounded to zero never prints as "-0".</summary>
    private static string Number(float value)
    {
        float rounded = Mathf.IsZeroApprox(value) ? 0f : value;
        return rounded.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static Vector2 Translation(Entity session) =>
        TryTransform(session, out XafTransform transform) ? transform.Translation : Vector2.Zero;

    private static Vector2 Scale(Entity session) =>
        TryTransform(session, out XafTransform transform) ? transform.Scale : Vector2.One;

    /// <summary>The selected element's transform in the mode being edited, if any.</summary>
    private static bool TryTransform(Entity session, out XafTransform transform)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        bool rest = session.GetComponent<AnimatorModes>().Rest;
        transform = XafTransform.Identity;

        if (selection.Bone >= 0 && selection.Bone < document.Package.Skeleton.Bones.Count)
        {
            transform = rest
                ? XafTransform.From(document.Package.Skeleton.Bones[selection.Bone].Rest)
                : document.Runtime.Pose.Bones[selection.Bone];
            return true;
        }

        if (selection.Attachment >= 0 && selection.Attachment < document.Package.Attachments.Count)
        {
            transform = rest
                ? XafTransform.From(document.Package.Attachments[selection.Attachment].Transform)
                : document.Runtime.Pose.Attachments[selection.Attachment];
            return true;
        }

        if (selection.Effect >= 0 && selection.Effect < document.Package.Effects.Count &&
            selection.Effect < document.Runtime.Pose.Effects.Length)
        {
            transform = rest
                ? XafTransform.From(document.Package.Effects[selection.Effect].Transform)
                : document.Runtime.Pose.Effects[selection.Effect].Transform;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Names the selection large, or says MULTIPLE when more than one bone is
    /// picked; the line under it says what kind it is and whether its pose is keyed.
    /// </summary>
    private static void Title(Entity session, Entity title, Entity subtitle)
    {
        if (!title.HasComponent<Text>() || !title.HasComponent<TextStyle>())
            return;

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        AnimatorModes modes = session.GetComponent<AnimatorModes>();

        string name;
        string kind;
        bool unkeyed = false;
        int extras = selection.ExtraBones?.Count ?? 0;
        if (selection.Bone >= 0 && selection.Bone < document.Package.Skeleton.Bones.Count)
        {
            XafBone bone = document.Package.Skeleton.Bones[selection.Bone];
            unkeyed = AnimatorUnkeyed.IsBone(session, selection.Bone);
            name = extras > 0 ? "MULTIPLE" : bone.Id;
            kind = extras > 0 ? $"{extras + 1} BONES" : "BONE";
        }
        else if (selection.Attachment >= 0 &&
                 selection.Attachment < document.Package.Attachments.Count)
        {
            XafAttachment attachment = document.Package.Attachments[selection.Attachment];
            unkeyed = AnimatorUnkeyed.IsAttachment(session, selection.Attachment);
            name = attachment.Group;
            kind = "SVG";
        }
        else if (selection.Effect >= 0 && selection.Effect < document.Package.Effects.Count &&
                 selection.Effect < document.Runtime.Pose.Effects.Length)
        {
            XafEffect effect = document.Package.Effects[selection.Effect];
            XafEffectState state = document.Runtime.Pose.Effects[selection.Effect];
            name = effect.Id;
            kind = "EFFECT  " + (state.Visible ? $"{state.Opacity * 100f:0}%" : "HIDDEN");
        }
        else
        {
            UiText.Set(title, "NOTHING SELECTED");
            UiText.SetColor(title, Palette.TextDim);
            UiText.Set(subtitle, "CLICK A JOINT, OR PICK FROM THE LIST");
            return;
        }

        string status = modes.Rest ? "  \u00b7  REST POSE" : unkeyed ? "  \u00b7  UNKEYED: K KEYS IT" : "";
        UiText.Set(title, name);
        UiText.SetColor(title, unkeyed ? UnkeyedTitle : NormalTitle);
        UiText.Set(subtitle, kind + status);
        UiText.SetColor(subtitle, unkeyed ? UnkeyedTitle : Palette.TextDim);
    }
}
