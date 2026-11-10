using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Input;
using Xanadu.Systems.Scenes;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorShortcutsTests
{
    private static EditorShortcutScope Scopes(
        bool dialog = false,
        bool help = false,
        bool warning = false,
        bool typing = false,
        bool target = false,
        bool group = false) =>
        EditorShortcuts.ActiveScopes(dialog, help, warning, typing, target, group);

    private static EditorShortcut? Action(KeyChord chord, EditorShortcutScope scopes) =>
        EditorShortcuts.Find(chord, scopes)?.Action;

    [Test]
    public void ADialogStopsEveryShortcutButTheOnesThatCloseIt()
    {
        EditorShortcutScope dialog = Scopes(dialog: true);

        Assert.That(Action(new KeyChord(DebugKeys.Preview), dialog), Is.Null);
        Assert.That(Action(new KeyChord(Key.Delete), dialog), Is.Null);
        Assert.That(Action(new KeyChord(Key.Escape), dialog), Is.EqualTo(EditorShortcut.CloseDialog));
        Assert.That(Action(new KeyChord(DebugKeys.EditorShaders), dialog), Is.EqualTo(EditorShortcut.ToggleShaders));
    }

    [Test]
    public void TypingKeepsSaveButNotEditing()
    {
        EditorShortcutScope typing = Scopes(typing: true);

        Assert.That(Action(new KeyChord(Key.S, Ctrl: true), typing), Is.EqualTo(EditorShortcut.Save));
        Assert.That(Action(new KeyChord(Key.Delete), typing), Is.Null);
        Assert.That(Action(new KeyChord(Key.C, Ctrl: true), typing), Is.Null);
    }

    [Test]
    public void ChordsMatchTheirModifiersExactly()
    {
        Assert.That(Action(new KeyChord(Key.S, Ctrl: true, Shift: true), Scopes()), Is.Null);
        Assert.That(Action(new KeyChord(Key.Left), Scopes()), Is.EqualTo(EditorShortcut.MoveLeft));
        Assert.That(Action(new KeyChord(Key.Left, Shift: true), Scopes()), Is.EqualTo(EditorShortcut.Timeline));
    }

    [Test]
    public void EscapeDoesTheMostSpecificThing()
    {
        var escape = new KeyChord(Key.Escape);

        Assert.That(Action(escape, Scopes()), Is.EqualTo(EditorShortcut.Back));
        Assert.That(Action(escape, Scopes(group: true)), Is.EqualTo(EditorShortcut.CloseGroupLayer));
        Assert.That(Action(escape, Scopes(target: true)), Is.EqualTo(EditorShortcut.CloseTarget));
        Assert.That(Action(escape, Scopes(help: true)), Is.EqualTo(EditorShortcut.ToggleHelp));
    }

    [Test]
    public void TheGroupOverlayIsModalButStillPlays()
    {
        EditorShortcutScope group = Scopes(group: true);

        Assert.That(Action(new KeyChord(Key.Space), group), Is.EqualTo(EditorShortcut.Timeline));
        Assert.That(Action(new KeyChord(Key.Key1), group), Is.Null);
        Assert.That(Action(new KeyChord(Key.Delete), group), Is.Null);
    }

    [Test]
    public void AWarningStopsFieldKeysButNotGlobalOnes()
    {
        EditorShortcutScope warning = Scopes(warning: true);

        Assert.That(Action(new KeyChord(Key.Escape), warning), Is.Null);
        Assert.That(Action(new KeyChord(Key.Z, Ctrl: true), warning), Is.EqualTo(EditorShortcut.Undo));
    }

    [Test]
    public void NumberKeysAndTheKeypadChooseThePlacementMode()
    {
        foreach (Key key in new[] { Key.Key3, Key.Kp3 })
        {
            EditorShortcuts.Binding? binding = EditorShortcuts.Find(new KeyChord(key), Scopes());
            Assert.That(binding?.Action, Is.EqualTo(EditorShortcut.Place));
            Assert.That(binding?.Placement, Is.EqualTo(EditorPlacementMode.Dash));
        }
    }

    [Test]
    public void EveryTimelineKeyIsLiveOnTheField()
    {
        foreach ((KeyChord chord, Xanadu.Systems.Timeline.TimelineCommand command) in Xanadu.Systems.Timeline.TimelineShortcuts.All)
        {
            EditorShortcuts.Binding? binding = EditorShortcuts.Find(chord, Scopes());
            Assert.That(binding?.Action, Is.EqualTo(EditorShortcut.Timeline), chord.ToString());
            Assert.That(binding?.Timeline, Is.EqualTo(command), chord.ToString());
        }
    }

    [Test]
    public void FitAndRedoMatchTheAnimator()
    {
        Assert.That(Action(new KeyChord(Key.F), Scopes()), Is.EqualTo(EditorShortcut.FitView));
        Assert.That(Action(new KeyChord(Key.Z, Ctrl: true, Shift: true), Scopes()), Is.EqualTo(EditorShortcut.Redo));
    }

    private sealed class FakeTarget : IEditorShortcutTarget
    {
        public readonly List<EditorShortcut> Ran = new();

        public bool IsOpen { get; set; } = true;

        public EditorShortcutScope ActiveScopes { get; set; } = EditorShortcutScope.Field | EditorShortcutScope.Always;

        public void Run(EditorShortcuts.Binding binding) => Ran.Add(binding.Action);
    }

    private static (SystemRoot Root, KeyPressQueue Keys, EntityStore Store, KeybindSettings Keybinds) App()
    {
        var store = new EntityStore();
        var keybinds = new KeybindSettings(persist: false);
        var keys = new KeyPressQueue();
        var root = new SystemRoot(store);
        root.Add(new AppShortcutSystem(
            keys,
            keybinds,
            new SceneSystem(store, new SceneDirector(), new Vector2(1920, 1080), keybinds: keybinds)));
        return (root, keys, store, keybinds);
    }

    [Test]
    public void EveryTapBetweenFramesReachesTheOpenEditor()
    {
        var (root, keys, store, _) = App();
        var target = new FakeTarget();
        store.CreateEntity(new EditorShortcutHost(target));

        keys.Press(new KeyChord(Key.Space));
        keys.Press(new KeyChord(Key.R));
        root.Update(new UpdateTick(1f / 60f, 0));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(target.Ran, Is.EqualTo(new[] { EditorShortcut.Timeline, EditorShortcut.RotateSelection }));
    }

    [Test]
    public void AClosedEditorGetsNoKeys()
    {
        var (root, keys, store, _) = App();
        var target = new FakeTarget { IsOpen = false };
        store.CreateEntity(new EditorShortcutHost(target));

        keys.Press(new KeyChord(Key.Space));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(target.Ran, Is.Empty);
    }

    [Test]
    public void AKeyBeingCapturedIsNotAlsoAShortcut()
    {
        var (root, keys, store, keybinds) = App();
        var target = new FakeTarget();
        store.CreateEntity(new EditorShortcutHost(target));
        keybinds.BeginCapture(KeybindAction.Action1);

        keys.Press(new KeyChord(Key.Space));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(target.Ran, Is.Empty);
        Assert.That(keybinds.Get(KeybindAction.Action1), Is.EqualTo(Key.Space));
    }
}
