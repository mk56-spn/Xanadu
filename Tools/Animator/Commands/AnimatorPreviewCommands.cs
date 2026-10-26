using Friflo.Engine.ECS;
using Godot;
using Writer.Animation;

namespace Writer.Tools.Animator.Commands;

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
        XafPose pose = AnimatorGeometry.PreviewPose(document, modes);
        Transform2D[] bones = XafRig.WorldBones(document.Package, pose);
        Transform2D[] attachments = XafRig.WorldAttachments(document.Package, pose, bones);

        if (bones.Length == 0 && attachments.Length == 0)
            return false;

        Rect2 bounds = Bounds(bones, attachments).Grow(FitMargin);
        ref AnimatorPreviewView view = ref session.GetComponent<AnimatorPreviewView>();
        view.Fit(bounds, area);
        AnimatorSession.SetStatus(session, $"FIT VIEW  x{view.Zoom:0.0}");
        return true;
    }

    /// <summary>Back to the default framing: one skeleton unit per pixel, centred.</summary>
    public static void Reset(Entity session)
    {
        ref AnimatorPreviewView view = ref session.GetComponent<AnimatorPreviewView>();
        view = AnimatorPreviewView.Default;
        AnimatorSession.SetStatus(session, "VIEW 1:1");
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
            left = Mathf.Min(left, point.X);
            top = Mathf.Min(top, point.Y);
            right = Mathf.Max(right, point.X);
            bottom = Mathf.Max(bottom, point.Y);
        }
    }
}
