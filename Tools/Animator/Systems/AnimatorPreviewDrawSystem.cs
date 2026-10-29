using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Systems.Canvas;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Theme;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Draws the skeleton, Verlet chains, onion-skin ghosts, selection markers and SVG attachments.</summary>
public sealed class AnimatorPreviewDrawSystem(
    IRectCanvas canvas,
    Rid target,
    IAttachmentVisuals visuals) : QuerySystem<AnimatorDocument, AnimatorUi>
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
    private static readonly Color FlowGhost = new(Palette.Accent, 0.3f);
    private static readonly Color EffectOutline = Palette.Rose;

    private readonly AnimatorFlowOverlay _flow = new(canvas, target);
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
            Palette.Muted,
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
                    new Color(Palette.Info, 0.25f), placement, modes.Kites, modes.Handles);
                DrawGhost(document.Package, clip, time + OnionOffset,
                    new Color(Palette.Rose, 0.25f), placement, modes.Kites, modes.Handles);
            }
        }

        DrawFlowGhost(session, document, placement, modes);
        DrawChains(session, document.Package, selection, placement);
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

        AnimatorHover hover = session.GetComponent<AnimatorHover>();
        int reparenting = session.GetComponent<AnimatorBoneMenu>().Reparenting;
        var labels = new System.Collections.Generic.List<Rect2>();
        for (int i = 0; modes.Handles && i < world.Length; i++)
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
            canvas.DrawRect(target, AnimatorGeometry.BoneLabelBackdrop(label, bone.Id), LabelBackdrop);
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
        visuals.ApplyChains(document.Package, session.GetComponent<AnimatorChains>().Simulation, placement);
        Transform2D[] effects = XafRig.WorldEffects(document.Package, pose, world);
        visuals.ApplyEffects(pose, effects, placement);
        DrawSelectedEffect(document.Package, pose, selection, effects, placement);

        // The squares are the handles for placing art, so they only show while placing it.
        for (int i = 0; modes.Svg && i < attachments.Length; i++)
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
            Palette.Text,
            14);
        _flow.Draw(session, area);
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
        XafClip? clip = document.Package.Clips.Find(candidate => candidate.Id == next);
        if (sequence.Index == 0 && clip is not null)
            DrawGhost(document.Package, clip, 0f, FlowGhost, placement, modes.Kites, modes.Handles);
    }

    /// <summary>
    /// Draws a line through the simulated points of each chain without art, and of
    /// the selected chain, which is brighter.
    /// </summary>
    private void DrawChains(
        Entity session,
        XafPackage package,
        AnimatorSelection selection,
        Transform2D placement)
    {
        XafChainSimulation simulation = session.GetComponent<AnimatorChains>().Simulation;
        Span<Vector2> line = stackalloc Vector2[XafChain.MaxSegments + 1];
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
        XafPackage package,
        AnimatorSelection selection,
        Transform2D[] world,
        Transform2D placement)
    {
        if (!AnimatorChainHandles.TryGet(package, selection.Chain, world, out Vector2 root, out Vector2? collider))
            return;

        XafChain chain = package.Chains[selection.Chain];
        if (collider is { } center)
        {
            Vector2 screen = placement * center;
            canvas.DrawCircleOutline(target, screen, chain.CollideRadius * Mathf.Abs(placement.Scale.X), ColliderColor, 1.5f);
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
    private void DrawSelectedEffect(
        XafPackage package,
        XafPose pose,
        AnimatorSelection selection,
        Transform2D[] effects,
        Transform2D placement)
    {
        int index = selection.Effect;
        if (index < 0 || index >= effects.Length || index >= package.Effects.Count)
            return;

        bool visible = index < pose.Effects.Length && pose.Effects[index].Visible;
        Color color = visible ? EffectOutline : new Color(EffectOutline, 0.35f);
        Transform2D toScreen = placement * effects[index];
        foreach (Xanadu.Items.XifPrimitive primitive in package.Effects[index].Item.Primitives)
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
        XafPackage package,
        XafClip clip,
        float time,
        Color color,
        Transform2D placement,
        bool kites,
        bool handles)
    {
        time = Mathf.Clamp(time, 0f, Mathf.Max(0.001f, clip.Duration));
        Transform2D[] world = XafRig.WorldBones(package, XafSampler.Sample(package, clip, time));
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
