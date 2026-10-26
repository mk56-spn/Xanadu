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
        Vector2 origin = AnimatorGeometry.PreviewOrigin(area);
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
                    new Color("#38bdf8", 0.25f), origin);
                DrawGhost(document.Package, clip, time + OnionOffset,
                    new Color("#fb7185", 0.25f), origin);
            }
        }

        // Bone shapes first so joints and labels stay readable on top of them.
        for (int i = 0; i < world.Length; i++)
        {
            int parent = document.Package.Skeleton.Bones[i].Parent;
            if (parent < 0 || parent >= world.Length)
                continue;

            // Links that the selected bone swings are highlighted.
            bool swung = parent == selection.Bone;
            DrawBone(
                origin + world[parent].Origin,
                origin + world[i].Origin,
                swung ? SwungFill : BoneFill,
                swung ? SwungOutline : BoneOutline,
                2f);
        }

        for (int i = 0; i < world.Length; i++)
        {
            XafBone bone = document.Package.Skeleton.Bones[i];
            Vector2 point = origin + world[i].Origin;
            bool selected = i == selection.Bone;
            canvas.DrawCircle(target, point, selected ? 8 : 5,
                selected ? Colors.White : BoneColor);
            canvas.DrawTextStyled(
                target,
                new Rect2(point + new Vector2(8, -12), new Vector2(100, 20)),
                bone.Id,
                Label,
                12);
        }

        Transform2D[] attachments = XafRig.WorldAttachments(document.Package, pose, world);
        visuals.Apply(attachments, new Transform2D(0f, origin));
        for (int i = 0; i < attachments.Length; i++)
        {
            Vector2 point = origin + attachments[i].Origin;
            float marker = i == selection.Attachment ? 14f : 10f;
            canvas.DrawRect(
                target,
                new Rect2(point - new Vector2(marker / 2, marker / 2),
                    new Vector2(marker, marker)),
                i == selection.Attachment ? Colors.White : AttachmentColor);
        }

        canvas.DrawTextStyledLeft(
            target,
            new Rect2(area.Position + new Vector2(12, 12), new Vector2(300, 20)),
            $"{document.Runtime.CurrentClip}  {document.Runtime.CurrentTime:0.00}s",
            new Color("#e2e8f0"),
            14);
    }

    private void DrawGhost(
        XafPackage package,
        XafClip clip,
        float time,
        Color color,
        Vector2 origin)
    {
        time = Mathf.Clamp(time, 0f, Mathf.Max(0.001f, clip.Duration));
        Transform2D[] world = XafRig.WorldBones(package, XafSampler.Sample(package, clip, time));
        for (int i = 0; i < world.Length; i++)
        {
            Vector2 point = origin + world[i].Origin;
            canvas.DrawCircle(target, point, 3, color);
            int parent = package.Skeleton.Bones[i].Parent;
            if (parent < 0 || parent >= world.Length)
                continue;

            Vector2 from = origin + world[parent].Origin;
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
}
