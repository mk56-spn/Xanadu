// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.GameDependencies;

namespace XanaduProject.Composer
{
    public partial class Waveform : VBoxContainer
    {
        private IComponent component = new ActiveColourEcs();
        private const int rate = 44;
        private const float audio_rate = 44100 / 44f;
        private float spacing = 1f;
        private const int height = 50;
        private float offset;
        private int size = 1500;
        private Vector2[] points = null!;

        private readonly Color bpmLineColor = new(1, 0.8f, 0.2f); // gold
        private readonly Color _quarterLineColor = new(0.6f, 0.6f, 0.6f); // grey


        private Font defaultFont = ThemeDB.FallbackFont;
        private int defaultFontSize = ThemeDB.FallbackFontSize;

        private readonly IClock clock = DiProvider.Get<IClock>();

        public override void _EnterTree()
        {
            CustomMinimumSize = new Vector2(1000, 100);
            clock.Started += () =>
            {
                QueueRedraw();
            };

            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        private Font font = ThemeDB.FallbackFont;
        private int fSize = ThemeDB.FallbackFontSize;

        public override void _Process(double delta)
        {
            base._Process(delta);
            QueueRedraw();
        }

        private const float horizontal_scale = 200f; // Controls the zoom/spacing of elements on the timeline

        public override void _Draw()
        {
            DrawSetTransform(-new Vector2((float)(200 *  clock.PlaybackTimeSec), 0));
            var timingPoints = clock.TimingPoints.ToList();
            if (!timingPoints.Any())
                return;

            var panelSize = Size;

            // Draw BPM text labels
            foreach (var timing in timingPoints)
                DrawString(font, new Vector2((float)timing.timingPoint * horizontal_scale, 0),
                    timing.bpm.ToString(CultureInfo.CurrentCulture), modulate: Colors.Green);

            // Draw measure lines
            for (int i = 0; i < timingPoints.Count; i++)
            {
                var currentTiming = timingPoints[i];
                double bpm = currentTiming.bpm;

                if (bpm <= 0)
                    continue;

                // Determine the time range for the current BPM
                double startTime = currentTiming.timingPoint;
                double endTime = i + 1 < timingPoints.Count
                    ? timingPoints[i + 1].timingPoint
                    : double.MaxValue; // Or song duration if available

                // Calculate duration of one measure (assuming 4 beats per measure)
                const int beats_per_measure = 4;
                double measureDuration = beats_per_measure * (60.0 / bpm);
                if (measureDuration <= 0)
                    continue;

                // Draw a line for each measure in the current BPM section
                for (double measureTime = startTime; measureTime < endTime; measureTime += measureDuration)
                {
                    float x = (float)(measureTime * horizontal_scale);

                    // Stop drawing if we're past the right edge of the panel
                    if (x > panelSize.X)
                        break;

                    // Only draw if the line is within the panel's visible area
                    if (x >= 0) DrawLine(new Vector2(x, 0), new Vector2(x, panelSize.Y), bpmLineColor);
                }
            }
        }
    }
}
