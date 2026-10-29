using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

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
        Root.Add(new AnimatorChainSimulationSystem());
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

    private void AddChain(string art)
    {
        Package.Chains.Add(new XafChain
        {
            Id = "hair", BoneId = "head", Segments = 6, SvgPath = art, Group = art.Length > 0 ? "hair_segment" : "",
        });
        Frame(2);
    }

    [Test]
    public void ChainArtIsHandedTheSimulatedPointsEveryFrame()
    {
        AddChain(XafChain.DefaultSvgPath);
        int before = _visuals.ChainFrames;

        Frame(3);

        Assert.That(_visuals.ChainFrames, Is.EqualTo(before + 3));
        Assert.That(_visuals.ChainPoints, Is.EqualTo(7));
    }

    [Test]
    public void AChainWithArtDrawsNoLineUntilItIsSelectedWhileOneWithoutDoes()
    {
        AddChain(XafChain.DefaultSvgPath);
        Assert.That(Circles(3), Is.EqualTo(0));
        Assert.That(Circles(4), Is.EqualTo(0));

        Session.GetComponent<AnimatorSelection>().SelectOnlyChain(0);
        Frame();
        Assert.That(Circles(4), Is.EqualTo(6));

        Package.Chains[0].SvgPath = "";
        Package.Chains[0].Group = "";
        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(1);
        Frame();
        Assert.That(Circles(3), Is.EqualTo(6));
    }

    private int AttachmentSquares() =>
        _canvas.RectSizes.FindAll(size => size == new Vector2(10, 10)).Count;

    [Test]
    public void AttachmentSquaresOnlyShowInSvgMode()
    {
        // The timeline draws squares of its own into the same recorder, so compare against it.
        Frame();
        Assert.That(Package.Attachments, Has.Count.EqualTo(1));
        int without = AttachmentSquares();

        Session.GetComponent<AnimatorModes>().Svg = true;
        Frame();
        Assert.That(AttachmentSquares(), Is.EqualTo(without + 1));

        Session.GetComponent<AnimatorModes>().Svg = false;
        Frame();
        Assert.That(AttachmentSquares(), Is.EqualTo(without));
    }

    [Test]
    public void HandlesAreJointDotsAndLabelsAndCanBeTurnedOff()
    {
        // A selected bone also gets a timeline row labelled with its name, so the
        // preview's label is counted by what disappears.
        Session.GetComponent<AnimatorSelection>().Bone = 1;
        Frame();
        Assert.That(Session.GetComponent<AnimatorModes>().Handles, Is.True);
        Assert.That(Circles(5), Is.EqualTo(2));
        Assert.That(Circles(8), Is.EqualTo(1));
        int labels = _canvas.Texts.FindAll(text => text == "torso").Count;

        Session.GetComponent<AnimatorModes>().Handles = false;
        Frame();

        Assert.That(Circles(5) + Circles(7) + Circles(8), Is.EqualTo(0));
        Assert.That(_canvas.Texts.FindAll(text => text == "torso"), Has.Count.EqualTo(labels - 1));
        // The bone shapes are their own toggle.
        Assert.That(_canvas.PolygonColors, Has.Count.EqualTo(Package.Skeleton.Bones.Count - 1));
    }

    [Test]
    public void EachBoneLabelHasAPlateBehindItThatHidesWithTheHandles()
    {
        // Plates are sized by name length: "torso" has its own size, "root" and "head" share one.
        int Plates(string name) => _canvas.RectSizes.FindAll(size =>
            size == AnimatorGeometry.BoneLabelBackdrop(
                AnimatorGeometry.BoneLabel(Vector2.Zero), name).Size).Count;

        Frame();
        Assert.That(Plates("torso"), Is.EqualTo(1));
        Assert.That(Plates("head"), Is.EqualTo(2));

        Session.GetComponent<AnimatorModes>().Handles = false;
        Frame();
        Assert.That(Plates("torso"), Is.EqualTo(0));
        Assert.That(Plates("head"), Is.EqualTo(0));
    }

    [Test]
    public void ALabelPlateStartsAtTheTextAndNeverOutgrowsTheLabelBox()
    {
        Rect2 label = AnimatorGeometry.BoneLabel(new Vector2(200, 100));

        Rect2 plate = AnimatorGeometry.BoneLabelBackdrop(label, "torso");
        Assert.That(plate.Size, Is.EqualTo(new Vector2(45, 18)));
        Assert.That(plate.Position, Is.EqualTo(label.Position + new Vector2(1, 1)));

        Rect2 longPlate = AnimatorGeometry.BoneLabelBackdrop(label, "upper_arm_with_a_very_longPlatename");
        Assert.That(label.Encloses(longPlate), Is.True);
    }

    [Test]
    public void HandlesOffAlsoHidesTheOnionGhostJoints()
    {
        Session.GetComponent<AnimatorModes>().Onion = true;
        Session.GetComponent<AnimatorModes>().Handles = false;
        Frame();

        Assert.That(Circles(3), Is.EqualTo(0));
        Assert.That(_canvas.PolygonColors, Has.Count.EqualTo(3 * (Package.Skeleton.Bones.Count - 1)));
    }

    [Test]
    public void BonesAreDrawnAsKitesUnlessTheModeIsOff()
    {
        Frame();
        Assert.That(Session.GetComponent<AnimatorModes>().Kites, Is.True);
        Assert.That(_canvas.PolygonColors, Has.Count.EqualTo(Package.Skeleton.Bones.Count - 1));

        Session.GetComponent<AnimatorModes>().Kites = false;
        Session.GetComponent<AnimatorSelection>().Bone = 1;
        Frame();

        Assert.That(_canvas.PolygonColors, Is.Empty);
        // Joints stay, so bones can still be seen, picked and selected.
        Assert.That(Circles(5), Is.EqualTo(2));
        Assert.That(Circles(8), Is.EqualTo(1));
    }

    [Test]
    public void TurningKitesOffAlsoClearsTheOnionGhostKites()
    {
        Session.GetComponent<AnimatorModes>().Onion = true;
        Frame();
        int withKites = _canvas.PolygonColors.Count;
        Assert.That(withKites, Is.EqualTo(3 * (Package.Skeleton.Bones.Count - 1)));

        Session.GetComponent<AnimatorModes>().Kites = false;
        Frame();

        Assert.That(_canvas.PolygonColors, Is.Empty);
        Assert.That(Circles(3), Is.EqualTo(2 * Package.Skeleton.Bones.Count));
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
    public void TheHeaderNamesTheLoopRangeWhenLooping()
    {
        Idle.Loop = true;
        Idle.LoopStart = 0.25f;
        Idle.LoopEnd = 1.5f;
        Frame();

        Entity caption = Session.GetComponent<AnimatorUi>().TimelineCaption;
        Assert.That(
            caption.GetComponent<Text>().Value,
            Does.Contain("loop 0.25-1.5s"));
    }

    [Test]
    public void AnExpandedTrackDrawsItsChannelRows()
    {
        ref AnimatorTracks tracks = ref Session.GetComponent<AnimatorTracks>();
        tracks.Expanded ??= new System.Collections.Generic.HashSet<XafTrack>();
        tracks.Expanded.Add(new XafTrack(XafChannelTarget.Bone, "head"));
        Frame();

        Assert.That(_canvas.Texts, Has.Some.StartsWith("▾ head"));
        Assert.That(_canvas.Texts, Does.Contain("rot"));
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
        public int ChainFrames;
        public int ChainPoints;

        public void Reload(XafPackage package) => Reloads++;

        public void Apply(Transform2D[] attachments, Transform2D placement) { }

        public void ApplyChains(XafPackage package, XafChainSimulation simulation, Transform2D placement)
        {
            ChainFrames++;
            ChainPoints = simulation.Points(0).Count;
        }
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        private int _clears;

        public List<float> LastFrameCircles { get; private set; } = new();
        public List<string> Texts { get; } = new();

        /// <summary>One-pixel-high rects drawn this frame: the strips of key diamonds.</summary>
        public int StripCount { get; private set; }

        /// <summary>Sizes of every rect drawn this frame, which tells the attachment squares apart.</summary>
        public List<Vector2> RectSizes { get; } = new();

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
                RectSizes.Clear();
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
            RectSizes.Add(bounds.Size);
            if (bounds.Size.Y == 1f)
                StripCount++;
        }
        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }
        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }
    }
}
