using Godot;
using XanaduProject.Audio;
using XanaduProject.GameDependencies;

namespace XanaduProject.Stage.Masters.Composer.Timelines
{
    /// <summary>
    /// Draws the BPM-based grid and a dedicated top-bar panel showing the current beat.
    /// Derivations add their own visual content by overriding DrawBody().
    /// </summary>
    public abstract partial class Timeline : VBoxContainer
    {
        private readonly IClock clock = DiProvider.Get<IClock>();

        // ---- layout / style -------------------------------------------------
        public const int TOP_BAR_HEIGHT = 30; // Height of the panel
        private const float default_scale = 400f; // Pixels per second
        protected readonly Color MeasureLineColor = new(0.6f, 0.6f, 0.6f);

        // ---- state ----------------------------------------------------------
        protected float HorizontalScale { get; set; } = default_scale;
        protected (double timingPoint, double bpm)[] Timing => clock.TimingPoints;

        //---------------------------------------------------------------------
        public Timeline()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        //---------------------------------------------------------------------
        public override void _Process(double delta)
        {
            QueueRedraw();
        }

        //---------------------------------------------------------------------
        public sealed override void _Draw()
        {
            DrawLine(new Vector2(Size.X / 2, 0), new Vector2(Size.X / 2, Size.Y), Colors.Green);
            // Shift drawing so playback position is centred at x = 0
            DrawSetTransform(new Vector2(Size.X / 2f - (float)(clock.PlaybackTimeSec * HorizontalScale),
                0));

            drawBeatLines();
            DrawBody();
        }

        protected abstract void DrawBody(); // subclasses override

        //---------------------------------------------------------------------
        private void drawBeatLines()
        {
            if (Timing.Length == 0) return;

            float viewportWidth = Size.X;

            for (int i = 0; i < Timing.Length; i++)
            {
                var tp = Timing[i];
                if (tp.bpm <= 0) continue;

                double start = tp.timingPoint;
                double end = i + 1 < Timing.Length ? Timing[i + 1].timingPoint : double.MaxValue;
                double beat = 60.0 / tp.bpm;

                for (double t = start; t < end; t += beat)
                {
                    float x = (float)(t * HorizontalScale);
                    if (x > viewportWidth) break;
                    if (x >= 0)
                        DrawLine(new Vector2(x, TOP_BAR_HEIGHT),
                            new Vector2(x, Size.Y),
                            MeasureLineColor);
                }
            }
        }
    }
}
