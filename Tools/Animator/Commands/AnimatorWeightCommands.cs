using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Skin weights in the animator: tracing the selected SVG into a skin and
/// weighting it automatically, and painting its weights with a brush in the
/// preview. Painting is per bone: click a joint to choose it, drag to add its
/// weight, Ctrl-drag to take it away.
/// </summary>
public static class AnimatorWeightCommands
{
    /// <summary>Traces the selected SVG into a skin mesh and weights it by heat diffusion, as one undo step.</summary>
    public static void AutoWeights(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        int index = session.GetComponent<AnimatorSelection>().Attachment;
        if (index < 0 || index >= document.Package.Attachments.Count)
        {
            AnimatorSession.SetStatus(session, "SELECT AN SVG FIRST");
            return;
        }

        XafAttachment attachment = document.Package.Attachments[index];
        string? svg = document.Files.ReadText(attachment.SvgPath);
        if (svg is null)
        {
            AnimatorSession.SetStatus(session, $"CANNOT READ {attachment.SvgPath}");
            return;
        }

        string status = $"{attachment.Group} HAS NO OUTLINE TO TRACE";
        AnimatorHistoryCommands.Edit(session, () =>
        {
            try
            {
                if (XafSkinAuthoring.Build(document.Package, index, svg))
                {
                    XafSkinMesh skin = attachment.Mesh!.Skin!;
                    status = $"{attachment.Group} SKINNED · {skin.Points.Length} POINTS OVER {string.Join(", ", skin.Bones)}";
                }
            }
            catch (XafFormatException exception)
            {
                status = exception.Message.ToUpperInvariant();
            }
        });
        session.GetComponent<AnimatorDocument>().Version++;
        AnimatorSession.SetStatus(session, status);
    }

    /// <summary>Starts or stops painting the selected skin, for the selected bone if it bends it.</summary>
    public static void TogglePaint(Entity session)
    {
        ref AnimatorWeightPaint paint = ref session.GetComponent<AnimatorWeightPaint>();
        if (paint.Active)
        {
            paint.Active = false;
            AnimatorSession.SetStatus(session, "DONE PAINTING WEIGHTS");
            return;
        }

        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        int index = selection.Attachment;
        if (index < 0 || index >= package.Attachments.Count || package.Attachments[index].Mesh?.Skin is null)
        {
            AnimatorSession.SetStatus(session, "SELECT A SKINNED SVG FIRST · AUTO WEIGHTS MAKES ONE");
            return;
        }

        paint.Active = true;
        paint.Attachment = index;
        paint.Bone = XafSkinWeights.Influences(package, package.Attachments[index]) is { Count: > 0 } bones ? bones[0] : -1;
        AnimatorSession.SetStatus(session, $"PAINTING {BoneName(package, paint.Bone)} · CLICK A JOINT FOR ANOTHER BONE · CTRL-DRAG TAKES AWAY");
    }

    /// <summary>A click on a joint while painting: that bone's weight is painted from now on.</summary>
    public static void PickBone(Entity session, int bone)
    {
        ref AnimatorWeightPaint paint = ref session.GetComponent<AnimatorWeightPaint>();
        paint.Bone = bone;
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        AnimatorSession.SetStatus(session, $"PAINTING {BoneName(package, bone)}");
    }

    public static void StepRadius(Entity session, int step)
    {
        ref AnimatorWeightPaint paint = ref session.GetComponent<AnimatorWeightPaint>();
        paint.Radius = Math.Clamp(paint.Radius + step * AnimatorWeightPaint.RadiusStep,
            AnimatorWeightPaint.MinRadius, AnimatorWeightPaint.MaxRadius);
        AnimatorSession.SetStatus(session, $"BRUSH {paint.Radius:0} PX");
    }

    public static void StepStrength(Entity session, int step)
    {
        ref AnimatorWeightPaint paint = ref session.GetComponent<AnimatorWeightPaint>();
        paint.Strength = Math.Clamp(paint.Strength + step * AnimatorWeightPaint.StrengthStep,
            AnimatorWeightPaint.MinStrength, AnimatorWeightPaint.MaxStrength);
        AnimatorSession.SetStatus(session, $"BRUSH STRENGTH {paint.Strength:0.00}");
    }

    /// <summary>
    /// One stroke step at a screen point: every skin point drawn within the
    /// brush gets the painted bone's weight raised (or, with
    /// <paramref name="subtract"/>, lowered), most at the brush's middle.
    /// </summary>
    public static void Stroke(Entity session, Vector2 pointer, Transform2D placement, bool subtract)
    {
        AnimatorWeightPaint paint = session.GetComponent<AnimatorWeightPaint>();
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        XafPackage package = document.Package;
        if (!paint.Active || paint.Attachment < 0 || paint.Attachment >= package.Attachments.Count ||
            paint.Bone < 0 || paint.Bone >= package.Skeleton.Bones.Count ||
            package.Attachments[paint.Attachment].Mesh?.Skin is not { } skin)
        {
            return;
        }

        XafPose pose = AnimatorGeometry.PreviewPose(document, session.GetComponent<AnimatorModes>());
        Vector2[] posed = XafSkinAuthoring.Posed(package, paint.Attachment, pose);
        var under = new List<(int, float)>();
        for (int i = 0; i < posed.Length; i++)
        {
            float distance = (placement * posed[i]).DistanceTo(pointer);
            if (distance >= paint.Radius)
                continue;
            float t = 1f - distance * distance / (paint.Radius * paint.Radius);
            under.Add((i, t * t));
        }
        if (under.Count == 0)
            return;

        XafSkinAuthoring.Paint(skin, package.Skeleton.Bones[paint.Bone].Id, under,
            subtract ? -paint.Strength : paint.Strength);
    }

    private static string BoneName(XafPackage package, int bone) =>
        bone >= 0 && bone < package.Skeleton.Bones.Count ? package.Skeleton.Bones[bone].Id : "NOTHING";
}
