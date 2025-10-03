// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Buttons
{
    public partial class AnimatedSlider : Control
    {
        [Signal]
        public delegate void ValueChangedEventHandler(double value);

        private double value;
        public double Value
        {
            get => value;
            set
            {
                double clampedValue = Mathf.Clamp(value, MinValue, MaxValue);
                if (Mathf.IsEqualApprox(this.value, clampedValue)) return;

                this.value = clampedValue;
                targetNubX = valueToPosition(this.value);
            }
        }

        public void SetValueSilent(double newValue)
        {
            double clampedValue = Mathf.Clamp(newValue, MinValue, MaxValue);
            value = clampedValue;
            targetNubX = valueToPosition(value);
            currentNubX = targetNubX;
            QueueRedraw();
        }

        public double MinValue { get; set; } = 0.0;
        public double MaxValue { get; set; } = 1.0;
        public double Step { get; set; } = 0.01;
        public bool ConsiderNubWidthForPlacement { get; set; } = false;

        private float currentNubX;
        private float targetNubX;
        private bool isDragging;

        private readonly float transitionSpeed = 15.0f;
        private readonly Color nubColor = Colors.Gold;
        private readonly Color bgColor = Colors.Black;
        private readonly Color fillColor = Colors.Gold.Darkened(0.3f);
        private readonly float margin = 4f;

        public AnimatedSlider()
        {
            CustomMinimumSize = new Vector2(100, 20);
        }

        public override void _Ready()
        {
            targetNubX = valueToPosition(Value);
            currentNubX = targetNubX;
        }

        public override void _Process(double delta)
        {
            if (Mathf.IsEqualApprox(currentNubX, targetNubX))
            {
                return;
            }

            float t = (float)delta * transitionSpeed;
            currentNubX = Mathf.Lerp(currentNubX, targetNubX, t);

            double newValue = positionToValue(currentNubX);
            if (!Mathf.IsEqualApprox(value, newValue))
            {
                value = newValue;
                EmitSignal(SignalName.ValueChanged, value);
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            // Background
            DrawRect(new Rect2(Vector2.Zero, Size), bgColor);

            // Filled portion
            DrawRect(new Rect2(Vector2.Zero, new Vector2(currentNubX, Size.Y)), fillColor);

            // Border
            DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black with { A = 0.5f }, false, 2);

            // Nub
            float nubSize = getNubSize();
            var nubRect = new Rect2(currentNubX - nubSize / 2, margin, nubSize, nubSize);
            DrawRect(nubRect, nubColor);
            DrawRect(nubRect, Colors.Black with { A = 0.5f }, false, 2);
        }

        public override void _GuiInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                {
                    isDragging = mouseButton.Pressed;
                    if (isDragging)
                    {
                        updateValueFromMouse(mouseButton.Position);
                    }

                    break;
                }
                case InputEventMouseMotion mouseMotion when isDragging:
                    updateValueFromMouse(mouseMotion.Position);
                    break;
            }
        }

        private void updateValueFromMouse(Vector2 mousePosition)
        {
            float newPosition;
            if (ConsiderNubWidthForPlacement)
            {
                float nubSize = getNubSize();
                float startOffset = nubSize / 2f;
                float endOffset = Size.X - nubSize / 2f;
                newPosition = Mathf.Clamp(mousePosition.X, startOffset, endOffset);
            }
            else
            {
                newPosition = Mathf.Clamp(mousePosition.X, 0, Size.X);
            }

            double newValue = positionToValue(newPosition);

            // Snap to step
            double snappedValue = Mathf.Round(newValue / Step) * Step;
            Value = snappedValue;
        }

        private float getNubSize()
        {
            return Size.Y - margin * 2;
        }

        private float valueToPosition(double val)
        {
            if (ConsiderNubWidthForPlacement)
            {
                float nubSize = getNubSize();
                float effectiveWidth = Size.X - nubSize;
                float startOffset = nubSize / 2f;
                return (float)((val - MinValue) / (MaxValue - MinValue) * effectiveWidth + startOffset);
            }
            else
            {
                return (float)((val - MinValue) / (MaxValue - MinValue) * Size.X);
            }
        }

        private double positionToValue(float pos)
        {
            if (ConsiderNubWidthForPlacement)
            {
                float nubSize = getNubSize();
                float effectiveWidth = Size.X - nubSize;
                float startOffset = nubSize / 2f;
                float adjustedPos = pos - startOffset;
                return (adjustedPos / effectiveWidth) * (MaxValue - MinValue) + MinValue;
            }
            else
            {
                return (pos / Size.X) * (MaxValue - MinValue) + MinValue;
            }
        }
    }
}
