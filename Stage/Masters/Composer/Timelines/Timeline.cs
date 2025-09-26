using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.GameDependencies;

namespace XanaduProject.Stage.Masters.Composer.Timelines
{
    /// <summary>
    /// Draws the BPM-based grid and a dedicated top-bar panel showing the current beat.
    /// Derivations add their own visual content by overriding DrawBody().
    /// </summary>
    public abstract partial class AudioTimeline : TimelineBase
    {
        private readonly IClock clock = DiProvider.Get<IClock>();


        protected TimingPoint[] Timing => clock.TimingPoints;

        //---------------------------------------------------------------------

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
                if (tp.Bpm <= 0) continue;

                double start = tp.Value;
                double end = i + 1 < Timing.Length ? Timing[i + 1].Value : double.MaxValue;
                double beat = 60.0 / tp.Bpm;

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
