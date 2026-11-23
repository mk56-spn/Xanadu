using System.Collections.Generic;
using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Animation;
using Xanadu.Core.Items;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Draws the skeleton, Verlet chains, onion-skin ghosts, selection markers and parts.</summary>
public sealed class AnimatorPreviewDrawSystem(
    IRectCanvas canvas,
    Handle target,
    AnimatorArt visuals,
    Handle held = default) : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Color BoneColor = Palette.Accent;
    private static readonly Color BoneFill = new(Palette.Info, 0.30f);
    private static readonly Color BoneOutline = Palette.Info;
    private static readonly Color SwungFill = new(Palette.Accent, 0.35f);
    private static readonly Color SwungOutline = new("#fde68a");
    private static readonly Color AttachmentColor = Palette.Lime;
    private static readonly Color ChainColor = Palette.Cyan;
    private static readonly Color ColliderColor = Palette.Lime;
    private static readonly Color Label = Palette.TextMuted;
    private static readonly Color LabelBackdrop = new(Palette.SurfaceDeep, 0.85f);
    private static readonly Color ReparentLabel = Palette.Warning;
    private const float OnionOffset = 0.1f;
    private const int TargetPathPoints = 64;
    private static readonly Color TargetColor = Palette.Warning;
    private static readonly Color TipHandleColor = Palette.Pink;
    private const float TipHandleRadius = 4.5f;
    private static readonly Color FlowGhost = new(Palette.Accent, 0.3f);
    private static readonly Color EffectOutline = Palette.Rose;
    private static readonly Color JointOffsetColor = Palette.Pink;
    private const float JointOffsetRing = 5f;

    /// <summary>How far, in rig units, a joint has to sit off its rest spot before the ring shows.</summary>
    private const float JointOffsetMinimum = 0.5f;
    private static readonly Color LimitColor = new(Palette.Warning, 0.8f);

    /// <summary>Rings bones and art posed but not keyed, the colour the inspector flags them in.</summary>
    private static readonly Color UnkeyedColor = Palette.Warning;
    private const float LimitRadius = 34f;
    private const int LimitSegments = 24;

    private readonly AnimatorFlowOverlay _flow = new(canvas, target);
    private readonly AnimatorMotionPathOverlay _paths = new(canvas, target);
    private readonly AnimatorClothOverlay _cloth = new(canvas, target);
    private readonly AnimatorMeshOverlay _mesh = new(canvas, target);
    private int _version = -1;
    private int _effectsVersion = -1;

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
            _effectsVersion = document.EffectsVersion;
            visuals.Reload(document.Package);
        }
        else if (document.EffectsVersion != _effectsVersion)
        {
            _effectsVersion = document.EffectsVersion;
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
            Palette.Muted,
            1);

        RigPose pose = AnimatorGeometry.PreviewPose(document, modes);
        Transform2D[] world = RigRig.WorldBones(document.Package, pose);

        if (modes.Onion && !modes.Rest)
        {
            RigClip? clip = AnimatorSession.CurrentClip(document);
            if (clip is not null)
            {
                float time = document.Runtime.CurrentTime;
                DrawGhost(document.Package, clip, time - OnionOffset,
                    new Color(Palette.Info, 0.25f), placement, modes.Kites, modes.Handles);
                DrawGhost(document.Package, clip, time + OnionOffset,
                    new Color(Palette.Rose, 0.25f), placement, modes.Kites, modes.Handles);
            }
        }

        DrawFlowGhost(session, document, placement, modes);

        // Under the bones, so the rig stays readable over its own trails.
        _paths.Draw(document, modes, selection, AnimatorPreviewModeCommands.IsOn(session), placement);
        DrawChains(session, document.Package, selection, placement);
        _cloth.Draw(session, document.Package, world, placement);
        DrawChainHandles(document.Package, selection, world, placement);

        // Bone shapes first so joints and labels stay readable on top of them.
        for (int i = 0; modes.Kites && i < world.Length; i++)
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

        if (modes.Handles && !modes.Rest)
        {
            DrawJointOffsets(document.Package, pose, world, placement);
            DrawLimits(document.Package, pose, selection, world, placement);
        }

        (bool[] Bones, bool[] Attachments)? unkeyed = AnimatorUnkeyed.Find(session);
        int unkeyedCount = 0;
        AnimatorHover hover = session.GetComponent<AnimatorHover>();
        int reparenting = session.GetComponent<AnimatorBoneMenu>().Reparenting;
        var labels = new System.Collections.Generic.List<Rect2>();
        for (int i = 0; modes.Handles && i < world.Length; i++)
        {
            RigBone bone = document.Package.Skeleton.Bones[i];
            Vector2 point = placement * world[i].Origin;
            bool primary = i == selection.Bone;
            bool selected = selection.IsBoneSelected(i);
            bool hovered = i == hover.Bone;
            float marker = primary ? 8f : selected ? 7f : hovered ? 6f : 5f;
            canvas.DrawCircle(target, point, marker,
                selected ? Colors.White : hovered ? SwungOutline : BoneColor);
            if (unkeyed is { } bones && i < bones.Bones.Length && bones.Bones[i])
                canvas.DrawCircleOutline(target, point, marker + 4f, UnkeyedColor, 2f, 20);

            // Labels that would overlap are dropped unless they are the bone the
            // pointer or the selection is on; crowded rigs stay readable unzoomed.
            Rect2 label = AnimatorGeometry.BoneLabel(point);
            if (!selected && !hovered && labels.Exists(drawn => drawn.Intersects(label)))
                continue;
            labels.Add(label);
            canvas.DrawRect(target, AnimatorGeometry.BoneLabelBackdrop(label, bone.Id), LabelBackdrop);
            // Left-aligned so the text sits on its right-click target.
            canvas.DrawTextStyledLeft(
                target,
                label,
                bone.Id,
                i == reparenting ? ReparentLabel : Label,
                12);
        }

        DrawTipHandles(document.Package, modes, selection, hover, world, placement);
        DrawPlants(session, placement);
        DrawTarget(session, document, world, placement);
        DrawSnapGuides(session.GetComponent<AnimatorSnapGuides>(), placement, area);

        Transform2D[] attachments = RigRig.WorldAttachments(document.Package, pose, world);
        // A part the XIF editor holds on its bone draws above the editor's dimming, not under it.
        int heldPart = Commands.AnimatorXifHost.HeldOnBone(session);
        visuals.Draw(canvas, document.Package, pose, world, attachments,
            session.GetComponent<AnimatorChains>().Simulation, session.GetComponent<AnimatorCloths>().Simulation, placement, heldPart);
        if (held.IsValid && Commands.AnimatorXifHost.Boxed(session) is { } boxed)
            visuals.DrawBoxed(canvas, held, boxed.Item, boxed.Shown);
        else if (held.IsValid)
            visuals.DrawHeld(canvas, held, document.Package, pose, world, attachments, placement, heldPart);
        _mesh.Draw(session, visuals, document.Package, world, placement);
        DrawSelectedPart(document.Package, pose, selection, attachments, placement);

        // The squares are the handles for placing art, so they only show while placing it.
        for (int i = 0; modes.Parts && i < attachments.Length; i++)
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
            if (unkeyed is { } art && i < art.Attachments.Length && art.Attachments[i])
            {
                float ring = marker + 6f;
                Rect2 outline = new(point - new Vector2(ring / 2, ring / 2), new Vector2(ring, ring));
                canvas.DrawRectOutlines(target, [outline], UnkeyedColor);
            }
        }

        if (unkeyed is { } parts)
        {
            foreach (bool part in parts.Bones)
                unkeyedCount += part ? 1 : 0;
            foreach (bool part in parts.Attachments)
                unkeyedCount += part ? 1 : 0;
        }
        if (unkeyedCount > 0)
        {
            canvas.DrawTextStyledLeft(
                target,
                new Rect2(area.Position + new Vector2(12, 32), new Vector2(300, 20)),
                AnimatorUnkeyed.Note(unkeyedCount) + (modes.AutoKey ? "" : " · AUTO KEY IS OFF"),
                UnkeyedColor,
                14);
        }

        List<string> noStart = AnimatorUnkeyed.MissingStartKey(session);
        if (noStart.Count > 0)
        {
            canvas.DrawTextStyledLeft(
                target,
                new Rect2(area.Position + new Vector2(12, unkeyedCount > 0 ? 52 : 32), new Vector2(600, 20)),
                AnimatorUnkeyed.MissingStartNote(noStart),
                UnkeyedColor,
                14);
        }

        if (modes.Rest)
            DrawRigFrame(area);

        canvas.DrawTextStyledLeft(
            target,
            new Rect2(area.Position + new Vector2(12, 12), new Vector2(300, 20)),
            $"{document.Runtime.CurrentClip}  {document.Runtime.CurrentTime:0.00}s   x{view.Zoom:0.##}",
            Palette.Text,
            14);
        _flow.Draw(session, area);
    }

    /// <summary>
    /// While the rig is being edited the preview gets an orange frame and a
    /// caption along its bottom, so it never passes for posing a clip.
    /// </summary>
    private void DrawRigFrame(Rect2 area)
    {
        const float edge = 4f;
        Vector2 p = area.Position, s = area.Size;
        canvas.DrawRect(target, new Rect2(p, new Vector2(s.X, edge)), UnkeyedColor);
        canvas.DrawRect(target, new Rect2(p + new Vector2(0, s.Y - edge), new Vector2(s.X, edge)), UnkeyedColor);
        canvas.DrawRect(target, new Rect2(p, new Vector2(edge, s.Y)), UnkeyedColor);
        canvas.DrawRect(target, new Rect2(p + new Vector2(s.X - edge, 0), new Vector2(edge, s.Y)), UnkeyedColor);
        canvas.DrawTextStyledLeft(
            target,
            new Rect2(p + new Vector2(12, s.Y - 30), new Vector2(700, 20)),
            "EDITING THE RIG · CHANGES MOVE EVERY CLIP · EDIT RIG AGAIN TO GO BACK TO ANIMATING",
            UnkeyedColor,
            14);
    }

    /// <summary>
    /// A selected bone with an angle limit shows the turn it is allowed: a fan
    /// round its joint from the low end to the high, set against its parent,
    /// so it moves with the pose.
    /// </summary>
    private void DrawLimits(RigPackage package, RigPose pose, AnimatorSelection selection, Transform2D[] world, Transform2D placement)
    {
        for (int i = 0; i < world.Length && i < pose.Bones.Length; i++)
        {
            RigBone bone = package.Skeleton.Bones[i];
            if (!bone.Limited || !selection.IsBoneSelected(i))
                continue;

            Vector2 joint = placement * world[i].Origin;

            // The bone points at its first child: up a spine, down a leg. A bone
            // with no child points where its stem dot does: along its tip (a foot
            // at its toes), else on down the limb.
            int child = package.Skeleton.Bones.FindIndex(candidate => candidate.Parent == i);
            Vector2 local = child >= 0 && package.Skeleton.Bones[child].Rest.Origin.LengthSquared() > 1e-4f
                ? package.Skeleton.Bones[child].Rest.Origin
                : bone.Tip.LengthSquared() > 1e-4f ? bone.Tip
                : bone.Rest.Origin.LengthSquared() > 1e-4f ? bone.Rest.Origin
                : Vector2.Right;

            // Where it points now, on screen, through its own scale and any
            // mirroring; each edge turns that by how far the edge is from the
            // bone's turn now, the other way on screen when the bone is mirrored.
            Vector2 now = placement.BasisXform(world[i].BasisXform(local));
            if (now.LengthSquared() < 1e-6f)
                continue;
            now = now.Normalized();
            float current = RigJointLocks.Turn(package, pose, i);
            float sense = world[i].Determinant() * placement.Determinant() < 0f ? -1f : 1f;
            Vector2 Edge(float turn) => joint + now.Rotated((turn - current) * sense) * LimitRadius;

            Vector2 previous = Edge(bone.LimitMin);
            canvas.DrawLine(target, joint, previous, LimitColor, 2f);
            for (int s = 1; s <= LimitSegments; s++)
            {
                Vector2 next = Edge(float.Lerp(bone.LimitMin, bone.LimitMax, s / (float)LimitSegments));
                canvas.DrawLine(target, previous, next, LimitColor, 2f);
                previous = next;
            }
            canvas.DrawLine(target, joint, previous, LimitColor, 2f);
        }
    }

    /// <summary>
    /// A joint slid off its rest spot shows where it belongs: a hollow ring
    /// at the rest spot on its parent, tethered to where the joint is now.
    /// </summary>
    private void DrawJointOffsets(RigPackage package, RigPose pose, Transform2D[] world, Transform2D placement)
    {
        for (int i = 0; i < world.Length; i++)
        {
            RigBone bone = package.Skeleton.Bones[i];
            if (bone.Parent < 0 || !bone.AllowTranslation ||
                pose.Bones[i].Translation.DistanceTo(bone.Rest.Origin) < JointOffsetMinimum)
            {
                continue;
            }

            Vector2 rest = placement * (world[bone.Parent] * bone.Rest.Origin);
            Vector2 now = placement * world[i].Origin;
            canvas.DrawLine(target, rest, now, JointOffsetColor, 1.5f);
            canvas.DrawCircleOutline(target, rest, JointOffsetRing, JointOffsetColor, 1.5f, 16);
        }
    }

    /// <summary>
    /// While a transition preview leads in on the old clip, the new clip's
    /// first frame shows as a ghost, so the distance the fade has to cover is
    /// visible before it starts.
    /// </summary>
    private void DrawFlowGhost(Entity session, in AnimatorDocument document, Transform2D placement, AnimatorModes modes)
    {
        AnimatorSequence sequence = session.GetComponent<AnimatorSequence>();
        if (modes.Rest || !sequence.Active || sequence.Name != "FLOW" || sequence.Steps is not { Count: > 1 } steps)
            return;

        string next = steps[Math.Min(sequence.Index + 1, steps.Count - 1)].Clip;
        RigClip? clip = document.Package.Clips.Find(candidate => candidate.Id == next);
        if (sequence.Index == 0 && clip is not null)
            DrawGhost(document.Package, clip, 0f, FlowGhost, placement, modes.Kites, modes.Handles);
    }

    /// <summary>
    /// Draws a line through the simulated points of each chain without art, and of
    /// the selected chain, which is brighter.
    /// </summary>
    private void DrawChains(
        Entity session,
        RigPackage package,
        AnimatorSelection selection,
        Transform2D placement)
    {
        RigChainSimulation simulation = session.GetComponent<AnimatorChains>().Simulation;
        Span<Vector2> line = stackalloc Vector2[RigChain.MaxSegments + 1];
        for (int i = 0; i < package.Chains.Count; i++)
        {
            System.Collections.Generic.IReadOnlyList<Vector2> points = simulation.Points(i);
            if (points.Count < 2 || points.Count > line.Length)
                continue;

            // Chains with art are the art; only the selected one also shows its line.
            bool selected = i == selection.Chain;
            if (package.Chains[i].HasArt && !selected)
                continue;

            Color color = selected ? Colors.White : ChainColor;
            for (int p = 0; p < points.Count; p++)
                line[p] = placement * points[p];

            canvas.DrawPolyline(target, line[..points.Count], color, selected ? 3f : 2f);
            for (int p = 1; p < points.Count; p++)
                canvas.DrawCircle(target, line[p], selected ? 4f : 3f, color);
        }
    }

    /// <summary>
    /// The selected chain's two handles: a dot on its first point and a ring on its
    /// collider, which the pointer can drag along their bones.
    /// </summary>
    private void DrawChainHandles(
        RigPackage package,
        AnimatorSelection selection,
        Transform2D[] world,
        Transform2D placement)
    {
        if (!AnimatorChainHandles.TryGet(package, selection.Chain, world, out Vector2 root, out Vector2? collider))
            return;

        RigChain chain = package.Chains[selection.Chain];
        if (collider is { } center)
        {
            Vector2 screen = placement * center;
            canvas.DrawCircleOutline(target, screen, chain.CollideRadius * Math.Abs(placement.Scale.X), ColliderColor, 1.5f);
            canvas.DrawCircle(target, screen, 4f, ColliderColor);
        }

        Vector2 rootScreen = placement * root;
        canvas.DrawCircle(target, rootScreen, 7f, Colors.White);
        canvas.DrawCircle(target, rootScreen, 5f, ChainColor);
    }

    /// <summary>
    /// The selected effect's outline, solid while it shows and faint
    /// while hidden, so it can be placed at a time it is not on screen.
    /// </summary>
    /// <summary>The selected part's outline, dim while the pose hides it, so a hidden streak can still be found.</summary>
    private void DrawSelectedPart(
        RigPackage package,
        RigPose pose,
        AnimatorSelection selection,
        Transform2D[] attachments,
        Transform2D placement)
    {
        int index = selection.Attachment;
        if (index < 0 || index >= attachments.Length || index >= package.Attachments.Count)
            return;

        bool visible = index < pose.AttachmentStates.Length && pose.AttachmentStates[index].Alpha > 0f;
        Color color = visible ? EffectOutline : new Color(EffectOutline, 0.35f);
        Transform2D toScreen = placement * attachments[index];
        foreach (XifPolygon primitive in package.Attachments[index].Node.Polygons)
        {
            int count = primitive.Points.Count;
            if (count < 2)
                continue;

            var outline = new Vector2[count + 1];
            for (int i = 0; i <= count; i++)
                outline[i] = toScreen * primitive.Points[i % count];
            canvas.DrawPolyline(target, outline, color, 1.5f);
        }
        canvas.DrawCircle(target, toScreen.Origin, 4f, color);
    }

    private void DrawGhost(
        RigPackage package,
        RigClip clip,
        float time,
        Color color,
        Transform2D placement,
        bool kites,
        bool handles)
    {
        time = Math.Clamp(time, 0f, Math.Max(0.001f, clip.Duration));
        Transform2D[] world = RigRig.WorldBones(package, RigSampler.Sample(package, clip, time));
        for (int i = 0; i < world.Length; i++)
        {
            Vector2 point = placement * world[i].Origin;
            if (handles)
                canvas.DrawCircle(target, point, 3, color);
            int parent = package.Skeleton.Bones[i].Parent;
            if (!kites || parent < 0 || parent >= world.Length)
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

    /// <summary>The stem and dot that turn each childless bone; over the labels, so a name never hides one.</summary>
    private void DrawTipHandles(
        RigPackage package,
        AnimatorModes modes,
        AnimatorSelection selection,
        AnimatorHover hover,
        Transform2D[] world,
        Transform2D placement)
    {
        if (!AnimatorTipHandles.Shown(modes))
            return;

        bool[] childless = AnimatorTipHandles.Childless(package);
        for (int i = 0; i < childless.Length && i < world.Length; i++)
        {
            if (!childless[i])
                continue;

            Color color = selection.IsBoneSelected(i) ? Colors.White : i == hover.Bone ? SwungOutline : TipHandleColor;
            Vector2 tip = AnimatorTipHandles.Tip(package, i, world, placement);
            canvas.DrawLine(target, placement * world[i].Origin, tip, color, 1);
            canvas.DrawCircle(target, tip, TipHandleRadius, color);
        }
    }

    /// <summary>The selected bone's target: its path round the anchor as it is now, where it is at this moment, and its handles.</summary>
    private void DrawTarget(Entity session, in AnimatorDocument document, Transform2D[] world, Transform2D placement)
    {
        if (AnimatorPreviewModeCommands.IsOn(session) || session.GetComponent<AnimatorModes>().Rest ||
            AnimatorTargetCommands.Selected(session) is not { } limb || !RigTargets.IsValid(document.Package, limb))
        {
            return;
        }

        Transform2D toScreen = placement * AnimatorTargetCommands.AnchorWorld(document.Package, limb, world);
        Vector2 center = toScreen * limb.Center;
        canvas.DrawRect(target, new Rect2(center - new Vector2(5f, 5f), new Vector2(10f, 10f)), TargetColor);
        if (limb.Shape != RigTargetShape.Ellipse)
            return;

        // Red where the limb cannot reach: the baked motion goes flat there.
        Transform2D anchor = AnimatorTargetCommands.AnchorWorld(document.Package, limb, world);
        int bone = RigRig.FindBone(document.Package, limb.BoneId);
        Vector2 previous = limb.OnEllipse(limb.StartAngle);
        for (int i = 1; i <= TargetPathPoints; i++)
        {
            Vector2 next = limb.OnEllipse(limb.StartAngle + limb.Sweep * i / TargetPathPoints);
            bool reached = RigTargets.CanReach(document.Package, world, bone, anchor * next);
            canvas.DrawLine(target, toScreen * previous, toScreen * next, reached ? TargetColor : Palette.Rose, 2);
            previous = next;
        }
        canvas.DrawCircle(target, toScreen * AnimatorTargetCommands.RadiusXHandle(limb), 6f, TargetColor);
        canvas.DrawCircle(target, toScreen * AnimatorTargetCommands.RadiusYHandle(limb), 6f, TargetColor);
        canvas.DrawCircle(target, toScreen * limb.LocalAt(document.Runtime.CurrentTime), 4f, Colors.White);
    }

    /// <summary>A marker on each planted spot: green while held, red when the limb cannot reach it.</summary>
    private void DrawPlants(Entity session, Transform2D placement)
    {
        foreach (AnimatorPlant plant in session.GetComponent<AnimatorPlants>().Held)
        {
            Vector2 at = placement * plant.At;
            Color color = plant.OutOfReach ? Palette.Rose : Palette.Lime;
            canvas.DrawCircleOutline(target, at, 11f, color, 2f);
            canvas.DrawLine(target, at + new Vector2(-15f, 0f), at + new Vector2(15f, 0f), color, 2);
        }
    }

    /// <summary>The lines a dragged joint is snapped to, across the whole preview.</summary>
    private void DrawSnapGuides(AnimatorSnapGuides guides, Transform2D placement, Rect2 area)
    {
        Color color = new(Palette.Cyan, 0.8f);
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
