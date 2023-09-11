// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Screens.Player;

namespace XanaduProject.Screens.StageUI
{
    [SuperNode(typeof(Dependent))]
    public partial class ComboCounter : Control
    {
        public override partial void _Notification(int what);

        [Dependency]
        private ScoreProcessor scoreProcessor => DependOn<ScoreProcessor>();

        public override void _Process(double delta)
        {
            base._Process(delta);
            PivotOffset = Size / 2;
        }

        public void OnResolved() =>
            scoreProcessor.OnComboChanged += i =>
            {
                AnimationPlayer animationPlayer = GetNode<AnimationPlayer>("ComboAnimator");

                animationPlayer.Stop();
                animationPlayer.Play("animate");

                GetNode<Label>("Number").Text = i.ToString();
            };
    }
}
