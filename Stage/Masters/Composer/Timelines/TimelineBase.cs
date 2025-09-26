// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Stage.Masters.Composer.Timelines
{
    public partial class TimelineBase : VBoxContainer
    {
        public TimelineBase()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        //---------------------------------------------------------------------
        public override void _Process(double delta)
        {
            QueueRedraw();
        }

        // ---- layout / style -------------------------------------------------
        public const int TOP_BAR_HEIGHT = 30; // Height of the panel
        public const float DEFAULT_SCALE = 400f; // Pixels per second
        protected readonly Color MeasureLineColor = new(0.6f, 0.6f, 0.6f);

        // ---- state ----------------------------------------------------------
        protected float HorizontalScale { get; set; } = DEFAULT_SCALE;
    }
}
