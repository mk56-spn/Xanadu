// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Singletons;

namespace XanaduProject.Composer
{
    public partial class Note : Node2D
    {
        [Export]
        private Area2D hitBox { get; set; } = null!;

        [Export]
        private AnimationPlayer animation { get; set; } = null!;

        [Export]
        private Label judgementText { get; set; } = null!;

        [Export]
        public float PositionInTrack { get; private set; }

        public void Activate()
        {
            hitBox.Monitorable = false;

            double millisecondDeviation = TimeSpan.FromSeconds(PositionInTrack - SingletonSource.GetAudioSource().TrackPosition).TotalMilliseconds;

            GD.Print($"Note hit with position {PositionInTrack} seconds");
            GD.Print($"Deviation of {millisecondDeviation} milliseconds");

            var judgement = JudgementInfo.GetJudgement(Math.Abs(millisecondDeviation));

            judgementText.Text = $"{JudgementInfo.GetJudgmentText(judgement).ToUpper()} \n {(millisecondDeviation < 0 ? "late" : "early" )}";

            animation.AssignedAnimation = "Animate";
            animation.Play();
        }
    }
}
