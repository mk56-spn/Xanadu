using System.Collections.Generic;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Ui.Editing;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class TextEditorInputSystemTests : EcsSystemTest
{
    private static readonly Rect2 Area = new(0, 0, 400, 300);

    private ScriptedPointer _pointer = null!;
    private FakeKeyboard _keyboard = null!;
    private FakeEntry _entry = null!;
    private ITextEntry _hostEntry = null!;

    protected override void OnEcsSetUp()
    {
        _pointer = new ScriptedPointer();
        _keyboard = new FakeKeyboard();
        _entry = new FakeEntry();
        _hostEntry = HostPlatform.TextEntry;
        HostPlatform.TextEntry = _entry;
        Root.Add(new TextEditorInputSystem(_pointer, _keyboard));
        Root.Add(new TextEditorDrawSystem(HostPlatform.NewCanvas()));
    }

    [TearDown]
    public void RestoreEntry() => HostPlatform.TextEntry = _hostEntry;

    [Test]
    public void TypingGoesIntoTheFocusedEditorAndClaimsTheKeyboard()
    {
        Entity editor = TextEditors.Create(Store, default, Area, "ab");
        Frame();
        _entry.Typed = "x";
        Frame();

        TextBuffer buffer = editor.GetComponent<TextEditor>().Buffer;
        Assert.That(buffer.Text, Is.EqualTo("xab"));
        Assert.That(editor.GetComponent<TextEditor>().Frame.Edited, Is.True);
        Assert.That(_entry.Claimed, Is.True);
    }

    [Test]
    public void APressOutsideUnfocusesAndReleasesTheKeyboard()
    {
        Entity editor = TextEditors.Create(Store, default, Area, "ab");
        Frame();
        _pointer.Current = new PointerInput(new Vector2(600, 50), true);
        Frame();
        _entry.Typed = "x";
        Frame();

        Assert.That(editor.Tags.Has<TextEditorFocused>(), Is.False);
        Assert.That(editor.GetComponent<TextEditor>().Buffer.Text, Is.EqualTo("ab"));
        Assert.That(_entry.Claimed, Is.False);
    }

    [Test]
    public void APressInsidePlacesTheCaretAndFocuses()
    {
        Entity editor = TextEditors.Create(Store, default, Area, "hello");
        editor.RemoveTag<TextEditorFocused>();
        // Drawn once, so there are rows to hit.
        Frame();
        float x = editor.GetComponent<TextEditor>().Layout.TextLeft + 3 * editor.GetComponent<TextEditor>().Layout.CharWidth;
        _pointer.Current = new PointerInput(new Vector2(x, TextEditorLayout.Inset + 5), true);
        Frame();

        Assert.That(editor.Tags.Has<TextEditorFocused>(), Is.True);
        Assert.That(editor.GetComponent<TextEditor>().Buffer.Column, Is.EqualTo(3));
    }

    [Test]
    public void CtrlZUndoesARunOfTyping()
    {
        Entity editor = TextEditors.Create(Store, default, Area, "");
        // Typing is taken once the keyboard is claimed, which the first frame does.
        Frame();
        foreach (string c in new[] { "a", "b", "c" })
        {
            _entry.Typed = c;
            Frame();
        }

        // Moving the caret ends the run.
        _keyboard.Down.Add(Key.Left);
        Frame();
        _keyboard.Down.Clear();
        _keyboard.Down.Add(Key.Ctrl);
        _keyboard.Down.Add(Key.Z);
        Frame();

        Assert.That(editor.GetComponent<TextEditor>().Buffer.Text, Is.EqualTo(""));
        Assert.That(editor.GetComponent<TextEditor>().Frame.SteppedHistory, Is.True);
    }

    [Test]
    public void TheHostTakesKeysFirst()
    {
        var host = new TakesEnter();
        Entity editor = TextEditors.Create(Store, default, Area, "ab", host);
        _keyboard.Down.Add(Key.Enter);
        Frame();

        Assert.That(host.Taken, Is.EqualTo(1));
        Assert.That(editor.GetComponent<TextEditor>().Buffer.Lines, Has.Count.EqualTo(1));
    }

    [Test]
    public void CtrlSlashCommentsOnlyWhereTheHostHasLineComments()
    {
        Entity plain = TextEditors.Create(Store, default, Area, "ab");
        _keyboard.Down.Add(Key.Ctrl);
        _keyboard.Down.Add(Key.Slash);
        Frame();
        Assert.That(plain.GetComponent<TextEditor>().Buffer.Text, Is.EqualTo("ab"));

        plain.DeleteEntity();
        _keyboard.Down.Clear();
        Frame();
        Entity code = TextEditors.Create(Store, default, Area, "ab", new Comments());
        _keyboard.Down.Add(Key.Ctrl);
        _keyboard.Down.Add(Key.Slash);
        Frame();
        Assert.That(code.GetComponent<TextEditor>().Buffer.Text, Is.EqualTo("// ab"));
    }

    private sealed class Comments : ITextEditorHost
    {
        public string LineComment => "//";
    }

    private sealed class TakesEnter : ITextEditorHost
    {
        public int Taken;

        public bool TakeKey(TextBuffer buffer, Key key, bool shift, bool ctrl)
        {
            if (key != Key.Enter)
                return false;
            Taken++;
            return true;
        }
    }

    private sealed class ScriptedPointer : IPointerInputSource
    {
        public PointerInput Current;

        public PointerInput Read() => Current;
    }

    private sealed class FakeKeyboard : IKeyboardState
    {
        public readonly HashSet<Key> Down = new();

        public bool IsPressed(Key key) => Down.Contains(key);
    }

    private sealed class FakeEntry : ITextEntry
    {
        public string Typed = "";

        public bool Claimed { get; set; }

        public string TakeTyped()
        {
            string typed = Typed;
            Typed = "";
            return typed;
        }

        public string Clipboard { get; set; } = "";
    }
}
