// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Audio
{
    /// <summary>
    /// Represents a high-level audio clock with additional functionality for controlling
    /// playback, such as pausing, restarting, and resuming audio tracks. Inherits core functionality
    /// from BaseClock and provides properties and methods specific to handling BPM and track timing.
    /// </summary>
    public partial class Clock(TrackInfo track) : BaseClock(track), IClock, IDisposable
    {

        private readonly TrackInfo track = track;

        /// <summary>
        /// Gets the current beats per minute (BPM) of the clock based on the active timing point.
        /// </summary>
        /// <remarks>
        /// The <see cref="CurrentBpm"/> property reflects the tempo at which the clock operates
        /// as defined by the track's timing points. It updates dynamically when the timing point changes
        /// during playback.
        /// </remarks>
        public double CurrentBpm { get; private set; } = track.TimingPoints[0].bpm;

        public double PlaybackTimeSec  => PlaybackTime;

        public event Action? Started;
        public event Action? Stopped;
        public event Action<bool>? PlaybackToggled;

        /// <summary>
        /// Gets a value indicating whether the clock is currently paused.
        /// </summary>
        /// <remarks>
        /// The <see cref="IsPaused"/> property reflects the playback state of the clock, where a value of <c>true</c>
        /// indicates that playback is temporarily halted but can be resumed, and a value of <c>false</c> indicates
        /// that playback is active or stopped. This property is updated internally when playback is toggled between
        /// paused and active states.
        /// </remarks>
        public bool IsPaused
        {
            get => Paused;
            private set
            {
               SetPaused(value);
                PlaybackToggled?.Invoke(value);
            }
        }

        /// <summary>
        /// Restarts the clock from the beginning. If it was already running, it will be stopped first.
        /// </summary>
        public void Restart()
        {
            GD.Print("Restarting clock...");
            // Stop() handles all the cleanup and state reset.
            Reset();

            // Re-create the player and kick off the new audio thread.
        }

        public void Start()  {}

        /// <summary>
        /// Pauses the clock, halting its timing and playback operations.
        /// This action effectively freezes the current state without resetting progress or losing data.
        /// </summary>
        /// <remarks>
        /// When called, the clock's process mode is disabled, and any active audio playback
        /// is temporarily stopped while retaining its current position in the audio stream.
        /// </remarks>
        public void Pause()
        {

            IsPaused = true;
            // This tells the AudioStreamPlayer to stop playing but keep its current buffer.
            GD.Print("Clock paused.");
        }
        /// <summary>
        /// Resumes the clock and the audio stream from where it left off.
        /// </summary>
        public void Resume()
        {
            IsPaused = false;

            GD.Print("Clock resumed.");
        }

        /// <summary>
        /// Toggles the pause state of the clock.
        /// </summary>
        public void TogglePause()
        {
            if (IsPaused)
                Resume();
            else
                Pause();
        }

        public override void _Process(double delta)
        {
            base._Process(delta);
            updateBpm();
        }

        public override void _ExitTree()
        {
            GlobalClock.Unregister(this);
            Dispose();
        }

        public new void Dispose()
        {
          /*  IsPlaying = false;
            // Wait for the thread to finish
            FeedThread?.Join();
            FeedThread = null;*/
        }




        private int bpmIndex;

        private void updateBpm()
        {
            // We check the next 3 bpm timing points, if we are skipping 3 timing points in a frame we have bigger issues
            for (int i = 0; i < 3; i++)
            {
                // Ensure we don't look past the end of the timing points list.
                if (bpmIndex + 1 >= track.TimingPoints.Length)
                    break;

                var nextTimingPoint = track.TimingPoints[bpmIndex + 1];

                if (PlaybackTimeSec >= nextTimingPoint.timingPoint)
                {
                    CurrentBpm = nextTimingPoint.bpm;
                    bpmIndex++;
                }
                else
                    // The next timing point is in the future.
                {
                    break;
                }
            }
        }
    }
}
