using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Editor;

/// <summary>Hidden native text bridge for ECS editor text fields.</summary>
public partial class EditorOverlay : CanvasLayer
{
    private void CreateTextFieldBridge()
    {
        _textInput = new LineEdit
        {
            Position = new Vector2(-10_000, -10_000),
            Size = new Vector2(10, 10),
            Modulate = new Color(1, 1, 1, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.All,
        };
        _textInput.TextChanged += OnTextFieldChanged;
        _textInput.TextSubmitted += OnTextFieldSubmitted;
        AddChild(_textInput);
    }

    private void SyncTextFieldBridge()
    {
        if (_textInput is null || _editor.IsNull)
            return;

        Entity focused = FindFocusedTextField();
        if (focused.Id == _textFieldEntity.Id && !focused.IsNull)
            return;

        _textFieldEntity = focused;
        if (focused.IsNull || !focused.HasComponent<TextField>())
        {
            _textInput.ReleaseFocus();
            return;
        }

        TextField field = focused.GetComponent<TextField>();
        _syncingText = true;
        _textInput.Text = field.Value;
        _textInput.CaretColumn = field.Value.Length;
        _textInput.GrabFocus();
        _syncingText = false;
    }

    private Entity FindFocusedTextField()
    {
        foreach (Entity entity in _entities
                     .Query<TextField>()
                     .AllTags(Tags.Get<TextFieldFocused>())
                     .ToEntityList())
        {
            return entity;
        }

        return default;
    }

    private void OnTextFieldChanged(string value)
    {
        if (_syncingText || _textFieldEntity.IsNull)
            return;

        if (!_textFieldEntity.HasComponent<TextField>())
            return;

        ref TextField field = ref _textFieldEntity.GetComponent<TextField>();
        string filtered = field.Numeric
            ? FilterNumeric(value)
            : value;
        field.Value = filtered;

        if (filtered != value)
        {
            _syncingText = true;
            _textInput.Text = filtered;
            _textInput.CaretColumn = filtered.Length;
            _syncingText = false;
        }
    }

    private void OnTextFieldSubmitted(string value)
    {
        if (_textFieldEntity.IsNull || !_textFieldEntity.HasComponent<TextField>())
            return;

        _textFieldEntity.RemoveTag<TextFieldFocused>();
        _textInput.ReleaseFocus();
        _textFieldEntity = default;
    }

    private void SetStatus(string message)
    {
        Entity editor = FindEditor();
        if (editor.IsNull || !editor.HasComponent<EditorHud>())
            return;

        ref EditorHud hud = ref editor.GetComponent<EditorHud>();
        hud.StatusText = message;
    }

    private static string FilterNumeric(string value)
    {
        var builder = new System.Text.StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsDigit(c) ||
                (c == '-' && builder.Length == 0) ||
                (c == '.' && builder.ToString().IndexOf('.') < 0))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
