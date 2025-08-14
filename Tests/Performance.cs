using System.Globalization;
using Godot;

namespace XanaduProject.Tests
{
    public partial class Performance : CanvasLayer
    {
        private Label label = new();

        public override void _Ready()
        {
            base._Ready();

            Layer = 10;
            AddChild(label);

            label.CustomMinimumSize = GetViewport().GetVisibleRect().Size + new Vector2(-200, 200);
            label.HorizontalAlignment = HorizontalAlignment.Right;
            label.Modulate = Colors.DeepSkyBlue;

            var t = new Timer();
            AddChild(t);

            var gradient = new Gradient { Colors = [Colors.Red, Colors.Green], Offsets = [0, 1] };
            t.OneShot = false;
            t.WaitTime = 0.3f;
            t.Start();
            t.Timeout += () =>
            {
                double calls = Godot.Performance.GetMonitor(Godot.Performance.Monitor.RenderTotalDrawCallsInFrame);
                label.Modulate = gradient.Sample((float)((20000 - calls) / 20000));
                label.Text = calls.ToString(CultureInfo.InvariantCulture);
            };
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (@event is InputEventKey { KeyLabel: Key.F11 })
                Visible = !Visible;
        }
    }
}
