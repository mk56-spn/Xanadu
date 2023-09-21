// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Audio;

namespace XanaduProject.Screens.StageUI
{
    [GlobalClass]
    [SuperNode(typeof(Dependent))]
    public partial class StagePause : Control
    {
        public override partial void _Notification(int what);

        public event Action? RestartRequest;

        [Dependency]
        private TrackHandler trackHandler => DependOn<TrackHandler>();

        public override void _Ready()
        {
            base._Ready();

            Visible = false;
            VisibilityChanged += () =>
            {
                GetTree().Paused = Visible;
                trackHandler.TogglePlayback();
            };
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
            GetNode<Button>("ButtonContainer/Restart").ButtonUp += () => RestartRequest?.Invoke();
        }
    }
}
