using System;
using Xanadu.Core.Keys;
using Xanadu.Core.Layering;

namespace Xanadu.Model.Animation;

/// <summary>
/// Alternate art, keyed art, layers and visibility, and meshes for
/// attachments. Keys are held (step) keys on the attachment's channels, so a
/// swap happens exactly on its frame.
/// </summary>
public static class RigAttachmentAuthoring
{
    public const float BlendStep = 4f;

    /// <summary>
    /// Adds art the part can switch to, a copy of its own geometry to edit
    /// from, and returns its art index, or -1 for a bad part.
    /// </summary>
    public static int AddAlternate(RigPackage package, int index)
    {
        if (!Valid(package, index))
            return -1;

        RigAttachment part = package.Attachments[index];
        var copy = (Core.Items.XifGeometry)part.Art(0).Copy();
        part.NeedAlternates().Add(copy);
        return part.Alternates.Count;
    }

    /// <summary>
    /// Removes an alternate. Keys that showed it go back to the attachment's own
    /// art, and keys on later alternates move down one, so they show the same art.
    /// </summary>
    public static bool RemoveAlternate(RigPackage package, int index, int art)
    {
        if (!Valid(package, index) || art < 1 || art > package.Attachments[index].Alternates.Count)
            return false;

        RigAttachment attachment = package.Attachments[index];
        attachment.NeedAlternates().RemoveAt(art - 1);
        if (attachment.Alternates.Count == 0)
            attachment.Node.Modules.RemoveAll(module => module is XifVariants);
        foreach (RigClip clip in package.Clips)
        {
            foreach (KeyChannel channel in clip.Channels)
            {
                if (channel.Target != RigChannelTarget.Attachment || channel.TargetId != attachment.Id ||
                    channel.Property != RigChannelProperty.Art)
                {
                    continue;
                }
                foreach (Keyframe key in channel.Keys)
                {
                    int shown = (int)MathF.Round(key.Value);
                    key.Value = shown == art ? 0f : shown > art ? shown - 1 : shown;
                }
            }
        }
        return true;
    }

    public static void KeyArt(RigPackage package, RigClip clip, int index, float time, int art)
    {
        if (Valid(package, index))
            Key(package, clip, index, RigChannelProperty.Art, time,
                Math.Clamp(art, 0, package.Attachments[index].ArtCount - 1));
    }

    public static void KeyLayer(RigPackage package, RigClip clip, int index, float time, int layer)
    {
        if (Valid(package, index))
            Key(package, clip, index, RigChannelProperty.Layer, time,
                DrawOrder.ClampLayer(layer));
    }

    public static void KeyVisible(RigPackage package, RigClip clip, int index, float time, bool visible)
    {
        if (Valid(package, index))
            Key(package, clip, index, RigChannelProperty.Visible, time, visible ? 1f : 0f);
    }

    /// <summary>Turns the attachment into a mesh, or back into rigid art; true if it now has one.</summary>
    public static bool ToggleMesh(RigPackage package, int index)
    {
        if (!Valid(package, index))
            return false;

        RigAttachment attachment = package.Attachments[index];
        attachment.Mesh = attachment.Mesh is null ? new RigMesh() : null;
        return attachment.Mesh is not null;
    }

    public static bool SetDetail(RigPackage package, int index, int detail)
    {
        if (!Valid(package, index) || package.Attachments[index].Mesh is not { } mesh)
            return false;
        mesh.Detail = Math.Clamp(detail, RigMesh.MinDetail, RigMesh.MaxDetail);
        return true;
    }

    public static bool SetBlend(RigPackage package, int index, float blend)
    {
        if (!Valid(package, index) || package.Attachments[index].Mesh is not { } mesh || !float.IsFinite(blend))
            return false;
        mesh.Blend = Math.Clamp(blend, 0f, RigMesh.MaxBlend);
        return true;
    }

    /// <summary>
    /// Lets a bone bend the mesh, or stops it. The first pick replaces the
    /// automatic choice (own bone and the bones below it) with an explicit
    /// list that starts from it. The attachment's own bone always bends it.
    /// </summary>
    public static bool ToggleMeshBone(RigPackage package, int index, int bone)
    {
        if (!Valid(package, index) || package.Attachments[index].Mesh is not { } mesh ||
            bone < 0 || bone >= package.Skeleton.Bones.Count)
        {
            return false;
        }

        RigAttachment attachment = package.Attachments[index];
        string id = package.Skeleton.Bones[bone].Id;
        if (id == attachment.BoneId)
            return false;

        // The explicit list keeps the own bone in it, so taking every other
        // bone out leaves the own bone alone rather than the automatic choice.
        if (mesh.Bones.Count == 0)
        {
            foreach (int automatic in RigSkinWeights.Influences(package, attachment))
                mesh.Bones.Add(package.Skeleton.Bones[automatic].Id);
        }

        if (!mesh.Bones.Remove(id))
            mesh.Bones.Add(id);
        return true;
    }

    private static void Key(RigPackage package, RigClip clip, int index, RigChannelProperty property, float time, float value) =>
        RigAuthoring.SetChannel(clip, RigChannelTarget.Attachment, package.Attachments[index].Id, property,
            time, value, KeyEase.Step);

    private static bool Valid(RigPackage package, int index) => index >= 0 && index < package.Attachments.Count;
}
