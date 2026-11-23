using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Ui;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>Preview framing: fit the whole rig in the panel, or go back to one unit per pixel.</summary>
public static class AnimatorPreviewCommands
{
    /// <summary>Extra skeleton-space room around the rig, so joints and labels do not touch the edges.</summary>
    public const float FitMargin = 30f;

    /// <summary>
    /// Frames every joint and attachment in the preview. False while the panel
    /// has not been laid out yet, so a caller can retry on the next frame.
    /// </summary>
    public static bool Fit(Entity session)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        if (ui.Preview.IsNull)
            return false;

        Rect2 area = AnimatorGeometry.PanelArea(ui.Preview);
        if (area.Size.X < 8f || area.Size.Y < 8f)
            return false;

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorModes modes = session.GetComponent<AnimatorModes>();
        RigPose pose = AnimatorGeometry.PreviewPose(document, modes);
        Transform2D[] bones = RigRig.WorldBones(document.Package, pose);
        Transform2D[] attachments = RigRig.WorldAttachments(document.Package, pose, bones);

        if (bones.Length == 0 && attachments.Length == 0)
            return false;

        // With something selected, F frames that (and the bones it hangs off
        // into); with nothing, the whole rig.
        Rect2? selected = SelectionBounds(document.Package, session.GetComponent<AnimatorSelection>(), bones, attachments);
        Rect2 bounds = (selected ?? Bounds(bones, attachments)).Grow(FitMargin);
        ref AnimatorPreviewView view = ref session.GetComponent<AnimatorPreviewView>();
        view.Fit(bounds, area);
        AnimatorSession.SetStatus(session, selected is null ? $"FIT VIEW  x{view.Zoom:0.0}" : $"FIT SELECTION  x{view.Zoom:0.0}");
        return true;
    }

    /// <summary>Back to the default framing: one skeleton unit per pixel, centred.</summary>
    public static void Reset(Entity session)
    {
        ref AnimatorPreviewView view = ref session.GetComponent<AnimatorPreviewView>();
        view = AnimatorPreviewView.Default;
        AnimatorSession.SetStatus(session, "VIEW 1:1");
    }

    /// <summary>
    /// The smallest framed span when fitting a selection, in skeleton units,
    /// so one joint does not zoom all the way in.
    /// </summary>
    public const float MinSelectionSpan = 120f;

    /// <summary>
    /// Bounds of the selected bones (each joint and its children's joints, so
    /// the bone's length shows) and the selected part; null with nothing selected.
    /// </summary>
    private static Rect2? SelectionBounds(RigPackage package, AnimatorSelection selection, Transform2D[] bones, Transform2D[] attachments)
    {
        var points = new List<Vector2>();
        foreach (int bone in selection.Bones())
        {
            if (bone < 0 || bone >= bones.Length)
                continue;
            points.Add(bones[bone].Origin);
            for (int i = 0; i < package.Skeleton.Bones.Count && i < bones.Length; i++)
            {
                if (package.Skeleton.Bones[i].Parent == bone)
                    points.Add(bones[i].Origin);
            }
        }
        if (selection.Attachment >= 0 && selection.Attachment < attachments.Length)
            points.Add(attachments[selection.Attachment].Origin);
        if (points.Count == 0)
            return null;

        Vector2 min = points[0], max = points[0];
        foreach (Vector2 point in points)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        Vector2 size = Vector2.Max(max - min, new Vector2(MinSelectionSpan));
        Vector2 centre = (min + max) / 2;
        return new Rect2(centre - size / 2, size);
    }

    /// <summary>Skeleton-space bounds of the joints and attachment origins.</summary>
    private static Rect2 Bounds(Transform2D[] bones, Transform2D[] attachments)
    {
        bool any = false;
        float left = float.MaxValue;
        float top = float.MaxValue;
        float right = float.MinValue;
        float bottom = float.MinValue;

        foreach (Transform2D bone in bones)
            Include(bone.Origin);
        foreach (Transform2D attachment in attachments)
            Include(attachment.Origin);

        return any
            ? new Rect2(new Vector2(left, top), new Vector2(right - left, bottom - top))
            : new Rect2();

        void Include(Vector2 point)
        {
            any = true;
            left = Math.Min(left, point.X);
            top = Math.Min(top, point.Y);
            right = Math.Max(right, point.X);
            bottom = Math.Max(bottom, point.Y);
        }
    }
}
