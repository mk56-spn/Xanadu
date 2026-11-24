using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Ui.Terminal;

/// <summary>
/// The text terminal: Ctrl+` drops it down over the top of any screen, Ctrl+`
/// or Esc puts it away (plain ` is the game's docs). It edits its own line (typing, Backspace, Ctrl+V), Enter runs
/// it through <see cref="TerminalCommands"/>, Up and Down walk back through what
/// was run, Tab completes a command or an argument, Page Up and Page Down scroll
/// the log. While it is down it has the keyboard: it takes focus from any text
/// field or editor, and the host holds the exe's shortcuts back
/// (<see cref="IsOpen"/>).
/// </summary>
public sealed class TerminalSystem : BaseSystem
{
    private const int FontSize = UiType.Small;
    private const int Lines = 16;
    private const float LineHeight = 24;
    private const float Pad = 16;
    private const int MaxLog = 400;
    private const int MaxHistory = 100;

    private static readonly Key[] Watched =
    [
        DebugKeys.Terminal, Key.Escape, Key.Enter, Key.KpEnter, Key.Up, Key.Down, Key.Tab, Key.Pageup, Key.Pagedown, Key.V,
    ];

    private readonly EntityStore _store;
    private readonly TerminalCommands _commands;
    private readonly IKeyboardState? _keyboard;
    private readonly List<(string Text, Color Colour)> _log = [];
    private readonly List<string> _history = [];
    private readonly HashSet<Key> _down = [];
    private readonly HashSet<Key> _pressed = [];
    private readonly KeyRepeat _backspace = new(0.4f, 0.05f, 0.03f);

    private Entity _layer;
    private readonly Entity[] _lines = new Entity[Lines];
    private Entity _input;
    private string _line = "";
    private int _historyAt = -1;
    private int _scroll;
    private float _clock;
    private bool _dirty = true;
    private string _shownInput = "";

    public TerminalSystem(EntityStore store, TerminalCommands commands, IKeyboardState? keyboard = null)
    {
        _store = store;
        _commands = commands;
        _keyboard = keyboard;
        _commands.Add(new TerminalCommand("clear", "Empties the log", [], _ =>
        {
            _log.Clear();
            _scroll = 0;
            return "";
        }));
        Print("Xanadu terminal. Type help for the commands; Esc or Ctrl+` closes.", DebugPalette.TextDim);
    }

    public override string Name => "Terminal";

    /// <summary>True while the terminal is down and has the keyboard.</summary>
    public bool IsOpen => !_layer.IsNull;

    /// <summary>Adds lines to the log, as a command's answer would.</summary>
    public void Print(string text, Color colour)
    {
        foreach (string line in text.Split('\n'))
            _log.Add((line, colour));
        if (_log.Count > MaxLog)
            _log.RemoveRange(0, _log.Count - MaxLog);
        _scroll = 0;
        _dirty = true;
    }

    protected override void OnUpdateGroup()
    {
        float delta = Tick.deltaTime;
        _clock += delta;
        ReadKeys();
        ITextEntry entry = HostPlatform.TextEntry;

        if (!IsOpen)
        {
            // Ctrl+` opens it from anywhere, a focused field or code editor too: with Ctrl held the
            // ` is no character (editors type nothing while Ctrl is down), and Open takes the focus.
            if (_pressed.Contains(DebugKeys.Terminal) && Ctrl)
                Open();
            return;
        }

        if ((_pressed.Contains(DebugKeys.Terminal) && Ctrl) || _pressed.Contains(Key.Escape))
        {
            Close();
            return;
        }

        entry.Claimed = true;
        Edit(entry, delta);
        Show();
    }

    private IKeyboardState Keyboard => _keyboard ?? HostPlatform.Keyboard;

    private bool Ctrl => Keyboard.IsPressed(Key.Ctrl) || Keyboard.IsPressed(Key.Meta);

    private void ReadKeys()
    {
        _pressed.Clear();
        foreach (Key key in Watched)
        {
            bool down = Keyboard.IsPressed(key);
            if (down && !_down.Contains(key))
                _pressed.Add(key);
            if (down)
                _down.Add(key);
            else
                _down.Remove(key);
        }
    }

    private void Edit(ITextEntry entry, float delta)
    {
        string typed = entry.TakeTyped().Replace("`", "");
        if (Ctrl && _pressed.Contains(Key.V))
            typed += entry.Clipboard.ReplaceLineEndings(" ");
        if (typed.Length > 0)
        {
            _line += typed;
            _historyAt = -1;
        }

        int deletes = _backspace.Update(Keyboard.IsPressed(Key.Backspace) ? 1 : 0, delta);
        for (int i = 0; i < deletes && _line.Length > 0; i++)
            _line = _line[..^(char.IsLowSurrogate(_line[^1]) && _line.Length > 1 ? 2 : 1)];

        if (_pressed.Contains(Key.Enter) || _pressed.Contains(Key.KpEnter))
        {
            string line = _line;
            _line = "";
            _historyAt = -1;
            Run(line);
        }
        if (_pressed.Contains(Key.Up))
            Recall(+1);
        if (_pressed.Contains(Key.Down))
            Recall(-1);
        if (_pressed.Contains(Key.Tab))
            Complete();
        if (_pressed.Contains(Key.Pageup))
            Scroll(Lines - 1);
        if (_pressed.Contains(Key.Pagedown))
            Scroll(-(Lines - 1));
    }

    /// <summary>Drops the terminal down, as Ctrl+` would.</summary>
    public void Show(bool open)
    {
        if (open && !IsOpen)
            Open();
        else if (!open)
            Close();
    }

    /// <summary>Runs a line as if it were typed and Enter pressed: echoed, kept in the history, answered in the log.</summary>
    public void Run(string line)
    {
        line = line.Trim();
        if (line.Length == 0)
            return;

        if (_history.Count == 0 || _history[^1] != line)
            _history.Add(line);
        if (_history.Count > MaxHistory)
            _history.RemoveAt(0);

        Print("> " + line, DebugPalette.Title);
        TerminalReply reply = _commands.Run(line);
        if (reply.Text.Length > 0)
            Print(reply.Text, reply.Failed ? DebugPalette.Warning : DebugPalette.Text);
    }

    /// <summary>Up goes back through what was run, Down forwards and out to an empty line.</summary>
    private void Recall(int step)
    {
        if (_history.Count == 0)
            return;
        _historyAt = Math.Clamp(_historyAt + step, -1, _history.Count - 1);
        _line = _historyAt < 0 ? "" : _history[_history.Count - 1 - _historyAt];
    }

    /// <summary>One match fills in; several fill their shared start and are listed.</summary>
    private void Complete()
    {
        IReadOnlyList<string> options = _commands.Complete(_line);
        if (options.Count == 0)
            return;

        int cut = _line.Length;
        while (cut > 0 && !char.IsWhiteSpace(_line[cut - 1]))
            cut--;
        string shared = options[0];
        foreach (string option in options)
        {
            int n = 0;
            while (n < shared.Length && n < option.Length && char.ToLowerInvariant(shared[n]) == char.ToLowerInvariant(option[n]))
                n++;
            shared = shared[..n];
        }

        if (options.Count == 1)
            _line = _line[..cut] + options[0] + " ";
        else
        {
            if (shared.Length > _line.Length - cut)
                _line = _line[..cut] + shared;
            Print(string.Join("   ", options), DebugPalette.TextDim);
        }
    }

    private void Scroll(int lines)
    {
        int most = Math.Max(0, _log.Count - Lines);
        _scroll = Math.Clamp(_scroll + lines, 0, most);
        _dirty = true;
    }

    private void Open()
    {
        // The terminal takes the keyboard: no field or editor keeps focus under it.
        var focused = new List<Entity>();
        foreach (Entity entity in _store.Query().AnyTags(Tags.Get<TextFieldFocused, TextEditorFocused>()).Entities)
            focused.Add(entity);
        foreach (Entity entity in focused)
        {
            entity.RemoveTag<TextFieldFocused>();
            entity.RemoveTag<TextEditorFocused>();
        }

        ITextEntry entry = HostPlatform.TextEntry;
        entry.Claimed = true;
        entry.TakeTyped();
        _backspace.Ignore(Keyboard.IsPressed(Key.Backspace) ? 1 : 0);

        Vector2 stage = UiStage.Full.Size;
        float height = Pad * 2 + LineHeight * (Lines + 1) + 8;
        UiNode layer = UiNode.Layer(_store, default, UiLayers.Terminal, null, UiStage.Full).Tag<DebugUi>();
        _layer = layer.Entity;
        UiNode panel = layer.Column()
            .Size(stage.X, height)
            .At(new Vector2(stage.X / 2, height / 2))
            .Fill(DebugPalette.Panel)
            .Border(DebugPalette.PanelBorder)
            .Pad(Pad)
            .Gap(0);

        float width = stage.X - Pad * 2;
        for (int i = 0; i < Lines; i++)
            _lines[i] = panel.Text("", FontSize, DebugPalette.Text).Mono().Size(width, LineHeight).Entity;
        panel.Column().Size(width, 8);
        _input = panel.Text("", FontSize, DebugPalette.Title).Mono().Size(width, LineHeight).Entity;
        _shownInput = "\0";
        _dirty = true;
        Show();
    }

    private void Close()
    {
        if (_layer.IsNull)
            return;
        UiLayout.DeleteSubtree(_layer);
        _layer = default;
        HostPlatform.TextEntry.TakeTyped();
        HostPlatform.TextEntry.Claimed = false;
    }

    /// <summary>Puts the log's last lines (less any scroll) and the line being typed on screen.</summary>
    private void Show()
    {
        if (_dirty)
        {
            _dirty = false;
            int first = Math.Max(0, _log.Count - Lines - _scroll);
            for (int i = 0; i < Lines; i++)
            {
                int at = first + i;
                (string text, Color colour) = at < _log.Count - _scroll && at < _log.Count ? _log[at] : ("", DebugPalette.Text);
                UiText.Set(_lines[i], text);
                ref TextStyle style = ref _lines[i].GetComponent<TextStyle>();
                if (style.Color != colour)
                {
                    style.Color = colour;
                    _lines[i].AddTag<Dirty>();
                }
            }
        }

        bool caret = _clock % 1f < 0.55f;
        string input = "> " + _line + (caret ? "_" : "");
        if (input != _shownInput)
        {
            _shownInput = input;
            UiText.Set(_input, input);
        }
    }
}
