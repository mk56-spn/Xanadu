using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>
/// The keyboard side of <see cref="AnimatorBindings"/>: the table itself, and
/// keys running the same actions the buttons carry.
/// </summary>
[TestFixture]
public sealed class AnimatorBindingTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorModeSystem());
        Root.Add(new AnimatorShortcutSystem(Keyboard));
    }

    private void Tap(params Key[] keys)
    {
        foreach (Key key in keys)
            Keyboard.Press(key);
        Frame();
        Keyboard.Clear();
        Frame();
    }

    private string Status => Session.GetComponent<AnimatorStatus>().Message;

    [Test]
    public void NoTwoBindingsShareTheSameChord()
    {
        var chords = new HashSet<(Key, bool, bool)>();
        foreach (AnimatorBindings.Binding binding in AnimatorBindings.All)
        {
            Assert.That(
                chords.Add((binding.Key, binding.Ctrl, binding.Shift)),
                Is.True,
                $"{binding.Action.Kind} reuses {binding.Key} {binding.Ctrl} {binding.Shift}");
        }
    }

    /// <summary>
    /// Every action a button can request has a key, apart from the ones that
    /// only make sense from the pointer: picking a clip or bone, choosing the
    /// parent of a reparent, and the menus opened at the pointer.
    /// </summary>
    [Test]
    public void EveryButtonActionHasAKey()
    {
        var contextual = new HashSet<ToolActionKind>
        {
            ToolActionKind.Play,
            ToolActionKind.Stop,
            ToolActionKind.Clip,
            ToolActionKind.SelectBone,
            ToolActionKind.SelectAttachment,
            ToolActionKind.SelectChain,
            ToolActionKind.PickChainArt,
            ToolActionKind.PickChainBone,
            ToolActionKind.PickColliderBone,
            ToolActionKind.ReparentBone,
            ToolActionKind.AddKey,
            ToolActionKind.AddSvg,
            ToolActionKind.AttachSvg,
        };

        var bound = new HashSet<ToolActionKind>();
        foreach (AnimatorBindings.Binding binding in AnimatorBindings.All)
            bound.Add(binding.Action.Kind);

        foreach (ToolActionKind kind in Enum.GetValues<ToolActionKind>())
        {
            if (contextual.Contains(kind))
                continue;

            Assert.That(bound, Does.Contain(kind), $"{kind} has no key");
        }
    }

    [Test]
    public void AModifiedKeyIsADifferentBinding()
    {
        Assert.That(
            AnimatorBindings.Find(Key.O, ctrl: false, shift: false)!.Value.Action.Argument,
            Is.EqualTo(nameof(AnimatorMode.Onion)));
        Assert.That(
            AnimatorBindings.Find(Key.O, ctrl: true, shift: false)!.Value.Action.Kind,
            Is.EqualTo(ToolActionKind.Load));
        Assert.That(AnimatorBindings.Find(Key.O, ctrl: false, shift: true), Is.Null);
    }

    [Test]
    public void XTogglesTheBoneKitesAndTheirButtonFollows()
    {
        Entity toggle = ModeButton(AnimatorMode.Kites);
        Frame();
        Assert.That(Session.GetComponent<AnimatorModes>().Kites, Is.True);
        Assert.That(toggle.GetComponent<Toggle>().Value, Is.True);

        Tap(Key.X);
        Frame();
        Assert.That(Session.GetComponent<AnimatorModes>().Kites, Is.False);
        Assert.That(toggle.GetComponent<Toggle>().Value, Is.False);
        Assert.That(Status, Is.EqualTo("BONE KITES OFF"));

        Tap(Key.X);
        Assert.That(Session.GetComponent<AnimatorModes>().Kites, Is.True);
        Assert.That(Status, Is.EqualTo("BONE KITES ON"));
    }

    [Test]
    public void JTogglesTheHandlesAndTheirButtonFollows()
    {
        Entity toggle = ModeButton(AnimatorMode.Handles);
        Frame();
        Assert.That(Session.GetComponent<AnimatorModes>().Handles, Is.True);
        Assert.That(toggle.GetComponent<Toggle>().Value, Is.True);

        Tap(Key.J);
        Frame();
        Assert.That(Session.GetComponent<AnimatorModes>().Handles, Is.False);
        Assert.That(toggle.GetComponent<Toggle>().Value, Is.False);
        Assert.That(Status, Is.EqualTo("HANDLES OFF"));
    }

    [Test]
    public void TheModeKeysFlipTheirModesAndSaySo()
    {
        Tap(Key.A);
        Assert.That(Session.GetComponent<AnimatorModes>().AutoKey, Is.True);
        Assert.That(Status, Is.EqualTo("AUTO KEY ON"));

        Tap(Key.O);
        Assert.That(Session.GetComponent<AnimatorModes>().Onion, Is.True);
        Assert.That(Status, Is.EqualTo("ONION ON"));

        Tap(Key.R);
        Assert.That(Session.GetComponent<AnimatorModes>().Rest, Is.True);
        Assert.That(Status, Is.EqualTo("REST POSE"));

        Tap(Key.G);
        Assert.That(Session.GetComponent<AnimatorModes>().Svg, Is.True);
        Assert.That(Status, Is.EqualTo("SVG PLACEMENT"));

        Tap(Key.A);
        Assert.That(Session.GetComponent<AnimatorModes>().AutoKey, Is.False);
        Assert.That(Status, Is.EqualTo("AUTO KEY OFF"));
    }

    [Test]
    public void AModeKeyAlsoMovesItsToggleButton()
    {
        Entity toggle = ModeButton(AnimatorMode.AutoKey);

        Tap(Key.A);
        Frame();

        Assert.That(toggle.GetComponent<Toggle>().Value, Is.True);

        Tap(Key.A);
        Frame();

        Assert.That(toggle.GetComponent<Toggle>().Value, Is.False);
    }

    [Test]
    public void SpacePlaysAndThenStops()
    {
        AnimatorPlayback playback = Session.GetComponent<AnimatorDocument>().Playback;
        playback.Stop();

        Tap(Key.Space);
        Assert.That(playback.IsPlaying, Is.True);
        Assert.That(Status, Is.EqualTo("PLAYING"));

        Tap(Key.Space);
        Assert.That(playback.IsPlaying, Is.False);
        Assert.That(Status, Is.EqualTo("STOPPED"));
    }

    [Test]
    public void DeleteRemovesTheSelectedKey()
    {
        AnimatorKeyCommands.SelectKey(
            Session,
            new XafTrack(XafChannelTarget.Bone, "head"),
            1f);
        Assert.That(Idle.Channels[0].Keyframes, Has.Count.EqualTo(3));

        Tap(Key.Delete);

        Assert.That(Idle.Channels[0].Keyframes, Has.Count.EqualTo(2));
        Assert.That(Session.GetComponent<AnimatorSelection>().HasKey, Is.False);
    }

    [Test]
    public void ShiftDeleteDeletesTheSelectedBone()
    {
        Session.GetComponent<AnimatorSelection>().Bone = 2;

        Tap(Key.Shift, Key.Delete);

        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(2));
        Assert.That(Package.Skeleton.Bones.Exists(bone => bone.Id == "head"), Is.False);
    }

    [Test]
    public void AddingABoneWithAKeyCanBeUndoneWithControlZ()
    {
        Tap(Key.B);
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(4));

        Tap(Key.Ctrl, Key.Z);
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(3));
    }

    [Test]
    public void VHangsAChainOffTheSelectedBoneAndControlDeleteRemovesIt()
    {
        Session.GetComponent<AnimatorSelection>().Bone = 2;

        Tap(Key.V);
        Assert.That(Package.Chains, Has.Count.EqualTo(1));
        Assert.That(Package.Chains[0].BoneId, Is.EqualTo("head"));

        Tap(Key.Ctrl, Key.Delete);
        Assert.That(Package.Chains, Is.Empty);
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(3));
    }

    [Test]
    public void TheNumberKeysSetTheSelectedKeysEasing()
    {
        AnimatorKeyCommands.SelectKey(
            Session,
            new XafTrack(XafChannelTarget.Bone, "head"),
            1f);

        Tap(Key.Key3);

        Assert.That(
            XafEasing.Read(Idle, XafChannelTarget.Bone, "head", 1f),
            Is.EqualTo(XafEasePreset.Ease));
    }

    [Test]
    public void ControlOStillLoadsRatherThanTogglingOnion()
    {
        Tap(Key.Ctrl, Key.O);

        Assert.That(Files.LoadedPath, Is.EqualTo("res://test.xaf"));
        Assert.That(Session.GetComponent<AnimatorModes>().Onion, Is.False);
    }
}
