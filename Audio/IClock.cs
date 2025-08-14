// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;

namespace XanaduProject.Audio
{
    public interface IClock
    {
        /// <summary>
        /// The current beats-per-minute of the song.
        /// </summary>
        double CurrentBpm { get; }

        public (double timingPoint, double bpm)[] TimingPoints { get; init; }

        /// <summary>
        /// The current position in the song, measured in beats.
        /// </summary>
        double CurrentBeat { get; }

        /// <summary>
        /// The precise, non-smoothed playback time in seconds.
        /// Ideal for game logic that requires sample-accurate timing.
        /// </summary>
        double PlaybackTimeSec { get; }

        /// <summary>
        /// The total length of the track in seconds.
        /// </summary>
        double TrackLength { get; }

        /// <summary>
        /// Event triggered when playback starts.
        /// </summary>
        public event Action Started;

        /// <summary>
        /// Occurs when playback is stopped.
        /// </summary>
        /// <remarks>
        /// The <c>Stopped</c> event is triggered when the <see cref="Stop"/> method is executed,
        /// signaling that the clock's timing and audio playback processes have been completely halted.
        /// Subscribers to this event can use it to perform cleanup or update related components to
        /// reflect the stopped state of the clock.
        /// </remarks>
        public event Action? Stopped;

        public float SnappedTime(int snapsPerBeat = 48)
        {
            // Guard clauses ────────────────────────────────────────────────────
            if (TimingPoints is null || TimingPoints.Length == 0 || snapsPerBeat <= 0)
                return (float)PlaybackTimeSec;

            // 1. Find the most recent timing point whose offset ≤ current time.
            (double offsetSec, double bpm) active = TimingPoints[0];
            for (int i = 1; i < TimingPoints.Length; ++i)
                if (PlaybackTimeSec >= TimingPoints[i].timingPoint)
                    active = TimingPoints[i];
                else
                    break;

            double secondsPerBeat = 60.0 / active.bpm;

            // 2. Time elapsed since that timing point.
            double deltaSec = PlaybackTimeSec - active.offsetSec;

            // 3. Snap Δt to the nearest beat subdivision.
            double subdivision = secondsPerBeat / snapsPerBeat;
            double snappedDeltaSec = Math.Round(deltaSec / subdivision) * subdivision;

            // 4. Combine back with timing-point offset.
            double snappedTimeSec = active.offsetSec + snappedDeltaSec;

            return (float)snappedTimeSec;
        }


        /// <summary>
        /// Event triggered to toggle playback state between paused and running.
        /// </summary>
        /// <remarks>
        /// The <c>TogglePlayback</c> event passes a boolean parameter, indicating the current playback state.
        /// A value of <c>true</c> implies the clock is paused, while <c>false</c> signifies that
        /// the clock is actively playing. Subscribers to this event can adjust their behaviour based
        /// on the updated clock state.
        /// </remarks>
        public event Action<bool>? PlaybackToggled;

        /// <summary>
        /// Gets a value indicating whether the clock is currently paused.
        /// </summary>
        /// <remarks>
        /// When the clock is paused, it halts its timing and playback operations,
        /// allowing for actions like temporarily stopping audio or animations while preserving the current state.
        /// This property is set by calling the <see cref="Pause"/> or <see cref="Resume"/> methods.
        /// </remarks>
        public bool IsPaused { get; }
    }
}
