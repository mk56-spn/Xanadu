// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Singletons
{
    public sealed partial class AudioSource : AudioStreamPlayer
    {
        /// <summary>
        /// If set to true starts the audio stream on the next physics process tic to ensure the beat actions are synced;
        /// </summary>
        public bool RequestPlay;

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
        public int Measures { get; private set; } = 4;

        public double TrackPosition { get; private set; }
        public int Measure { get; private set; }

        private int positionInBeats;
        public event EventHandler<int>? OnNewBeat;


        public double SongProgressPercentage() =>
            Math.Round(GetPlaybackPosition() / Stream.GetLength(), 2) * 100;

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            if (RequestPlay)
            {
                RequestPlay = false;
                Play();
            }
            if (!Playing) return;

            TrackPosition = GetPlaybackPosition() + AudioServer.GetTimeSinceLastMix();
            TrackPosition -= AudioServer.GetOutputLatency();
            positionInBeats = (int)Math.Floor(TrackPosition / SecondsPerBeat) + 1;

            reportBeat();
        }

        private void reportBeat()
        {
            if (LastPlayedBeat < positionInBeats is false)
                return;

            /*GD.Print(lastPlayedBeat, " / ", positionInBeats, $" / {TrackPosition}");*/

            if (Measure > Measures)
                Measure = 1;

            OnNewBeat?.Invoke(this, positionInBeats);

            LastPlayedBeat = positionInBeats;
            Measure++;
        }

        public void SetTrack(TrackInfo trackInfo)
        {
            Bpm = trackInfo.Bpm;
            Stream = trackInfo.Track;
            Measures = trackInfo.Measures;

            Measure = 1;

            SecondsPerBeat = 60 / Bpm;
        }
    }
}
