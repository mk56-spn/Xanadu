// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;

namespace XanaduProject.Screens.StageUI
{
    [GlobalClass]
    public partial class StagePause : Control
    {
        public event EventHandler? RestartRequest;

        public override void _Ready()
        {
            base._Ready();

            Visible = false;
            VisibilityChanged += () => GetTree().Paused = Visible;
            buttonActions();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);

            if (@event is not InputEventKey { KeyLabel: Key.Escape }) return;
            Show();
        }

        private void buttonActions()
        {
            GetNode<Button>("ButtonContainer/Play").ButtonUp += Hide;
            GetNode<Button>("ButtonContainer/Quit").ButtonUp += () =>
                GetTree().ChangeSceneToFile("res://Screens/StageSelection/StageSelection.tscn");
            GetNode<Button>("ButtonContainer/Restart").ButtonUp += () =>
            {
                Hide();
                RestartRequest?.Invoke(this, EventArgs.Empty);
            };
        }
    }
}
