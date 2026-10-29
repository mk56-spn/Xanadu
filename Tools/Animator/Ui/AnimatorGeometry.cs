using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tools.Animator;

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

    /// <summary>Where a bone's name is drawn beside its joint, which is also its right-click target.</summary>
    public static Rect2 BoneLabel(Vector2 joint) =>
        new(joint + new Vector2(8, -12), new Vector2(100, 20));

    /// <summary>Rough width of one character of a bone label, which is drawn at font size 12.</summary>
    private const float LabelCharWidth = 7f;

    /// <summary>
    /// The plate drawn behind a bone's name so it can be read over light art. The
    /// canvas cannot measure text, so the width comes from the name's length; the
    /// plate starts at the text's edge and never grows past the label's own box.
    /// </summary>
    public static Rect2 BoneLabelBackdrop(Rect2 label, string name) =>
        new(
            label.Position + new Vector2(1, 1),
            new Vector2(
                Mathf.Min(label.Size.X - 1, name.Length * LabelCharWidth + 10),
                label.Size.Y - 2));

    /// <summary>The bone whose joint or label is under the pointer, or -1; joints win.</summary>
    public static int PickBoneOrLabel(
        Transform2D[] world,
        Transform2D placement,
        Vector2 pointer)
    {
        int bone = PickNearest(world, placement, pointer, BoneHitRadius);
        if (bone >= 0)
            return bone;

        // Later bones draw on top, so they take overlapping labels.
        for (int i = world.Length - 1; i >= 0; i--)
        {
            if (BoneLabel(placement * world[i].Origin).HasPoint(pointer))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Id of the bone whose row label in the bone tree is under the pointer,
    /// or null. Collapsed rows and rows scrolled out of the clipping viewport
    /// are skipped.
    /// </summary>
    public static string? TreeLabelAt(Entity tree, Vector2 pointer)
    {
        if (tree.IsNull)
            return null;

        Entity viewport = ClipViewport(tree);
        if (!viewport.IsNull && !PanelArea(viewport).HasPoint(pointer))
            return null;

        foreach (Entity row in tree.ChildEntities)
        {
            if (row.Tags.Has<LayoutIgnore>() || !row.HasComponent<UiTreeNode>())
                continue;

            UiTreeNode node = row.GetComponent<UiTreeNode>();
            if (PanelArea(node.Label).HasPoint(pointer))
                return node.Id;
        }
        return null;
    }

    /// <summary>Screen rectangle of a bone's row label in the bone tree, if it is shown.</summary>
    public static Rect2? TreeLabel(Entity tree, string id)
    {
        Entity row = tree.IsNull ? default : UiTreeControl.FindNode(tree, id);
        if (row.IsNull || row.Tags.Has<LayoutIgnore>())
            return null;
        return PanelArea(row.GetComponent<UiTreeNode>().Label);
    }

    private static Entity ClipViewport(Entity entity)
    {
        for (Entity parent = entity.Parent; !parent.IsNull; parent = parent.Parent)
        {
            if (parent.HasComponent<ScrollView>())
                return parent;
        }
        return default;
    }

    /// <summary>Skeleton space origin with the default view: the middle of the preview panel.</summary>
    public static Vector2 PreviewOrigin(Rect2 area) => area.Position + area.Size / 2;

    /// <summary>Screen point of the skeleton origin under a zoomed and panned view.</summary>
    public static Vector2 PreviewOrigin(Rect2 area, in AnimatorPreviewView view) =>
        view.ToScreen(Vector2.Zero, area);

    /// <summary>Maps skeleton space to screen space for the current preview view.</summary>
    public static Transform2D PreviewPlacement(Rect2 area, in AnimatorPreviewView view) =>
        view.Placement(area);

    /// <summary>Index of the transform whose origin is nearest the pointer within the radius.</summary>
    public static int PickNearest(
        Transform2D[] world,
        Transform2D placement,
        Vector2 pointer,
        float radius)
    {
        int hit = -1;
        float best = radius;
        for (int i = 0; i < world.Length; i++)
        {
            float distance = (placement * world[i].Origin).DistanceTo(pointer);
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
        var pose = new XafPose(package.Skeleton.Bones.Count, package.Attachments.Count, package.Effects.Count);
        for (int i = 0; i < pose.Bones.Length; i++)
            pose.Bones[i] = XafTransform.From(package.Skeleton.Bones[i].Rest);
        for (int i = 0; i < pose.Attachments.Length; i++)
            pose.Attachments[i] = XafTransform.From(package.Attachments[i].Transform);
        for (int i = 0; i < pose.Effects.Length; i++)
            pose.Effects[i] = XafEffectState.Rest(package.Effects[i]);
        return pose;
    }
}
