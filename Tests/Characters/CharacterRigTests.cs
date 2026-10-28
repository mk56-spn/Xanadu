    using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Characters;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Characters;

[TestFixture]
public sealed class CharacterRigTests : EcsSystemTest
{
    private FakeRigFiles _files = null!;
    private List<FakeVisuals> _visuals = null!;

    protected override void OnEcsSetUp()
    {
        _files = new FakeRigFiles();
        _visuals = new List<FakeVisuals>();
        Root.Add(new CharacterRigLoadSystem(_files));
        Root.Add(new CharacterRigAnimateSystem());
        Root.Add(new CharacterRigDrawSystem(() =>
        {
            var visuals = new FakeVisuals();
            _visuals.Add(visuals);
            return visuals;
        }));
    }

    private Entity Player(CharacterKind kind = CharacterKind.Contact, PlayfieldKind mode = PlayfieldKind.Play) =>
        Store.CreateEntity(
            new PlayerCharacter(kind),
            new PlayfieldMode(mode),
            new Player(new Vector2(30, 100)),
            new Position(new Vector2(400, 300)));

    [Test]
    public void ContactLoadsItsXafAndStartsTheIdleClip()
    {
        Entity player = Player();
        Frame();

        Assert.That(_files.Loaded, Is.EqualTo(new[] { CharacterRigs.ContactPath }));
        Assert.That(player.GetComponent<CharacterRig>().Runtime.CurrentClip, Is.EqualTo("idle"));
        Assert.That(player.Tags.Has<CharacterRigVisible>(), Is.True);
    }

    [Test]
    public void TheRigIsLoadedOnceAndKeepsAnimating()
    {
        Entity player = Player();
        Frame(10);

        Assert.That(_files.Loaded, Has.Count.EqualTo(1));
        Assert.That(_visuals, Has.Count.EqualTo(1));
        Assert.That(player.GetComponent<CharacterRig>().Runtime.CurrentTime, Is.GreaterThan(0f));
    }

    [Test]
    public void TheRigsChainsAreSimulatedAndHandedToTheVisualsEveryFrameThroughThePlacement()
    {
        _files.Package = RigPackage();
        _files.Package.Chains.Add(new XafChain { Id = "hair", BoneId = "torso", Segments = 6 });
        Player();
        Frame(10);

        FakeVisuals visuals = _visuals[0];
        Assert.That(visuals.ChainFrames, Is.EqualTo(10));
        Assert.That(visuals.ChainPoints, Is.EqualTo(7));
        // The same rig-to-canvas placement the attachments use, so hair scales and stands with the body.
        Assert.That(visuals.ChainPlacement.X.Length(), Is.GreaterThan(0f));
        Assert.That(visuals.ChainPlacement.X.Length(), Is.EqualTo(100f / 200f).Within(0.0001f));
    }

    [Test]
    public void ARigWithoutChainsStillDrawsAndAsksForNoChainPoints()
    {
        Player();
        Frame(3);

        Assert.That(_visuals[0].ChainFrames, Is.EqualTo(3));
        Assert.That(_visuals[0].ChainPoints, Is.EqualTo(0));
    }

    [Test]
    public void AMissingFileKeepsThePlainBoxAndIsNotRetried()
    {
        _files.Package = null;
        Entity player = Player();
        Frame(3);

        Assert.That(player.Tags.Has<CharacterRigMissing>(), Is.True);
        Assert.That(player.Tags.Has<CharacterRigVisible>(), Is.False);
        Assert.That(_files.Loaded, Has.Count.EqualTo(1));
    }

    [Test]
    public void SwitchingToACharacterWithoutARigDropsItAndReleasesTheArt()
    {
        Entity player = Player();
        Frame();
        player.GetComponent<PlayerCharacter>().Kind = CharacterKind.Mage;
        Frame();

        Assert.That(player.HasComponent<CharacterRig>(), Is.False);
        Assert.That(player.Tags.Has<CharacterRigVisible>(), Is.False);
        Assert.That(_visuals[0].Released, Is.True);
    }

    [Test]
    public void DeletingThePlayerReleasesTheArt()
    {
        Entity player = Player();
        Frame();
        player.DeleteEntity();
        Frame();

        Assert.That(_visuals[0].Released, Is.True);
    }

    [Test]
    public void EditorPlayfieldsAreLeftAlone()
    {
        Player(mode: PlayfieldKind.Editor);
        Frame();

        Assert.That(_files.Loaded, Is.Empty);
    }

    [Test]
    public void ArtIsScaledToThePlayerHeightAndStandsOnItsBottomEdge()
    {
        // Rig art 0..200 tall, feet at y = 0, drawn 100 px tall with feet at (400, 350).
        Transform2D placement = CharacterRigDrawSystem.Placement(
            new Rect2(-25, -200, 50, 200),
            new Vector2(400, 350),
            100);

        Assert.That(placement * new Vector2(0, 0), Is.EqualTo(new Vector2(400, 350)));
        Assert.That(placement * new Vector2(0, -200), Is.EqualTo(new Vector2(400, 250)));
    }

    [Test]
    public void RebasePointsResPathsAtTheProjectThatSavedThePackage()
    {
        string root = Path.Combine(Path.GetTempPath(), $"xaf-rebase-{Guid.NewGuid():N}");
        string characters = Path.Combine(root, "Animations", "Characters");
        Directory.CreateDirectory(characters);
        File.WriteAllText(Path.Combine(root, "project.godot"), "");
        try
        {
            XafPackage package = RigPackage();
            package.Attachments.Add(new XafAttachment { SvgPath = "/abs/kept.svg" });
            package.Chains.Add(new XafChain { Id = "hair", BoneId = "torso", SvgPath = "res://Assets/SVG/Char/hair_segment.svg" });
            package.Chains.Add(new XafChain { Id = "bare", BoneId = "torso", SvgPath = "", Group = "" });
            XafAssetPaths.Rebase(package, Path.Combine(characters, "contact.xaf"));

            Assert.That(package.Attachments[0].SvgPath,
                Is.EqualTo(Path.Combine(root, "Assets/SVG/torso.svg")));
            Assert.That(package.Attachments[1].SvgPath, Is.EqualTo("/abs/kept.svg"));
            Assert.That(package.Chains[0].SvgPath,
                Is.EqualTo(Path.Combine(root, "Assets/SVG/Char/hair_segment.svg")));
            Assert.That(package.Chains[1].SvgPath, Is.EqualTo(""));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static XafPackage RigPackage()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "root", Parent = -1, Rest = Transform2D.Identity });
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "torso",
            Parent = 0,
            Rest = new Transform2D(0f, new Vector2(0, -80)),
        });
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://Assets/SVG/torso.svg",
            Group = "torso",
            BoneId = "torso",
        });
        package.Clips.Add(new XafClip { Id = "walk", Duration = 1f });
        package.Clips.Add(new XafClip { Id = "idle", Duration = 2f, Loop = true, LoopEnd = 2f });
        return package;
    }

    private sealed class FakeRigFiles : ICharacterRigFiles
    {
        public XafPackage? Package = RigPackage();
        public List<string> Loaded { get; } = new();

        public XafPackage? Load(string path)
        {
            Loaded.Add(path);
            return Package;
        }
    }

    private sealed class FakeVisuals : IAttachmentVisuals
    {
        public bool Released;

        public int ChainFrames;
        public int ChainPoints;
        public Transform2D ChainPlacement;

        public void Reload(XafPackage package) { }
        public void Apply(Transform2D[] attachments, Transform2D placement) { }

        public void ApplyChains(XafPackage package, XafChainSimulation simulation, Transform2D placement)
        {
            ChainFrames++;
            ChainPoints = simulation.Points(0).Count;
            ChainPlacement = placement;
        }
        public Rect2 Bounds(Transform2D[] attachments) => new(-25, -200, 50, 200);
        public void Release() => Released = true;
    }
}
