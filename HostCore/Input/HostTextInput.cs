using System.Text;
using Friflo.Engine.ECS;
using MoonWorks;
using MoonWorks.Input;
using SDL3;
using Xanadu.Ecs;

namespace Xanadu.Host.Input;

/// <summary>
/// Typing into whichever ECS <see cref="TextField"/> has focus, from SDL's
/// text-input events (which carry IME and layout-resolved characters). It does
/// what Godot's hidden <c>LineEdit</c> bridges did: the caret stays at the end,
/// Backspace deletes (repeating when held), Ctrl+V pastes, Enter submits, and
/// numeric fields keep digits, one dot and a leading minus.
/// </summary>
/// <remarks>
/// While a field has focus <see cref="Active"/> is true and the host passes
/// only Escape on to the game's shortcuts, as the <c>LineEdit</c> swallowed
/// the rest.
/// </remarks>
internal sealed class HostTextInput : Xanadu.Systems.Input.ITextEntry
{
    private const float RepeatDelay = 0.45f, RepeatEvery = 0.035f;

    private readonly EntityStore _entities;
    private readonly Window _window;
    private readonly StringBuilder _typed = new();
    private Entity _field;
    private float _backspaceHeld;

    public HostTextInput(EntityStore entities, Window window)
    {
        _entities = entities;
        _window = window;
        Inputs.TextInput += c => _typed.Append(c);
    }

    /// <summary>True while a text field has focus and keys go to it.</summary>
    public bool Active => !_field.IsNull;

    /// <summary>
    /// Set by something that runs its own text editing (the UI's <c>TextEditor</c>, through <c>HostPlatform.TextEntry</c>):
    /// SDL text input stays on, no field is typed into, and what is typed waits
    /// in <see cref="TakeTyped"/>.
    /// </summary>
    public bool Claimed { get; set; }

    /// <summary>What was typed since the last call, while <see cref="Claimed"/>.</summary>
    public string TakeTyped()
    {
        string typed = _typed.ToString();
        _typed.Clear();
        return typed;
    }

    public string Clipboard
    {
        get => SDL.SDL_GetClipboardText() ?? "";
        set => SDL.SDL_SetClipboardText(value);
    }

    public void Update(Keyboard keyboard, float delta)
    {
        if (Claimed)
        {
            _field = default;
            _window.StartTextInput();
            return;
        }

        _field = Focused();
        if (_field.IsNull || !_field.HasComponent<TextField>())
        {
            _field = default;
            _typed.Clear();
            _window.StopTextInput();
            return;
        }

        _window.StartTextInput();
        ref TextField field = ref _field.GetComponent<TextField>();
        string value = field.Value;

        bool ctrl = keyboard.IsDown(KeyCode.LeftControl) || keyboard.IsDown(KeyCode.RightControl);
        if (ctrl && keyboard.IsPressed(KeyCode.V) && SDL.SDL_GetClipboardText() is { Length: > 0 } clip)
            _typed.Append(clip.ReplaceLineEndings(" "));

        if (BackspaceFires(keyboard, delta) && value.Length > 0)
            value = value[..^(char.IsLowSurrogate(value[^1]) && value.Length > 1 ? 2 : 1)];

        if (_typed.Length > 0)
        {
            value += _typed.ToString();
            _typed.Clear();
        }

        value = field.Numeric ? NumericOnly(value) : value;
        if (value != field.Value)
            field.Value = value;

        if (keyboard.IsPressed(KeyCode.Return) || keyboard.IsPressed(KeyCode.KeypadEnter))
        {
            _field.RemoveTag<TextFieldFocused>();
            _field = default;
            _window.StopTextInput();
        }
    }

    private bool BackspaceFires(Keyboard keyboard, float delta)
    {
        if (keyboard.IsPressed(KeyCode.Backspace))
        {
            _backspaceHeld = 0;
            return true;
        }

        if (!keyboard.IsDown(KeyCode.Backspace))
            return false;

        float before = _backspaceHeld;
        _backspaceHeld += delta;
        if (_backspaceHeld < RepeatDelay)
            return false;

        // One delete each time the held time crosses another repeat step.
        return (int)((before - RepeatDelay) / RepeatEvery) != (int)((_backspaceHeld - RepeatDelay) / RepeatEvery)
               || before < RepeatDelay;
    }

    private Entity Focused()
    {
        foreach (Entity entity in _entities
                     .Query<TextField>()
                     .AllTags(Tags.Get<TextFieldFocused>())
                     .ToEntityList())
            return entity;

        return default;
    }

    private static string NumericOnly(string value)
    {
        var builder = new StringBuilder();
        bool dot = false;
        foreach (char c in value)
        {
            if (c == '.' && !dot)
                dot = true;
            else if (!char.IsDigit(c) && !(c == '-' && builder.Length == 0))
                continue;
            builder.Append(c);
        }

        return builder.ToString();
    }
}
