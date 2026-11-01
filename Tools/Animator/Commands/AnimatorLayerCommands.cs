using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>One piece of art in the layer stack.</summary>
public readonly record struct AnimatorLayerRow(bool IsCloth, int Index, int Layer, string Label)
{
    /// <summary>Names the row in a <see cref="ToolAction"/>'s argument.</summary>
    public string Argument => (IsCloth ? "c" : "a") + Index;
}

/// <summary>The layer stack: all the art in draw order, and moving it through that order.</summary>
public static class AnimatorLayerCommands
{
    /// <summary>
    /// Attachments and cloth as they draw at the playhead, front first. On one
    /// layer, cloth draws over attachments and later entries over earlier ones.
    /// </summary>
    public static List<AnimatorLayerRow> Rows(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        XafPackage package = document.Package;
        var rows = new List<AnimatorLayerRow>(package.Attachments.Count + package.Cloths.Count);
        for (int i = 0; i < package.Attachments.Count; i++)
        {
            XafAttachment attachment = package.Attachments[i];
            rows.Add(new AnimatorLayerRow(false, i, Showing(document, i).LayerOf(attachment), attachment.Group));
        }

        for (int i = 0; i < package.Cloths.Count; i++)
            rows.Add(new AnimatorLayerRow(true, i, package.Cloths[i].ZOrder, package.Cloths[i].Id));

        rows.Sort(static (a, b) =>
        {
            int layer = b.Layer.CompareTo(a.Layer);
            if (layer != 0)
                return layer;
            return a.IsCloth != b.IsCloth ? (a.IsCloth ? -1 : 1) : b.Index.CompareTo(a.Index);
        });
        return rows;
    }

    /// <summary>
    /// Moves a row one place towards the front (+1) or back (-1), as one undo step,
    /// and selects it. It swaps layers with its neighbour, or steps past a neighbour
    /// on the same layer. With no argument, as from a shortcut, it moves the selection.
    /// </summary>
    public static void Move(Entity session, string argument, int direction) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            List<AnimatorLayerRow> rows = Rows(session);
            string target = argument.Length > 0 ? argument : SelectedArgument(session);
            int at = rows.FindIndex(row => row.Argument == target);
            int next = at - direction;
            if (at < 0 || next < 0 || next >= rows.Count)
                return;

            AnimatorLayerRow row = rows[at];
            AnimatorLayerRow neighbour = rows[next];
            if (neighbour.Layer == row.Layer)
            {
                SetLayer(session, row, row.Layer + direction);
            }
            else
            {
                SetLayer(session, neighbour, row.Layer);
                SetLayer(session, row, neighbour.Layer);
            }

            AnimatorSession.RigChanged(session);
            ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
            if (row.IsCloth)
                selection.SelectOnlyCloth(row.Index);
            else
                selection.SelectOnlyAttachment(row.Index);
            AnimatorSession.SetStatus(session, $"MOVED {row.Label} {(direction > 0 ? "FORWARD" : "BACK")}");
        });

    private static string SelectedArgument(Entity session)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        if (selection.Attachment >= 0)
            return "a" + selection.Attachment;
        return selection.Cloth >= 0 ? "c" + selection.Cloth : "";
    }

    /// <summary>A layer keyed in the open clip is keyed again, as its base layer would not show.</summary>
    private static void SetLayer(Entity session, in AnimatorLayerRow row, int layer)
    {
        layer = System.Math.Clamp(layer, XafAttachment.MinLayer, XafAttachment.MaxLayer);
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        XafPackage package = document.Package;
        if (row.IsCloth)
        {
            package.Cloths[row.Index].ZOrder = layer;
            return;
        }

        if (!Showing(document, row.Index).HasLayer ||
            AnimatorSession.CurrentClip(document) is not { } clip)
        {
            package.Attachments[row.Index].ZOrder = layer;
            return;
        }

        document.Runtime.SetPaused(true);
        float time = document.Runtime.CurrentTime;
        XafAttachmentAuthoring.KeyLayer(package, clip, row.Index, time, layer);
        document.Runtime.Seek(time);
    }

    private static XafAttachmentState Showing(in AnimatorDocument document, int index)
    {
        XafPose pose = document.Runtime.Pose;
        return index < pose.AttachmentStates.Length ? pose.AttachmentStates[index] : default;
    }
}
