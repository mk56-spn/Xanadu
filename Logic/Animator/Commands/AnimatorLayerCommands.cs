using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Layering;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>One piece of art in the layer stack.</summary>
public readonly record struct AnimatorLayerRow(bool IsCloth, int Index, int Layer, string Label)
{
    /// <summary>Names the row in a <see cref="MoveLayer"/> intent's row: a part's index + 1, a cloth's -(index + 1); never 0.</summary>
    public int Key => IsCloth ? -1 - Index : Index + 1;

    /// <summary>Where the row draws. On one layer, cloth draws over attachments.</summary>
    public DrawSlot Slot => AnimatorDrawOrder.Slot(Layer, IsCloth, Index);
}

/// <summary>The layer stack: all the art in draw order, and moving it through that order.</summary>
public static class AnimatorLayerCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<MoveLayer>((s, e) => Move(s, e.Row, e.By));
    }

    /// <summary>
    /// Attachments and cloth as they draw at the playhead, front first. On one
    /// layer, cloth draws over attachments and later entries over earlier ones.
    /// </summary>
    public static List<AnimatorLayerRow> Rows(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        RigPackage package = document.Package;
        var rows = new List<AnimatorLayerRow>(package.Attachments.Count + package.Cloths.Count);
        for (int i = 0; i < package.Attachments.Count; i++)
        {
            RigAttachment attachment = package.Attachments[i];
            rows.Add(new AnimatorLayerRow(false, i, Showing(document, i).LayerOf(attachment), attachment.Id));
        }

        for (int i = 0; i < package.Cloths.Count; i++)
            rows.Add(new AnimatorLayerRow(true, i, package.Cloths[i].ZOrder, package.Cloths[i].Id));

        DrawStack.SortFrontFirst(rows, static row => row.Slot);
        return rows;
    }

    /// <summary>
    /// Moves a row one place towards the front (+1) or back (-1), as one undo step,
    /// and selects it. It swaps layers with its neighbour, or steps past a neighbour
    /// on the same layer. With key 0, as from a shortcut, it moves the selection.
    /// </summary>
    public static void Move(Entity session, int key, int direction) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            List<AnimatorLayerRow> rows = Rows(session);
            int target = key != 0 ? key : SelectedKey(session);
            int at = rows.FindIndex(row => row.Key == target);
            int next = DrawStack.Neighbour(at, direction, rows.Count);
            if (next < 0)
                return;

            AnimatorLayerRow row = rows[at];
            AnimatorLayerRow neighbour = rows[next];
            if (!DrawStack.TryMove(row.Slot, neighbour.Slot, direction, DrawStackRule.Layers,
                    out DrawSlot movedRow, out DrawSlot movedNeighbour))
                return;

            // Only what changed is set: setting a keyed layer keys it again.
            if (movedNeighbour.Layer != neighbour.Layer)
                SetLayer(session, neighbour, movedNeighbour.Layer);
            SetLayer(session, row, movedRow.Layer);

            AnimatorSession.RigChanged(session);
            ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
            if (row.IsCloth)
                selection.SelectOnlyCloth(row.Index);
            else
                selection.SelectOnlyAttachment(row.Index);
            AnimatorSession.SetStatus(session, $"MOVED {row.Label} {(direction > 0 ? "FORWARD" : "BACK")}");
        });

    private static int SelectedKey(Entity session)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        if (selection.Attachment >= 0)
            return new AnimatorLayerRow(false, selection.Attachment, 0, "").Key;
        return selection.Cloth >= 0 ? new AnimatorLayerRow(true, selection.Cloth, 0, "").Key : 0;
    }

    /// <summary>A layer keyed in the open clip is keyed again, as its base layer would not show.</summary>
    private static void SetLayer(Entity session, in AnimatorLayerRow row, int layer)
    {
        layer = DrawOrder.ClampLayer(layer);
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        RigPackage package = document.Package;
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
        RigAttachmentAuthoring.KeyLayer(package, clip, row.Index, time, layer);
        document.Runtime.Seek(time);
    }

    private static RigAttachmentState Showing(in AnimatorDocument document, int index)
    {
        RigPose pose = document.Runtime.Pose;
        return index < pose.AttachmentStates.Length ? pose.AttachmentStates[index] : default;
    }
}
