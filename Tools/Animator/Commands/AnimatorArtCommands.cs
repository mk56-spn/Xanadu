using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// The ART section's buttons, for the selected attachment: alternate art and
/// which art shows, its keyed layer and visibility, and its mesh.
/// </summary>
public static class AnimatorArtCommands
{
    /// <summary>Opens the SVG picker; the row chosen becomes new art the selected attachment can switch to.</summary>
    public static void OpenArtPicker(Entity session)
    {
        if (Selected(session) < 0)
        {
            AnimatorSession.SetStatus(session, "SELECT AN SVG FIRST");
            return;
        }

        ref AnimatorSvgMenu menu = ref session.GetComponent<AnimatorSvgMenu>();
        menu.Open = true;
        menu.ForChain = false;
        menu.ForArt = true;
        menu.Anchor = AnimatorLayout.ChainPickerAnchor(session.GetComponent<AnimatorUi>().ScreenSize);
    }

    public static void AddArt(Entity session, AnimatorSvgChoice choice)
    {
        ref AnimatorSvgMenu menu = ref session.GetComponent<AnimatorSvgMenu>();
        menu.Open = false;
        menu.ForArt = false;
        RigEdit(session, (package, index) =>
        {
            int art = XafAttachmentAuthoring.AddAlternate(package, index, choice.Path, choice.Group);
            return art < 0 ? "" : $"{package.Attachments[index].Group} CAN NOW SHOW {choice.Group} (ART {art})";
        });
    }

    /// <summary>Removes the art showing at the playhead, if it is an alternate.</summary>
    public static void DropArt(Entity session) =>
        RigEdit(session, (package, index) =>
        {
            int art = Showing(session, index).Art;
            return art >= 1 && XafAttachmentAuthoring.RemoveAlternate(package, index, art)
                ? $"DROPPED ART {art} OF {package.Attachments[index].Group}"
                : "SHOW AN ALTERNATE ART FIRST, THEN DROP IT";
        });

    /// <summary>Keys the next or previous art at the playhead, wrapping round.</summary>
    public static void StepArt(Entity session, int step) =>
        Key(session, (package, clip, index, time) =>
        {
            XafAttachment attachment = package.Attachments[index];
            if (attachment.ArtCount < 2)
                return $"{attachment.Group} HAS NO OTHER ART · ADD SOME WITH + ART";

            int art = ((Showing(session, index).Art + step) % attachment.ArtCount + attachment.ArtCount) %
                      attachment.ArtCount;
            XafAttachmentAuthoring.KeyArt(package, clip, index, time, art);
            return $"{attachment.Group} SHOWS {attachment.Art(art).Group} FROM {time:0.00} S";
        });

    public static void StepLayer(Entity session, int step) =>
        Key(session, (package, clip, index, time) =>
        {
            XafAttachment attachment = package.Attachments[index];
            int layer = Showing(session, index).LayerOf(attachment) + step;
            XafAttachmentAuthoring.KeyLayer(package, clip, index, time, layer);
            return $"{attachment.Group} ON LAYER {layer} FROM {time:0.00} S";
        });

    public static void ToggleVisible(Entity session) =>
        Key(session, (package, clip, index, time) =>
        {
            bool hidden = Showing(session, index).Hidden;
            XafAttachmentAuthoring.KeyVisible(package, clip, index, time, hidden);
            return $"{package.Attachments[index].Group} {(hidden ? "SHOWN" : "HIDDEN")} FROM {time:0.00} S";
        });

    public static void ToggleMesh(Entity session) =>
        RigEdit(session, (package, index) => XafAttachmentAuthoring.ToggleMesh(package, index)
            ? $"{package.Attachments[index].Group} IS A MESH · IT BENDS WITH ITS BONE AND THE ONES BELOW"
            : $"{package.Attachments[index].Group} IS RIGID AGAIN");

    public static void StepDetail(Entity session, int step) =>
        RigEdit(session, (package, index) =>
            package.Attachments[index].Mesh is { } mesh &&
            XafAttachmentAuthoring.SetDetail(package, index, mesh.Detail + step)
                ? $"MESH DETAIL {mesh.Detail}"
                : "TURN MESH ON FIRST");

    public static void StepBlend(Entity session, int step) =>
        RigEdit(session, (package, index) =>
            package.Attachments[index].Mesh is { } mesh &&
            XafAttachmentAuthoring.SetBlend(package, index, mesh.Blend + step * XafAttachmentAuthoring.BlendStep)
                ? $"MESH BLEND {mesh.Blend:0} PX"
                : "TURN MESH ON FIRST");

    /// <summary>Starts or stops picking the bones that bend the selected mesh by clicking them.</summary>
    public static void TogglePickBones(Entity session)
    {
        ref AnimatorMeshPick pick = ref session.GetComponent<AnimatorMeshPick>();
        int index = Selected(session);
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (!pick.Active && (index < 0 || package.Attachments[index].Mesh is null))
        {
            AnimatorSession.SetStatus(session, "SELECT A MESHED SVG FIRST");
            return;
        }

        pick.Active = !pick.Active;
        pick.Attachment = index;
        AnimatorSession.SetStatus(session, pick.Active
            ? "CLICK BONES TO LET THEM BEND THE MESH, OR STOP · CLICK NOTHING TO FINISH"
            : "DONE PICKING MESH BONES");
    }

    /// <summary>A click in the preview while picking: toggles the bone under it, or finishes on nothing.</summary>
    public static void PickBone(Entity session, int bone)
    {
        ref AnimatorMeshPick pick = ref session.GetComponent<AnimatorMeshPick>();
        int index = pick.Attachment;
        if (bone < 0)
        {
            pick.Active = false;
            AnimatorSession.SetStatus(session, "DONE PICKING MESH BONES");
            return;
        }

        RigEdit(session, (package, _) =>
        {
            string id = package.Skeleton.Bones[bone].Id;
            if (!XafAttachmentAuthoring.ToggleMeshBone(package, index, bone))
                return $"{id} IS THE SVG'S OWN BONE · IT ALWAYS BENDS IT";
            return package.Attachments[index].Mesh!.Bones.Contains(id)
                ? $"{id} NOW BENDS THE MESH"
                : $"{id} NO LONGER BENDS THE MESH";
        }, index);
    }

    private static int Selected(Entity session)
    {
        int index = session.GetComponent<AnimatorSelection>().Attachment;
        int count = session.GetComponent<AnimatorDocument>().Package.Attachments.Count;
        return index >= 0 && index < count ? index : -1;
    }

    private static XafAttachmentState Showing(Entity session, int index)
    {
        XafPose pose = session.GetComponent<AnimatorDocument>().Runtime.Pose;
        return index < pose.AttachmentStates.Length ? pose.AttachmentStates[index] : default;
    }

    /// <summary>An edit to the rig itself, as one undo step, then a rebuild so the art shows it.</summary>
    private static void RigEdit(Entity session, System.Func<XafPackage, int, string> edit, int? index = null) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int target = index ?? Selected(session);
            if (target < 0)
            {
                AnimatorSession.SetStatus(session, "SELECT AN SVG FIRST");
                return;
            }

            string status = edit(session.GetComponent<AnimatorDocument>().Package, target);
            AnimatorSession.RigChanged(session);
            if (status.Length > 0)
                AnimatorSession.SetStatus(session, status);
        });

    /// <summary>A key at the playhead in the open clip, as one undo step, shown at once.</summary>
    private static void Key(Entity session, System.Func<XafPackage, XafClip, int, float, string> key) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int index = Selected(session);
            AnimatorDocument document = session.GetComponent<AnimatorDocument>();
            if (index < 0)
            {
                AnimatorSession.SetStatus(session, "SELECT AN SVG FIRST");
                return;
            }
            if (session.GetComponent<AnimatorModes>().Rest)
            {
                AnimatorSession.SetStatus(session, "LEAVE REST POSE TO KEY");
                return;
            }
            if (AnimatorSession.CurrentClip(document) is not { } clip)
                return;

            document.Runtime.SetPaused(true);
            float time = document.Runtime.CurrentTime;
            string status = key(document.Package, clip, index, time);
            document.Runtime.Seek(time);
            AnimatorSession.SetStatus(session, status);
        });
}
