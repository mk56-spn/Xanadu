using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Hidden native text input that mirrors whichever ECS <see cref="TextField"/>
/// has focus, so a UI host gets typing, caret and IME support without
/// reimplementing them. Add one to the host control with <see cref="Attach"/>.
/// </summary>
public sealed partial class UiTextBridge : LineEdit
{
    private EntityStore _entities = null!;
    private Entity _field;
    private bool _syncing;

    public static UiTextBridge Attach(Control host, EntityStore entities)
    {
        var bridge = new UiTextBridge
        {
            Position = new Vector2(-10_000, -10_000),
            Size = new Vector2(10, 10),
            Modulate = new Color(1, 1, 1, 0),
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.All,
            _entities = entities,
        };
        host.AddChild(bridge);
        return bridge;
    }

    public override void _Ready()
    {
        TextChanged += OnChanged;
        TextSubmitted += OnSubmitted;
    }

    public override void _Process(double delta)
    {
        Entity focused = FindFocused();
        if (focused.Id == _field.Id && !focused.IsNull)
            return;

        _field = focused;
        if (focused.IsNull)
        {
            ReleaseFocus();
            return;
        }

        string value = focused.GetComponent<TextField>().Value;
        _syncing = true;
        Text = value;
        CaretColumn = value.Length;
        GrabFocus();
        _syncing = false;
    }

    private Entity FindFocused()
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

    private void OnChanged(string value)
    {
        if (_syncing || _field.IsNull || !_field.HasComponent<TextField>())
            return;

        ref TextField field = ref _field.GetComponent<TextField>();
        string filtered = field.Numeric ? NumericOnly(value) : value;
        field.Value = filtered;
        if (filtered == value)
            return;

        _syncing = true;
        Text = filtered;
        CaretColumn = filtered.Length;
        _syncing = false;
    }

    private void OnSubmitted(string value)
    {
        if (_field.IsNull || !_field.HasComponent<TextField>())
            return;

        _field.RemoveTag<TextFieldFocused>();
        ReleaseFocus();
        _field = default;
    }

    private static string NumericOnly(string value)
    {
        var builder = new System.Text.StringBuilder();
        foreach (char c in value)
        {
            if (char.IsDigit(c) || c == '.' || (c == '-' && builder.Length == 0))
                builder.Append(c);
        }
        return builder.ToString();
    }
}
