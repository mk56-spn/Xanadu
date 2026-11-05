using System;

namespace Xanadu.Animation;

/// <summary>
/// Alternate art, keyed art, layers and visibility, and meshes for
/// attachments. Keys are held (step) keys on the attachment's channels, so a
/// swap happens exactly on its frame.
/// </summary>
public static class XafAttachmentAuthoring
{
    public const float BlendStep = 4f;

    /// <summary>Adds art the attachment can switch to and returns its art index, or -1 for a bad attachment.</summary>
    public static int AddAlternate(XafPackage package, int index, string svgPath, string group)
    {
        if (!Valid(package, index) || string.IsNullOrWhiteSpace(svgPath) || string.IsNullOrWhiteSpace(group))
            return -1;

        package.Attachments[index].Alternates.Add(new XafArt { SvgPath = svgPath, Group = group });
        return package.Attachments[index].Alternates.Count;
    }

    /// <summary>
    /// Removes an alternate. Keys that showed it go back to the attachment's own
    /// art, and keys on later alternates move down one, so they show the same art.
    /// </summary>
    public static bool RemoveAlternate(XafPackage package, int index, int art)
    {
        if (!Valid(package, index) || art < 1 || art > package.Attachments[index].Alternates.Count)
            return false;

        XafAttachment attachment = package.Attachments[index];
        attachment.Alternates.RemoveAt(art - 1);
        foreach (XafClip clip in package.Clips)
        {
            foreach (XafChannel channel in clip.Channels)
            {
                if (channel.Target != XafChannelTarget.Attachment || channel.TargetId != attachment.Group ||
                    channel.Property != XafChannelProperty.Art)
                {
                    continue;
                }
                foreach (XafKeyframe key in channel.Keyframes)
                {
                    int shown = (int)MathF.Round(key.Value);
                    key.Value = shown == art ? 0f : shown > art ? shown - 1 : shown;
                }
            }
        }
        return true;
    }

    public static void KeyArt(XafPackage package, XafClip clip, int index, float time, int art)
    {
        if (Valid(package, index))
            Key(package, clip, index, XafChannelProperty.Art, time,
                Math.Clamp(art, 0, package.Attachments[index].ArtCount - 1));
    }

    public static void KeyLayer(XafPackage package, XafClip clip, int index, float time, int layer)
    {
        if (Valid(package, index))
            Key(package, clip, index, XafChannelProperty.Layer, time,
                Math.Clamp(layer, XafAttachment.MinLayer, XafAttachment.MaxLayer));
    }

    public static void KeyVisible(XafPackage package, XafClip clip, int index, float time, bool visible)
    {
        if (Valid(package, index))
            Key(package, clip, index, XafChannelProperty.Visible, time, visible ? 1f : 0f);
    }

    /// <summary>Turns the attachment into a mesh, or back into rigid art; true if it now has one.</summary>
    public static bool ToggleMesh(XafPackage package, int index)
    {
        if (!Valid(package, index))
            return false;

        XafAttachment attachment = package.Attachments[index];
        attachment.Mesh = attachment.Mesh is null ? new XafMesh() : null;
        return attachment.Mesh is not null;
    }

    public static bool SetDetail(XafPackage package, int index, int detail)
    {
        if (!Valid(package, index) || package.Attachments[index].Mesh is not { } mesh)
            return false;
        mesh.Detail = Math.Clamp(detail, XafMesh.MinDetail, XafMesh.MaxDetail);
        return true;
    }

    public static bool SetBlend(XafPackage package, int index, float blend)
    {
        if (!Valid(package, index) || package.Attachments[index].Mesh is not { } mesh || !float.IsFinite(blend))
            return false;
        mesh.Blend = Math.Clamp(blend, 0f, XafMesh.MaxBlend);
        return true;
    }

    /// <summary>
    /// Lets a bone bend the mesh, or stops it. The first pick replaces the
    /// automatic choice (own bone and the bones below it) with an explicit
    /// list that starts from it. The attachment's own bone always bends it.
    /// </summary>
    public static bool ToggleMeshBone(XafPackage package, int index, int bone)
    {
        if (!Valid(package, index) || package.Attachments[index].Mesh is not { } mesh ||
            bone < 0 || bone >= package.Skeleton.Bones.Count)
        {
            return false;
        }

        XafAttachment attachment = package.Attachments[index];
        string id = package.Skeleton.Bones[bone].Id;
        if (id == attachment.BoneId)
            return false;

        // The explicit list keeps the own bone in it, so taking every other
        // bone out leaves the own bone alone rather than the automatic choice.
        if (mesh.Bones.Count == 0)
        {
            foreach (int automatic in XafSkinWeights.Influences(package, attachment))
                mesh.Bones.Add(package.Skeleton.Bones[automatic].Id);
        }

        if (!mesh.Bones.Remove(id))
            mesh.Bones.Add(id);
        return true;
    }

    private static void Key(XafPackage package, XafClip clip, int index, XafChannelProperty property, float time, float value) =>
        XafAuthoring.SetChannel(clip, XafChannelTarget.Attachment, package.Attachments[index].Group, property,
            time, value, XafInterpolation.Step);

    private static bool Valid(XafPackage package, int index) => index >= 0 && index < package.Attachments.Count;
}
