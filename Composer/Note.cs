// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Perceptions;
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

        public void Activate()
        {
            hitBox.Monitorable = false;

            double deviation = Math.Abs(TimeSpan.FromSeconds(Position.X / Perception.BASE_VELOCITY - SingletonSource.GetAudioSource().TrackPosition).TotalMilliseconds);
            var judgement = JudgementInfo.GetJudgement(deviation);

            judgementText.Text = JudgementInfo.GetJudgmentText(judgement).ToUpper();

            animation.AssignedAnimation = "Animate";
            animation.Play();
        }
    }
}
