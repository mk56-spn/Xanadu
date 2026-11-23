using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Animation;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
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

        Title(session, ui.InspectorTitle, ui.InspectorSubtitle);
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
            RigBone bone = document.Package.Skeleton.Bones[selection.Bone];
            unkeyed = AnimatorUnkeyed.IsBone(session, selection.Bone);
            name = extras > 0 ? "MULTIPLE" : bone.Id;
            kind = extras > 0 ? $"{extras + 1} BONES" : "BONE";
        }
        else if (selection.Attachment >= 0 &&
                 selection.Attachment < document.Package.Attachments.Count)
        {
            RigAttachment attachment = document.Package.Attachments[selection.Attachment];
            unkeyed = AnimatorUnkeyed.IsAttachment(session, selection.Attachment);
            name = attachment.Id;
            RigAttachmentState state = selection.Attachment < document.Runtime.Pose.AttachmentStates.Length
                ? document.Runtime.Pose.AttachmentStates[selection.Attachment]
                : default;
            kind = state.Alpha <= 0f ? "PART  HIDDEN" : state.Alpha < 1f ? $"PART  {state.Alpha * 100f:0}%" : "PART";
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
