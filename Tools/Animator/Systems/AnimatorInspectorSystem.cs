using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Animation;
using Writer.Ecs;
using Writer.Tools.Animator.Commands;

namespace Writer.Tools.Animator.Systems;

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

    private static readonly Color NormalTitle = new("#facc15");
    private static readonly Color UnkeyedTitle = new("#fb923c");

    private readonly bool[] _wasFocused = new bool[6];
    private int _editBone = -1;
    private int _editAttachment = -1;
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
        Title(session, ui.InspectorTitle);
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

    private void RememberTarget(Entity session)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        _editBone = selection.Bone;
        _editAttachment = selection.Attachment;
        _editExtras = selection.ExtraBones?.Count ?? 0;
        _editRest = session.GetComponent<AnimatorModes>().Rest;
    }

    /// <summary>Applies a typed value, unless the selection changed while the field was focused.</summary>
    private void Commit(Entity session, Field kind, string typed)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        bool targetChanged = selection.Bone != _editBone ||
                             selection.Attachment != _editAttachment ||
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

        return false;
    }

    /// <summary>Names the selection and says when the pose is not on a key.</summary>
    private static void Title(Entity session, Entity title)
    {
        if (!title.HasComponent<Text>() || !title.HasComponent<TextStyle>())
            return;

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        AnimatorModes modes = session.GetComponent<AnimatorModes>();

        string value;
        bool unkeyed = false;
        int extras = selection.ExtraBones?.Count ?? 0;
        if (selection.Bone >= 0 && selection.Bone < document.Package.Skeleton.Bones.Count)
        {
            XafBone bone = document.Package.Skeleton.Bones[selection.Bone];
            unkeyed = !modes.Rest && IsUnkeyed(session, bone.Id, null);
            value = bone.Id +
                    (extras > 0 ? $"  +{extras}" : "") +
                    (modes.Rest ? "  REST" : unkeyed ? "  UNKEYED" : "");
        }
        else if (selection.Attachment >= 0 &&
                 selection.Attachment < document.Package.Attachments.Count)
        {
            XafAttachment attachment = document.Package.Attachments[selection.Attachment];
            unkeyed = !modes.Rest && IsUnkeyed(session, null, attachment.Group);
            value = attachment.Group + "  SVG" + (modes.Rest ? " REST" : unkeyed ? "  UNKEYED" : "");
        }
        else
        {
            value = "NOTHING SELECTED";
        }

        ref Text text = ref title.GetComponent<Text>();
        if (text.Value != value)
        {
            text.Value = value;
            title.AddTag<Dirty>();
        }

        ref TextStyle style = ref title.GetComponent<TextStyle>();
        Color color = unkeyed ? UnkeyedTitle : NormalTitle;
        if (style.Color != color)
        {
            style.Color = color;
            title.AddTag<Dirty>();
        }
    }

    /// <summary>Whether the shown pose differs from what the current clip samples.</summary>
    private static bool IsUnkeyed(Entity session, string? boneId, string? group)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        XafClip? clip = AnimatorSession.CurrentClip(document);
        if (clip is null)
            return false;

        XafPose sampled = XafSampler.Sample(
            document.Package, clip, document.Runtime.CurrentTime);
        if (boneId is not null)
        {
            int bone = XafRig.FindBone(document.Package, boneId);
            return bone >= 0 && !Near(
                document.Runtime.Pose.Bones[bone], sampled.Bones[bone]);
        }

        for (int i = 0; i < document.Package.Attachments.Count; i++)
        {
            if (document.Package.Attachments[i].Group != group)
                continue;
            return !Near(document.Runtime.Pose.Attachments[i], sampled.Attachments[i]);
        }
        return false;
    }

    private static bool Near(XafTransform a, XafTransform b) =>
        a.Translation.DistanceSquaredTo(b.Translation) < 0.0001f &&
        Mathf.Abs(a.Rotation - b.Rotation) < 0.001f &&
        a.Scale.DistanceSquaredTo(b.Scale) < 0.0001f;
}
