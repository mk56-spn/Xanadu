using Friflo.Engine.ECS;
using Xanadu.Core.Keys;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>
/// The ART section's buttons, for the selected part: alternate art and
/// which art shows, its keyed layer and visibility, and its mesh.
/// </summary>
public static class AnimatorArtCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<AddArt>((s, _) => AddArt(s));
        session.On<DropArt>((s, _) => DropArt(s));
        session.On<EditXif>((s, _) => EditXif(s));
        session.On<StepArt>((s, e) => StepArt(s, e.By));
        session.On<StepLayerKey>((s, e) => StepLayer(s, e.By));
        session.On<ToggleArtVisible>((s, _) => ToggleVisible(s));
        session.On<ToggleMesh>((s, _) => ToggleMesh(s));
        session.On<PickMeshBones>((s, _) => TogglePickBones(s));
        session.On<StepMeshDetail>((s, e) => StepDetail(s, e.By));
        session.On<StepMeshBlend>((s, e) => StepBlend(s, e.By));
    }

    /// <summary>Gives the selected part another art it can switch to: a copy of its own, to redraw.</summary>
    public static void AddArt(Entity session) =>
        RigEdit(session, (package, index) =>
        {
            int art = RigAttachmentAuthoring.AddAlternate(package, index);
            return art < 0 ? "" : $"{package.Attachments[index].Id} HAS ART {art} · A COPY OF ITS OWN TO REDRAW";
        });

    /// <summary>
    /// Opens the XIF editor over everything on what is selected: the part's
    /// art (the alternate it shows at the playhead, if any), the chain's or the cloth's.
    /// </summary>
    public static void EditXif(Entity session)
    {
        Entity editor = session.GetComponent<AnimatorUi>().XifEditor;
        if (editor.IsNull || !editor.HasComponent<XifEditor>() || editor.GetComponent<XifEditor>().Host.Item is null)
        {
            AnimatorSession.SetStatus(session, "SELECT A PART, A CHAIN OR A CLOTH FIRST");
            return;
        }
        XifEditorSystem.Open(editor);
    }

    /// <summary>Removes the art showing at the playhead, if it is an alternate.</summary>
    public static void DropArt(Entity session) =>
        RigEdit(session, (package, index) =>
        {
            int art = Showing(session, index).Art;
            return art >= 1 && RigAttachmentAuthoring.RemoveAlternate(package, index, art)
                ? $"DROPPED ART {art} OF {package.Attachments[index].Id}"
                : "SHOW AN ALTERNATE ART FIRST, THEN DROP IT";
        });

    /// <summary>Keys the next or previous art at the playhead, wrapping round.</summary>
    public static void StepArt(Entity session, int step) =>
        Key(session, (package, clip, index, time) =>
        {
            RigAttachment attachment = package.Attachments[index];
            if (attachment.ArtCount < 2)
                return $"{attachment.Id} HAS NO OTHER ART · ADD SOME WITH + ART";

            int art = ((Showing(session, index).Art + step) % attachment.ArtCount + attachment.ArtCount) %
                      attachment.ArtCount;
            RigAttachmentAuthoring.KeyArt(package, clip, index, time, art);
            return $"{attachment.Id} SHOWS ART {art} FROM {time:0.00} S";
        });

    public static void StepLayer(Entity session, int step) =>
        Key(session, (package, clip, index, time) =>
        {
            RigAttachment attachment = package.Attachments[index];
            int layer = Showing(session, index).LayerOf(attachment) + step;
            RigAttachmentAuthoring.KeyLayer(package, clip, index, time, layer);
            return $"{attachment.Id} ON LAYER {layer} FROM {time:0.00} S";
        });

    public static void ToggleVisible(Entity session) =>
        Key(session, (package, clip, index, time) =>
        {
            bool hidden = Showing(session, index).Hidden;
            RigAttachmentAuthoring.KeyVisible(package, clip, index, time, hidden);
            return $"{package.Attachments[index].Id} {(hidden ? "SHOWN" : "HIDDEN")} FROM {time:0.00} S";
        });

    public static void ToggleMesh(Entity session) =>
        RigEdit(session, (package, index) => RigAttachmentAuthoring.ToggleMesh(package, index)
            ? $"{package.Attachments[index].Id} IS A MESH · IT BENDS WITH ITS BONE AND THE ONES BELOW"
            : $"{package.Attachments[index].Id} IS RIGID AGAIN");

    public static void StepDetail(Entity session, int step) =>
        RigEdit(session, (package, index) =>
            package.Attachments[index].Mesh is { } mesh &&
            RigAttachmentAuthoring.SetDetail(package, index, mesh.Detail + step)
                ? $"MESH DETAIL {mesh.Detail}"
                : "TURN MESH ON FIRST");

    public static void StepBlend(Entity session, int step) =>
        RigEdit(session, (package, index) =>
            package.Attachments[index].Mesh is { } mesh &&
            RigAttachmentAuthoring.SetBlend(package, index, mesh.Blend + step * RigAttachmentAuthoring.BlendStep)
                ? $"MESH BLEND {mesh.Blend:0} PX"
                : "TURN MESH ON FIRST");

    /// <summary>Sets how wide a zone two bones share the selected mesh over, in pixels.</summary>
    public static void SetBlend(Entity session, float blend) =>
        RigEdit(session, (package, index) =>
            package.Attachments[index].Mesh is { } mesh &&
            RigAttachmentAuthoring.SetBlend(package, index, blend)
                ? $"MESH BLEND {mesh.Blend:0} PX"
                : "TURN MESH ON FIRST");

    /// <summary>The selected mesh's blend, if a part with a mesh is selected.</summary>
    public static bool TryGetBlend(Entity session, out float blend)
    {
        int index = Selected(session);
        blend = 0f;
        if (index < 0 || session.GetComponent<AnimatorDocument>().Package.Attachments[index].Mesh is not { } mesh)
            return false;
        blend = mesh.Blend;
        return true;
    }

    /// <summary>Starts or stops picking the bones that bend the selected mesh by clicking them.</summary>
    public static void TogglePickBones(Entity session)
    {
        ref AnimatorMeshPick pick = ref session.GetComponent<AnimatorMeshPick>();
        int index = Selected(session);
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (!pick.Active && (index < 0 || package.Attachments[index].Mesh is null))
        {
            AnimatorSession.SetStatus(session, "SELECT A MESHED PART FIRST");
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
            if (!RigAttachmentAuthoring.ToggleMeshBone(package, index, bone))
                return $"{id} IS THE PART'S OWN BONE · IT ALWAYS BENDS IT";
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

    private static RigAttachmentState Showing(Entity session, int index)
    {
        RigPose pose = session.GetComponent<AnimatorDocument>().Runtime.Pose;
        return index < pose.AttachmentStates.Length ? pose.AttachmentStates[index] : default;
    }

    /// <summary>An edit to the rig itself, as one undo step, then a rebuild so the art shows it.</summary>
    private static void RigEdit(Entity session, System.Func<RigPackage, int, string> edit, int? index = null) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int target = index ?? Selected(session);
            if (target < 0)
            {
                AnimatorSession.SetStatus(session, "SELECT A PART FIRST");
                return;
            }

            string status = edit(session.GetComponent<AnimatorDocument>().Package, target);
            AnimatorSession.RigChanged(session);
            if (status.Length > 0)
                AnimatorSession.SetStatus(session, status);
        });

    /// <summary>A key at the playhead in the open clip, as one undo step, shown at once.</summary>
    private static void Key(Entity session, System.Func<RigPackage, RigClip, int, float, string> key) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int index = Selected(session);
            AnimatorDocument document = session.GetComponent<AnimatorDocument>();
            if (index < 0)
            {
                AnimatorSession.SetStatus(session, "SELECT A PART FIRST");
                return;
            }
            if (AnimatorModes.Of(session).Rest)
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
