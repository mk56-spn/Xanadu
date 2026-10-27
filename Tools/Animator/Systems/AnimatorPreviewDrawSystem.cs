using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Animation;
using Writer.Systems.Canvas;

namespace Writer.Tools.Animator.Systems;

/// <summary>Draws the skeleton, onion-skin ghosts, selection markers and SVG attachments.</summary>
public sealed class AnimatorPreviewDrawSystem(
    IRectCanvas canvas,
    Rid target,
    IAttachmentVisuals visuals) : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Color BoneColor = new("#facc15");
    private static readonly Color BoneFill = new("#38bdf8", 0.30f);
    private static readonly Color BoneOutline = new("#38bdf8");
    private static readonly Color SwungFill = new("#facc15", 0.35f);
    private static readonly Color SwungOutline = new("#fde68a");
    private static readonly Color AttachmentColor = new("#a3e635");
    private static readonly Color Label = new("#cbd5e1");
    private static readonly Color ReparentLabel = new("#fb923c");
    private const float OnionOffset = 0.1f;

    private int _version = -1;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Draw(session);
    }

    private void Draw(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorModes modes = session.GetComponent<AnimatorModes>();
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        if (document.Version != _version)
        {
            _version = document.Version;
            visuals.Reload(document.Package);
        }

        canvas.Clear(target);
        Rect2 area = AnimatorGeometry.PanelArea(session.GetComponent<AnimatorUi>().Preview);
        AnimatorPreviewView view = session.GetComponent<AnimatorPreviewView>();
        Transform2D placement = AnimatorGeometry.PreviewPlacement(area, view);
        canvas.DrawLine(
            target,
            new Vector2(area.Position.X, area.End.Y),
            new Vector2(area.End.X, area.End.Y),
            new Color("#334155"),
            1);

        XafPose pose = AnimatorGeometry.PreviewPose(document, modes);
        Transform2D[] world = XafRig.WorldBones(document.Package, pose);

        if (modes.Onion && !modes.Rest)
        {
            XafClip? clip = AnimatorSession.CurrentClip(document);
            if (clip is not null)
            {
                float time = document.Runtime.CurrentTime;
                DrawGhost(document.Package, clip, time - OnionOffset,
                    new Color("#38bdf8", 0.25f), placement);
                DrawGhost(document.Package, clip, time + OnionOffset,
                    new Color("#fb7185", 0.25f), placement);
            }
        }

        // Bone shapes first so joints and labels stay readable on top of them.
        for (int i = 0; i < world.Length; i++)
        {
            int parent = document.Package.Skeleton.Bones[i].Parent;
            if (parent < 0 || parent >= world.Length)
                continue;

            // Links that a selected bone swings are highlighted.
            bool swung = selection.IsBoneSelected(parent);
            DrawBone(
                placement * world[parent].Origin,
                placement * world[i].Origin,
                swung ? SwungFill : BoneFill,
                swung ? SwungOutline : BoneOutline,
                2f);
        }

        AnimatorHover hover = session.GetComponent<AnimatorHover>();
        int reparenting = session.GetComponent<AnimatorBoneMenu>().Reparenting;
        var labels = new System.Collections.Generic.List<Rect2>();
        for (int i = 0; i < world.Length; i++)
        {
            XafBone bone = document.Package.Skeleton.Bones[i];
            Vector2 point = placement * world[i].Origin;
            bool primary = i == selection.Bone;
            bool selected = selection.IsBoneSelected(i);
            bool hovered = i == hover.Bone;
            float marker = primary ? 8f : selected ? 7f : hovered ? 6f : 5f;
            canvas.DrawCircle(target, point, marker,
                selected ? Colors.White : hovered ? SwungOutline : BoneColor);

            // Labels that would overlap are dropped unless they are the bone the
            // pointer or the selection is on; crowded rigs stay readable unzoomed.
            Rect2 label = AnimatorGeometry.BoneLabel(point);
            if (!selected && !hovered && labels.Exists(drawn => drawn.Intersects(label)))
                continue;
            labels.Add(label);
            // Left-aligned so the text sits on its right-click target.
            canvas.DrawTextStyledLeft(
                target,
                label,
                bone.Id,
                i == reparenting ? ReparentLabel : Label,
                12);
        }

        DrawSnapGuides(session.GetComponent<AnimatorSnapGuides>(), placement, area);

        Transform2D[] attachments = XafRig.WorldAttachments(document.Package, pose, world);
        visuals.Apply(attachments, placement);
        for (int i = 0; i < attachments.Length; i++)
        {
            Vector2 point = placement * attachments[i].Origin;
            bool selected = i == selection.Attachment;
            bool hovered = i == hover.Attachment;
            float marker = selected ? 14f : hovered ? 12f : 10f;
            canvas.DrawRect(
                target,
                new Rect2(point - new Vector2(marker / 2, marker / 2),
                    new Vector2(marker, marker)),
                selected ? Colors.White : hovered ? SwungOutline : AttachmentColor);
        }

        canvas.DrawTextStyledLeft(
            target,
            new Rect2(area.Position + new Vector2(12, 12), new Vector2(300, 20)),
            $"{document.Runtime.CurrentClip}  {document.Runtime.CurrentTime:0.00}s   x{view.Zoom:0.##}",
            new Color("#e2e8f0"),
            14);
    }

    private void DrawGhost(
        XafPackage package,
        XafClip clip,
        float time,
        Color color,
        Transform2D placement)
    {
        time = Mathf.Clamp(time, 0f, Mathf.Max(0.001f, clip.Duration));
        Transform2D[] world = XafRig.WorldBones(package, XafSampler.Sample(package, clip, time));
        for (int i = 0; i < world.Length; i++)
        {
            Vector2 point = placement * world[i].Origin;
            canvas.DrawCircle(target, point, 3, color);
            int parent = package.Skeleton.Bones[i].Parent;
            if (parent < 0 || parent >= world.Length)
                continue;

            Vector2 from = placement * world[parent].Origin;
            DrawBone(from, point, new Color(color, color.A * 0.5f), color, 1f);
        }
    }

    /// <summary>Draws one bone as a filled kite with an outline.</summary>
    private void DrawBone(Vector2 from, Vector2 to, Color fill, Color outline, float width)
    {
        Span<Vector2> kite = stackalloc Vector2[4];
        if (!AnimatorBoneShape.Kite(from, to, kite))
            return;

        canvas.DrawPolygon(target, kite, fill);
        Span<Vector2> closed = stackalloc Vector2[5];
        kite.CopyTo(closed);
        closed[4] = kite[0];
        canvas.DrawPolyline(target, closed, outline, width);
    }

    /// <summary>The lines a dragged joint is snapped to, across the whole preview.</summary>
    private void DrawSnapGuides(AnimatorSnapGuides guides, Transform2D placement, Rect2 area)
    {
        Color color = new("#22d3ee", 0.8f);
        if (guides.HasX)
        {
            float x = (placement * new Vector2(guides.X, 0f)).X;
            canvas.DrawLine(target, new Vector2(x, area.Position.Y), new Vector2(x, area.End.Y), color, 1);
        }

        if (guides.HasY)
        {
            float y = (placement * new Vector2(0f, guides.Y)).Y;
            canvas.DrawLine(target, new Vector2(area.Position.X, y), new Vector2(area.End.X, y), color, 1);
        }
    }
}
