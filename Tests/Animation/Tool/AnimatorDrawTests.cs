using System;
using System.Collections.Generic;
using Godot;
using NUnit.Framework;
using Writer.Animation;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Tools.Animator;
using Writer.Tools.Animator.Commands;
using Writer.Tools.Animator.Systems;

namespace Writer.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorDrawTests : AnimatorToolTest
{
    // NUnit reuses one fixture instance, so the fakes are rebuilt per test.
    private RecordingCanvas _canvas = null!;
    private RecordingVisuals _visuals = null!;

    protected override void AddSystems()
    {
        _canvas = new RecordingCanvas();
        _visuals = new RecordingVisuals();
        Root.Add(new AnimatorPreviewDrawSystem(_canvas, default, _visuals));
        Root.Add(new AnimatorTimelineDrawSystem(_canvas, default));
    }

    private int Circles(float radius) =>
        _canvas.LastFrameCircles.FindAll(circle => circle == radius).Count;

    [Test]
    public void LoadsAttachmentVisualsOnceAndOnEachNewPackage()
    {
        Frame(3);
        Assert.That(_visuals.Reloads, Is.EqualTo(1));

        Session.GetComponent<AnimatorDocument>().Version++;
        Frame();
        Assert.That(_visuals.Reloads, Is.EqualTo(2));
    }

    [Test]
    public void DrawsABoneMarkerPerBoneAndEnlargesTheSelection()
    {
        Session.GetComponent<AnimatorSelection>().Bone = 1;
        Frame();

        Assert.That(Circles(5), Is.EqualTo(2));
        Assert.That(Circles(8), Is.EqualTo(1));
    }

    [Test]
    public void OnionSkinAddsTwoGhostsOfTheSkeleton()
    {
        Frame();
        int before = Circles(3);
        Session.GetComponent<AnimatorModes>().Onion = true;
        Frame();

        Assert.That(Circles(3) - before, Is.EqualTo(2 * Package.Skeleton.Bones.Count));
    }

    [Test]
    public void OnionSkinIsHiddenInRestPose()
    {
        Session.GetComponent<AnimatorModes>().Onion = true;
        Session.GetComponent<AnimatorModes>().Rest = true;
        Frame();

        Assert.That(Circles(3), Is.EqualTo(0));
    }

    [Test]
    public void TimelineDrawsOneDiamondPerKeyTimeAndEnlargesTheSelected()
    {
        Frame();
        int plain = _canvas.StripCount;

        ref AnimatorSelection selection = ref Session.GetComponent<AnimatorSelection>();
        selection.HasKey = true;
        selection.KeyTarget = XafChannelTarget.Bone;
        selection.KeyId = "head";
        selection.KeyTime = 1f;
        Frame();

        // Three keys of radius 5 (11 strips each); one grows to radius 7 (15 strips).
        Assert.That(plain, Is.EqualTo(3 * 11));
        Assert.That(_canvas.StripCount, Is.EqualTo(2 * 11 + 15));
    }

    [Test]
    public void KeyMarkersShowHowEachKeyEases()
    {
        Frame();
        int diamonds = _canvas.StripCount;
        int circles = Circles(5);

        XafEasing.Apply(Idle, XafChannelTarget.Bone, "head", 1f, XafEasePreset.Ease);
        XafEasing.Apply(Idle, XafChannelTarget.Bone, "head", 0f, XafEasePreset.Step);
        Frame();

        // One diamond became a circle and another a square.
        Assert.That(_canvas.StripCount, Is.EqualTo(diamonds - 2 * 11));
        Assert.That(Circles(5), Is.EqualTo(circles + 1));
    }

    [Test]
    public void TimelineLabelsRowsAndRulerTicks()
    {
        Frame();

        Assert.That(_canvas.Texts, Does.Contain("head"));
        Assert.That(_canvas.Texts, Has.Some.StartsWith("idle"));
        Assert.That(_canvas.Texts, Does.Contain("0.2"));
    }

    [Test]
    public void ZoomingInShowsOnlyKeysInsideTheWindow()
    {
        Runtime.Seek(0f);
        Session.GetComponent<TimelineView>().Zoom = 8f;
        Frame();

        // At zoom 8 the window is 0.25 s wide: only the key at 0 is inside it.
        Assert.That(_canvas.StripCount, Is.EqualTo(11));
    }

    [Test]
    public void TimelineExplainsHowToStartWhenThereAreNoKeys()
    {
        Idle.Channels.Clear();
        Frame();

        Assert.That(_canvas.Texts, Has.Some.StartsWith("No keys yet"));
    }

    [Test]
    public void EachParentLinkIsDrawnAsOneBoneShape()
    {
        Frame();

        // torso -> root and head -> torso; the root has no parent link.
        Assert.That(_canvas.PolygonColors, Has.Count.EqualTo(2));
        Assert.That(_canvas.PolygonColors.TrueForAll(color => color.A < 1f), Is.True);
    }

    [Test]
    public void LinksTheSelectedBoneSwingsAreHighlighted()
    {
        Frame();
        Color plain = _canvas.PolygonColors[0];

        Session.GetComponent<AnimatorSelection>().Bone = 0;
        Frame();

        Assert.That(_canvas.PolygonColors, Has.Count.EqualTo(2));
        // Root is the parent of the torso link only.
        Assert.That(_canvas.PolygonColors[0], Is.Not.EqualTo(plain));
        Assert.That(_canvas.PolygonColors[1], Is.EqualTo(plain));
    }

    [Test]
    public void OnionSkinGhostsAreDrawnAsFainterBoneShapes()
    {
        Session.GetComponent<AnimatorModes>().Onion = true;
        Runtime.Seek(1f);
        Frame();

        Assert.That(_canvas.PolygonColors, Has.Count.EqualTo(2 + 2 * 2));
        float mainAlpha = _canvas.PolygonColors[^1].A;
        Assert.That(_canvas.PolygonColors.FindAll(color => color.A < mainAlpha), Is.Not.Empty);
    }

    [Test]
    public void PreviewCaptionNamesTheCurrentClip()
    {
        Frame();

        Assert.That(_canvas.Texts, Has.Some.StartsWith("idle"));
    }

    private sealed class RecordingVisuals : IAttachmentVisuals
    {
        public int Reloads;

        public void Reload(XafPackage package) => Reloads++;

        public void Apply(Transform2D[] attachments, Transform2D placement) { }
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        private int _clears;

        public List<float> LastFrameCircles { get; private set; } = new();
        public List<string> Texts { get; } = new();

        /// <summary>One-pixel-high rects drawn this frame: the strips of key diamonds.</summary>
        public int StripCount { get; private set; }

        /// <summary>Fill colours of the filled polygons (bone shapes) drawn this frame.</summary>
        public List<Color> PolygonColors { get; } = new();

        public void Clear(Rid canvas)
        {
            // Two canvases are cleared per frame: preview first, timeline second.
            if (_clears++ % 2 == 0)
            {
                LastFrameCircles = new List<float>();
                Texts.Clear();
                StripCount = 0;
                PolygonColors.Clear();
            }
        }

        public void DrawCircle(Rid canvas, Vector2 center, float radius, Color color) =>
            LastFrameCircles.Add(radius);

        public void DrawTextStyled(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
            Texts.Add(text);

        public void DrawTextStyledLeft(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
            Texts.Add(text);

        public void DrawPolygon(Rid canvas, ReadOnlySpan<Vector2> points, Color color) =>
            PolygonColors.Add(color);

        public void SetVisible(Rid canvas, bool visible) { }
        public void SetTransform(Rid canvas, Transform2D transform) { }
        public void DrawRect(Rid canvas, Rect2 bounds, Color color)
        {
            if (bounds.Size.Y == 1f)
                StripCount++;
        }
        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }
        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }
    }
}
