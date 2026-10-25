using Friflo.Engine.ECS;
using Godot;
using Writer.Animation;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Tools.Animator;

/// <summary>Screen-space areas and hit tests for the preview and timeline panels.</summary>
public static class AnimatorGeometry
{
    public const float BoneHitRadius = 16f;
    public const float AttachmentHitRadius = 22f;

    /// <summary>Screen rectangle of a laid-out panel entity.</summary>
    public static Rect2 PanelArea(Entity panel)
    {
        if (panel.IsNull ||
            !panel.HasComponent<Rectangle>() ||
            !panel.HasComponent<Position>())
        {
            return new Rect2();
        }

        Vector2 center = UiTransform.WorldPosition(
            panel,
            panel.GetComponent<Position>().Value);
        Vector2 size = panel.GetComponent<Rectangle>().Bounds.Size;
        return new Rect2(center - size / 2, size);
    }

    /// <summary>Skeleton space origin: the middle of the preview panel.</summary>
    public static Vector2 PreviewOrigin(Rect2 area) => area.Position + area.Size / 2;

    /// <summary>Index of the transform whose origin is nearest the pointer within the radius.</summary>
    public static int PickNearest(
        Transform2D[] world,
        Vector2 origin,
        Vector2 pointer,
        float radius)
    {
        int hit = -1;
        float best = radius;
        for (int i = 0; i < world.Length; i++)
        {
            float distance = (origin + world[i].Origin).DistanceTo(pointer);
            if (distance > best)
                continue;
            best = distance;
            hit = i;
        }
        return hit;
    }

    /// <summary>The pose shown in the preview: the rest pose in rest mode, else the runtime pose.</summary>
    public static XafPose PreviewPose(in AnimatorDocument document, in AnimatorModes modes)
    {
        if (!modes.Rest)
            return document.Runtime.Pose;

        XafPackage package = document.Package;
        var pose = new XafPose(package.Skeleton.Bones.Count, package.Attachments.Count);
        for (int i = 0; i < pose.Bones.Length; i++)
            pose.Bones[i] = XafTransform.From(package.Skeleton.Bones[i].Rest);
        for (int i = 0; i < pose.Attachments.Length; i++)
            pose.Attachments[i] = XafTransform.From(package.Attachments[i].Transform);
        return pose;
    }
}
