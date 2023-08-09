// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Screens;

namespace XanaduProject.Composer
{
    public partial class Composer : Control
    {
        public StageInfo StageInfo = null!;

        public override void _Ready()
        {
            base._Ready();

            Stage stage = StageInfo.Stage.Instantiate<Stage>();

            AddChild(stage);
            AddChild(new PanningCamera());

            // Makes sure that the Composer's ready function is called after the core has loaded, avoiding the physics process being turned on automatically from there
            ProcessPriority = 2;
            stage.Core.SetPhysicsProcess(false);
        }

        private partial class PanningCamera : Camera2D
        {
            public override void _UnhandledInput(InputEvent @event)
            {
                base._UnhandledInput(@event);

                if (@event is InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } mouse)
                    Position -= mouse.Relative;
            }
        }
    }
}
