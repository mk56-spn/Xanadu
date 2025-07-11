// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;


namespace XanaduProject.Screens.StageUI
{
    [SuperNode(typeof(Dependent))]
    public partial class Transition : Control
    {
        public override partial void _Notification(int what);

        [Export]
        private Label stageText { get; set; } = null!;
        [Export]
        private Label variantText { get; set; } = null!;
        [Export]
        private Label genreText { get; set; } = null!;
        [Export]
        private AnimationPlayer animation { get; set; } = null!;


        /// <summary>
        /// Emitted after the transition animation is finished
        /// </summary>
        public event EventHandler? TransitionFinished;

        public void OnResolved()
        {
            animation.AnimationFinished += _ =>
            {
                Hide();
                TransitionFinished?.Invoke(this, EventArgs.Empty);
            };
        }
    }
}
