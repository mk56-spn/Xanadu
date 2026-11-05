using System;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Editor.Recovery;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorDialogSystemTests : EcsSystemTest
{
    private Entity _editor;
    private EditorCommandBus _commands = null!;
    private string _recoveryFolder = "";

    protected override void OnEcsSetUp()
    {
        _commands = new EditorCommandBus();
        _editor = Store.CreateEntity(
            new LevelInfo("Test", "") { Background = new Color("#204060") },
            new EditorHud());
        EditorScene.BuildDialogs(Store, default, _editor, new Vector2(1280, 720), new FakeVisualRidFactory());
        _recoveryFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xanadu-dialog-tests-" + Guid.NewGuid().ToString("N"));
        Root.Add(new EditorDialogSystem(Store, _commands, new FakeVisualRidFactory(), new LevelRecoveryStore(_recoveryFolder)));
    }

    [TearDown]
    public void RemoveRecoveryFolder()
    {
        if (System.IO.Directory.Exists(_recoveryFolder))
            System.IO.Directory.Delete(_recoveryFolder, recursive: true);
    }

    private bool Shown(EditorDialogKind kind) => !UiLayout.IsHidden(EditorDialogs.Find(Store, kind));

    private Entity Button(EditorDialogAction action)
    {
        foreach (Entity entity in Store.Query<EditorDialogButton>().WithDisabled().ToEntityList())
        {
            if (entity.GetComponent<EditorDialogButton>().Action == action)
                return entity;
        }

        throw new InvalidOperationException($"no {action} button");
    }

    private void Click(Entity button)
    {
        button.AddTag<Pressed>();
        Frame();
        button.RemoveTag<Pressed>();
        Frame();
    }

    [Test]
    public void EveryDialogStartsClosed()
    {
        Frame();

        foreach (EditorDialogKind kind in Enum.GetValues<EditorDialogKind>())
            Assert.That(Shown(kind), Is.False, $"{kind}");
        Assert.That(EditorDialogs.AnyOpen(Store), Is.False);
    }

    [Test]
    public void ARecoveryOfferAsksOnceAndDiscardingDropsIt()
    {
        _editor.AddComponent(new EditorRecoveryOffer("", new LevelDocument(), DateTimeOffset.UtcNow));
        Frame();

        Assert.That(Shown(EditorDialogKind.Recovery), Is.True);
        Assert.That(_editor.GetComponent<EditorRecoveryOffer>().Asked, Is.True);

        // Escape and CLOSE do not answer it.
        EditorDialogs.Dismiss(Store);
        Assert.That(Shown(EditorDialogKind.Recovery), Is.True);

        Click(Button(EditorDialogAction.Discard));
        Assert.That(Shown(EditorDialogKind.Recovery), Is.False);
        Assert.That(_editor.HasComponent<EditorRecoveryOffer>(), Is.False);
        Assert.That(_commands.Count, Is.Zero, "discarding loads nothing");
    }

    [Test]
    public void TheMapColourDialogShowsTheColourAndAppliesThePickerLive()
    {
        EditorDialogs.Open(Store, EditorDialogKind.MapColor, (int)EditorColorTarget.Background);
        Frame();

        Assert.That(Shown(EditorDialogKind.MapColor), Is.True);
        Entity picker = _editor.GetComponent<EditorDialogs>().ColorPicker;
        Assert.That(picker.GetComponent<UiColorPicker>().Color.IsEqualApprox(new Color("#204060")), Is.True);
        Assert.That(_commands.Count, Is.Zero, "opening changes nothing");

        picker.GetComponent<UiColorPicker>().Color = new Color("#e0a020");
        Frame();

        Assert.That(_commands.TryDequeue(out EditorCommand command), Is.True);
        Assert.That(command.Type, Is.EqualTo(EditorCommandType.SetBackgroundColor));
        Assert.That(command.ColorValue!.Value.IsEqualApprox(new Color("#e0a020")), Is.True);

        Click(Button(EditorDialogAction.Close));
        Assert.That(EditorDialogs.AnyOpen(Store), Is.False);
    }

    [Test]
    public void ShaderButtonsActOnThePickedSlot()
    {
        _editor.AddComponent(LevelShaders.Create());
        EditorDialogs.Open(Store, EditorDialogKind.Shaders);
        Frame();

        Entity third = default;
        foreach (Entity entity in Store.Query<EditorDialogButton>().WithDisabled().ToEntityList())
        {
            EditorDialogButton button = entity.GetComponent<EditorDialogButton>();
            if (button.Action == EditorDialogAction.PickShader && button.Arg == 3)
                third = entity;
        }

        Assert.That(third.IsNull, Is.False, "a row per slot");
        Click(third);
        Click(Button(EditorDialogAction.ApplyShader));

        Assert.That(_commands.TryDequeue(out EditorCommand command), Is.True);
        Assert.That(command.Type, Is.EqualTo(EditorCommandType.AssignShader));
        Assert.That(command.IntValue, Is.EqualTo(3));
    }
}
