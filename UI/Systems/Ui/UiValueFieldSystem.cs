using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// Runs every <see cref="UiValueField"/>: a slot pressed and dragged sideways
/// scrubs its number (a float across the field's range in
/// <see cref="ScrubSpan"/> px, a whole number one step per
/// <see cref="StepPixels"/> px), pressed and let go it takes typing, applied as
/// it parses and finally when focus leaves; a right-click resets it; toggles,
/// steppers and a colour's picker do the rest. Unfocused slots always show the
/// value. Each edit is written back through the store so the owner hears it
/// (<see cref="UiValueFields.OnValueChanged"/>), and a field with a
/// <see cref="UiMaterialBinding"/> puts it on the material.
/// </summary>
public sealed class UiValueFieldSystem(IPointerInputSource pointer) : QuerySystem<UiValueField>
{
    /// <summary>How far a slot is dragged before it scrubs instead of taking typing.</summary>
    public const float DeadZone = 3;

    /// <summary>The drag, in px, that covers a float field's whole range.</summary>
    public const float ScrubSpan = 200;

    /// <summary>The drag, in px, per step of a whole number.</summary>
    public const float StepPixels = 8;

    private Entity _grabbed;
    private float _grabX;
    private float _grabValue;
    private bool _scrubbing;
    private bool _wasSecondary;

    protected override void OnUpdate()
    {
        PointerInput input = pointer.Read();
        bool resetStarted = input.SecondaryPressed && !_wasSecondary;
        _wasSecondary = input.SecondaryPressed;
        if (!input.Pressed)
        {
            _grabbed = default;
            _scrubbing = false;
        }

        // Edits tag slots and write the field back, which a running query forbids.
        foreach (Entity entity in Query.ToEntityList())
            Update(entity, input, resetStarted);
    }

    private void Update(Entity entity, PointerInput input, bool resetStarted)
    {
        ref UiValueField field = ref entity.GetComponent<UiValueField>();
        bool changed = false, live = false;

        changed |= Step(ref field, field.Less, ref field.LessHeld, -1);
        changed |= Step(ref field, field.More, ref field.MoreHeld, +1);

        for (int i = 0; i < field.Slots.Length; i++)
        {
            Entity slot = field.Slots[i];
            if (slot.IsNull)
                continue;
            if (field.Shape.Scalar == UiScalar.Bool)
                changed |= SyncToggle(ref field, slot, i);
            else
            {
                changed |= UpdateSlot(ref field, slot, i, input, resetStarted, ref live);
                if (slot.HasComponent<TextField>())
                    slot.GetComponent<TextField>().Fill = MarkFill(field.Mark);
            }
        }

        if (!field.Picker.IsNull)
            changed |= SyncPicker(ref field, ref live);

        bool ended = field.Live && !live;
        field.Live = live;
        if (!changed && !ended)
            return;

        if (entity.TryGetComponent(out UiMaterialBinding binding))
            Bind(binding, field);

        // Written back through the store so its owner hears it: the native
        // ComponentChanged event (Update) on the entity.
        UiValueField announced = field;
        entity.AddComponent(announced);
    }

    private static bool Step(ref UiValueField field, Entity button, ref bool held, int delta)
    {
        if (button.IsNull)
            return false;

        bool pressed = button.Tags.Has<Pressed>();
        bool stepped = pressed && !held;
        held = pressed;
        if (!stepped)
            return false;

        float next = field.Shape.Clamp(field.Values[0] + delta);
        if (next == field.Values[0])
            return false;
        field.Values[0] = next;
        return true;
    }

    /// <summary>A switch's toggle: a press flipped it, or the value was set from code and it catches up.</summary>
    private static bool SyncToggle(ref UiValueField field, Entity slot, int index)
    {
        ref Toggle toggle = ref slot.GetComponent<Toggle>();
        ref UiValueSlot state = ref slot.GetComponent<UiValueSlot>();
        bool value = field.Values[index] > 0.5f;
        if (toggle.Value != state.ShownOn)
        {
            field.Values[index] = toggle.Value ? 1 : 0;
            state.ShownOn = toggle.Value;
            return true;
        }

        if (value != toggle.Value)
            toggle.Value = value;
        state.ShownOn = value;
        return false;
    }

    private bool UpdateSlot(ref UiValueField field, Entity slot, int index, PointerInput input, bool resetStarted, ref bool live)
    {
        ref UiValueSlot state = ref slot.GetComponent<UiValueSlot>();
        ref TextField text = ref slot.GetComponent<TextField>();
        UiValueShape shape = field.Shape;
        bool changed = false;

        if (slot.Tags.Has<Pressed>() && _grabbed.IsNull)
        {
            _grabbed = slot;
            _grabX = input.Position.X;
            _grabValue = field.Values[index];
            _scrubbing = false;
        }

        if (resetStarted && Box(slot).HasPoint(input.Position) && !InputSystem.IsClippedOut(slot, input.Position))
        {
            changed |= Assign(ref field, index, field.Defaults[index]);
            CommandBuffer.RemoveTag<TextFieldFocused>(slot.Id);
            state.WasFocused = false;
        }

        bool focused = slot.Tags.Has<TextFieldFocused>();
        if (_grabbed == slot && input.Pressed)
        {
            float moved = input.Position.X - _grabX;
            _scrubbing |= MathF.Abs(moved) > DeadZone;
            if (_scrubbing)
            {
                // The press gave it focus; a drag is a scrub, not typing.
                CommandBuffer.RemoveTag<TextFieldFocused>(slot.Id);
                focused = false;
                state.WasFocused = false;
                float target = shape.IsWhole
                    ? _grabValue + MathF.Truncate(moved / StepPixels)
                    : _grabValue + moved / ScrubSpan * MathF.Max(1e-4f, field.Max - field.Min);
                changed |= Assign(ref field, index, target);
                live = true;
            }
        }

        if (focused)
        {
            live = true;
            if (TryParse(text.Value, out float typed))
                changed |= Assign(ref field, index, typed);
        }
        else if (state.WasFocused)
        {
            // Focus left: the typing is final; the field goes back to showing the value.
            changed = true;
        }

        state.WasFocused = focused;
        if (!focused)
        {
            string shown = Format(field.Values[index], shape);
            if (text.Value != shown)
                text.Value = shown;
        }

        PlaceBar(state.Bar, slot, field, index);
        return changed;
    }

    /// <summary>A colour's picker: dragging it sets the channels; otherwise it shows them.</summary>
    private static bool SyncPicker(ref UiValueField field, ref bool live)
    {
        ref UiColorPicker picker = ref field.Picker.GetComponent<UiColorPicker>();
        bool changed = false;
        if (picker.Dragging)
        {
            Color color = picker.Color;
            float[] channels = [color.R, color.G, color.B, color.A];
            for (int i = 0; i < Math.Min(4, field.Values.Length); i++)
                changed |= Assign(ref field, i, channels[i]);
            live = true;
        }
        else
        {
            Color shown = new(field.Values[0], field.Values[1], field.Values[2], field.Values[3]);
            if (!picker.Color.IsEqualApprox(shown))
                picker.Color = shown;
        }

        return changed;
    }

    private static bool Assign(ref UiValueField field, int index, float value)
    {
        value = field.Shape.Clamp(value);
        if (field.Values[index] == value)
            return false;
        field.Values[index] = value;
        return true;
    }

    private static void Bind(UiMaterialBinding binding, in UiValueField field)
    {
        if (binding.Material is null)
            return;
        if (field.Shape.IsMatrix)
            binding.Material.Set(binding.Name, UiValueFields.AsMatrix(field.Values, field.Shape));
        else
            binding.Material.Set(binding.Name, UiValueFields.AsVector(field.Values));
    }

    /// <summary>Lays the slot's bar along its foot, from zero (or the range's near end) to the value.</summary>
    /// <summary>A slot's fill for its key mark: the inset colour, tinted yellow, green or orange.</summary>
    public static Color? MarkFill(UiValueMark mark) => mark switch
    {
        UiValueMark.Keyed => Palette.SurfaceInset.Lerp(Palette.Gold, 0.32f),
        UiValueMark.Animated => Palette.SurfaceInset.Lerp(Palette.Success, 0.26f),
        UiValueMark.Changed => Palette.SurfaceInset.Lerp(Palette.Warning, 0.34f),
        _ => null,
    };

    private static void PlaceBar(Entity bar, Entity slot, in UiValueField field, int index)
    {
        if (bar.IsNull || !bar.HasComponent<Rectangle>())
            return;

        Rect2 box = slot.GetComponent<Rectangle>().Bounds;
        float span = MathF.Max(1e-6f, field.Max - field.Min);
        float from = Math.Clamp((0 - field.Min) / span, 0, 1);
        float to = Math.Clamp((field.Values[index] - field.Min) / span, 0, 1);
        float start = box.Size.X * MathF.Min(from, to);
        float width = box.Size.X * MathF.Abs(to - from);
        ref Rectangle rect = ref bar.GetComponent<Rectangle>();
        var bounds = new Rect2(-width / 2, -1, width, 2);
        if (rect.Bounds != bounds)
        {
            rect.Bounds = bounds;
            bar.AddTag<Dirty>();
        }

        // The slot's space is centred on it, so its left edge is at box.Position.X.
        UiLayout.Move(bar, new Vector2(box.Position.X + start + width / 2, box.End.Y - 1));
    }

    private static Rect2 Box(Entity part)
    {
        Rect2 bounds = part.GetComponent<Rectangle>().Bounds;
        Vector2 world = UiTransform.WorldPosition(part, part.GetComponent<Position>().Value);
        return new Rect2(world + bounds.Position, bounds.Size);
    }

    private static bool TryParse(string text, out float value) =>
        float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);

    /// <summary>How a slot shows its number: whole numbers bare, floats to three places, trailing zeros dropped.</summary>
    public static string Format(float value, UiValueShape shape) =>
        shape.IsWhole
            ? ((long)MathF.Round(value)).ToString(CultureInfo.InvariantCulture)
            : (MathF.Abs(value) < 5e-4f ? 0f : value).ToString("0.###", CultureInfo.InvariantCulture);
}
