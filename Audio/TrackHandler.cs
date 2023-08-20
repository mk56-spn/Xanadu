// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Audio
{
    /// <summary>
    /// The track manager is a source of truth for audio timing across a gameplay instance.
    /// </summary>
    public partial class TrackHandler : Node
    {
        /// <summary>
        /// The time in seconds between every 1/1 beat;
        /// </summary>
        public double SecondsPerBeat { get; private set; }

        /// <summary>
        /// The furthest beat measure that has been passed so far in the playback of the track.
        /// </summary>
        public int LastPlayedBeat { get; private set; }

        /// <summary>
        /// The BPM of the track.
        /// </summary>
        public double Bpm { get; private set; } = 120;

        /// <summary>
        /// The current position in the song.
        /// </summary>
        public double TrackPosition { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public int Measure { get; private set; }

        /// <summary>
        /// How many measures there are per every major measure.
        /// </summary>
        public int Measures { get; private set; } = 4;

        /// <summary>
        /// The length of this track in seconds
        /// </summary>
        public double TrackLength => audio.Stream.GetLength();

        /// <summary>
        /// Called on every 1/1 beat
        /// </summary>
        public event EventHandler<int>? OnBeat;

        private int positionInBeats;

        private double offset => SecondsPerBeat * 4;

        private readonly AudioStreamPlayer audio = new AudioStreamPlayer();

        public TrackHandler ()
        {
            AddChild(audio);
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            TrackPosition = audio.GetPlaybackPosition() + AudioServer.GetTimeSinceLastMix();
            TrackPosition -= AudioServer.GetOutputLatency();
            positionInBeats = (int)Math.Floor(TrackPosition / SecondsPerBeat) + 1;

            reportBeat();
        }

        private void reportBeat()
        {
            if (LastPlayedBeat < positionInBeats is false)
                return;

            if (Measure > Measures)
                Measure = 1;

            OnBeat?.Invoke(this, positionInBeats);

            LastPlayedBeat = positionInBeats;
            Measure++;
        }

        public void SetTrack(TrackInfo info)
        {
            audio.Stream = info.Track;

            Bpm = info.Bpm;
            SecondsPerBeat = 60f / info.Bpm;
        }

        public void StartTrack()
        {
            Timer timer = new Timer();

            AddChild(timer);
            timer.OneShot = true;
            timer.WaitTime = offset;
            timer.Start();

            timer.Timeout += () => audio.Play();
        }
    }
}
